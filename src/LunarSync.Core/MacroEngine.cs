namespace LunarSync.Core;

public interface IInputOutput { void SetKey(int virtualKey,bool down); }
public sealed record MacroStatus(string Id,bool Configured,bool Enabled,bool Holding,double Cps,long Cycles);

public sealed class MacroEngine : IDisposable
{
    private sealed class Runtime(MacroDefinition definition,MacroConfig config)
    {
        public MacroDefinition Definition=definition;
        public MacroConfig Config=config;
        public bool Enabled,Holding;
        public int Phase;
        public double Next=double.PositiveInfinity,CycleStart;
        public long Cycles;
    }
    private readonly object sync=new();
    private readonly IInputOutput output;
    private readonly Func<double> clock;
    private readonly Random random;
    private readonly List<Runtime> macros=[];
    private readonly HashSet<int> physical=[];
    private readonly Dictionary<int,HashSet<string>> owners=[];
    private readonly Dictionary<int,double> reassert=[];
    private bool ready;
    public event Action? Changed;
    public event Action<int>? ActionKeyPressed;
    public event Action<string>? MacroUsed;

    public MacroEngine(IInputOutput output,Func<double> clock,Random? random=null)
    { this.output=output;this.clock=clock;this.random=random??Random.Shared; }

    public void Configure(IEnumerable<MacroDefinition> definitions,IEnumerable<MacroConfig> configs)
    {
        lock(sync)
        {
            StopAllInternal();macros.Clear();
            var map=configs.ToDictionary(c=>c.Id);
            foreach(var d in definitions)
            {
                var c=map.GetValueOrDefault(d.Id,MacroConfig.Default(d)).Copy();
                c.Validate(d);macros.Add(new(d,c));
            }
        }
        Changed?.Invoke();
    }
    public void SetReady(bool value)
    {
        lock(sync) {ready=value;if(!ready)StopAllInternal();}
        Changed?.Invoke();
    }
    public bool IsReady { get { lock(sync)return ready; } }
    public double DelayToNext()
    {
        lock(sync)
        {
            double next=macros.Where(m=>m.Enabled).Select(m=>m.Next).Concat(reassert.Values).DefaultIfEmpty(double.PositiveInfinity).Min();
            return Math.Clamp(next-clock(),0.2,1000);
        }
    }

    // Returns true only for an assigned toggle. Base inputs always pass through.
    public bool Input(int key,bool down)
    {
        key=Keys.Normalize(key);
        bool changed=false,consume;
        lock(sync)
        {
            bool edge=down?physical.Add(key):physical.Remove(key);
            consume=ready && macros.Any(m=>m.Config.Configured && m.Config.ToggleKey==key);
            if(!edge)return consume;
            var now=clock();
            if(!down && owners.ContainsKey(key))reassert[key]=now+2;
            if(ready && down && consume)
            {
                var group=macros.Where(m=>m.Config.Configured && m.Config.ToggleKey==key).ToArray();
                bool enable=!group.Any(m=>m.Enabled);
                foreach(var m in group)SetEnabled(m,enable,now);
                changed=true;
            }
            if(down&&ready&&macros.Any(m=>m.Definition.Kind==MacroKind.Hold&&m.Enabled&&m.Config.BaseKey==key))ActionKeyPressed?.Invoke(key);
            foreach(var m in macros.Where(m=>m.Definition.Kind==MacroKind.Hold && m.Config.BaseKey==key))
            {
                if(down && ready && m.Enabled)
                { m.Holding=true;m.Next=now+m.Config.GapMs;m.Phase=0;changed=true; }
                else if(!down)
                { m.Holding=false;m.Next=double.PositiveInfinity;Release(m.Definition.Id);changed=true; }
            }
        }
        if(changed)Changed?.Invoke();
        return consume;
    }
    public void Toggle(string id)
    {
        lock(sync)
        {
            var m=macros.Single(m=>m.Definition.Id==id);
            if(!ready||!m.Config.Configured)return;
            SetEnabled(m,!m.Enabled,clock());
        }
        Changed?.Invoke();
    }
    private void SetEnabled(Runtime m,bool enabled,double now)
    {
        Release(m.Definition.Id);m.Enabled=enabled;m.Holding=false;m.Phase=0;
        m.Next=enabled && m.Definition.Kind!=MacroKind.Hold ? now : double.PositiveInfinity;
    }
    public void Tick()
    {
        bool changed=false;
        lock(sync)
        {
            var now=clock();
            foreach(var key in reassert.Where(k=>k.Value<=now).Select(k=>k.Key).ToArray())
            {if(owners.ContainsKey(key)){output.SetKey(key,true);ActionKeyPressed?.Invoke(key);}reassert.Remove(key);}
            if(!ready)return;
            foreach(var m in macros)
            {
                if(!m.Enabled||m.Next>now)continue;
                var c=m.Config;
                if(m.Definition.Kind==MacroKind.Hold)
                {
                    if(m.Holding){Claim(m.Definition.Id,1);m.Cycles++;MacroUsed?.Invoke(m.Definition.Id);changed=true;}
                    m.Next=double.PositiveInfinity;continue;
                }
                switch(m.Phase)
                {
                    case 0:
                        m.CycleStart=now;Claim(m.Definition.Id,c.BaseKey);
                        m.Phase=1;m.Next=now+c.PressMs;break;
                    case 1:
                        Release(m.Definition.Id,c.BaseKey);
                        if(m.Definition.Kind==MacroKind.KeyAndClick){m.Phase=2;m.Next=now+c.GapMs;}
                        else CompleteCycle(m,now);
                        break;
                    case 2:
                        Claim(m.Definition.Id,1);m.Phase=3;m.Next=now+c.PressMs;break;
                    case 3:
                        Release(m.Definition.Id,1);CompleteCycle(m,now);break;
                }
                changed=true;
            }
        }
        if(changed)Changed?.Invoke();
    }
    private void CompleteCycle(Runtime m,double now)
    {
        m.Cycles++;MacroUsed?.Invoke(m.Definition.Id);m.Phase=0;
        m.Next=Math.Max(now+1,m.CycleStart+NextPeriod(m.Config,m.Definition.Kind,random));
    }
    public static double NextPeriod(MacroConfig c,MacroKind kind,Random rng)
    {
        double period=1000/c.Cps;
        if(c.Irregular)
        {
            // Symmetric variation in period preserves the requested long-run average.
            double spread=Math.Min(period*c.VariationPercent/100,Math.Max(0,period-c.MinimumPeriod(kind)-1));
            period+=(rng.NextDouble()*2-1)*spread;
        }
        return Math.Max(period,c.MinimumPeriod(kind)+1);
    }
    private void Claim(string id,int key)
    {
        if(!owners.TryGetValue(key,out var set)){set=[];owners[key]=set;}
        if(set.Add(id)&&set.Count==1){output.SetKey(key,true);ActionKeyPressed?.Invoke(key);}
    }
    private void Release(string id,int? onlyKey=null)
    {
        foreach(int key in owners.Keys.ToArray())
        {
            if(onlyKey.HasValue&&onlyKey!=key)continue;
            var set=owners[key];
            if(!set.Remove(id)||set.Count!=0)continue;
            owners.Remove(key);reassert.Remove(key);
            if(!physical.Contains(key))output.SetKey(key,false);
        }
    }
    public MacroStatus[] Snapshot()
    {
        lock(sync)return macros.Select(m=>new MacroStatus(m.Definition.Id,m.Config.Configured,m.Enabled,m.Holding,m.Config.Cps,m.Cycles)).ToArray();
    }
    public void StopAll()
    {lock(sync)StopAllInternal();Changed?.Invoke();}
    private void StopAllInternal()
    {foreach(var m in macros)SetEnabled(m,false,clock());reassert.Clear();}
    public void Dispose(){StopAll();}
}
