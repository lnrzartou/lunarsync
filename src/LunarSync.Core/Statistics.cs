namespace LunarSync.Core;

public sealed class ProfileStatistics
{
    public Dictionary<int,long> Keys { get; set; } = [];
    public Dictionary<string,long> Macros { get; set; } = [];
}
public sealed class UsageStatistics
{
    public int SchemaVersion { get; set; } = 1;
    public Dictionary<int,ProfileStatistics> Profiles { get; set; } = [];
}

// Only aggregate macro counters are stored: no text, timestamps or unrelated input.
public sealed class StatisticsTracker
{
    private readonly object sync=new();
    private readonly UsageStatistics data;
    private int profile;
    public StatisticsTracker(UsageStatistics? initial=null,int selectedProfile=1)
    {
        data=initial??new();data.Profiles??=[];
        foreach(var slot in data.Profiles.Keys.ToArray())
        {
            if(slot is <1 or >10||data.Profiles[slot]==null){data.Profiles.Remove(slot);continue;}
            var p=data.Profiles[slot];p.Keys??=[];p.Macros??=[];
            p.Keys=p.Keys.Where(k=>(Keys.IsAllowed(k.Key)||k.Key==1)&&k.Value>=0).ToDictionary();
            p.Macros=p.Macros.Where(k=>k.Value>=0).ToDictionary();
        }
        SelectProfile(selectedProfile);
    }
    public void SelectProfile(int slot){if(slot is <1 or >10)throw new ArgumentOutOfRangeException(nameof(slot));lock(sync)profile=slot;}
    private ProfileStatistics Current()
    {if(!data.Profiles.TryGetValue(profile,out var p))data.Profiles[profile]=p=new();return p;}
    public void KeyPressed(int key)
    {lock(sync){var p=Current();p.Keys[key]=Increment(p.Keys.GetValueOrDefault(key));}}
    public void MacroUsed(string id)
    {lock(sync){var p=Current();p.Macros[id]=Increment(p.Macros.GetValueOrDefault(id));}}
    private static long Increment(long value)=>value==long.MaxValue?value:value+1;
    public UsageStatistics Export()
    {lock(sync)return new(){Profiles=data.Profiles.ToDictionary(p=>p.Key,p=>new ProfileStatistics{Keys=new(p.Value.Keys),Macros=new(p.Value.Macros)})};}
    public ProfileStatistics Snapshot(int? slot=null)
    {
        lock(sync)
        {
            var result=new ProfileStatistics();
            foreach(var p in data.Profiles.Where(p=>slot==null||p.Key==slot).Select(p=>p.Value))
            {
                foreach(var k in p.Keys)result.Keys[k.Key]=Add(result.Keys.GetValueOrDefault(k.Key),k.Value);
                foreach(var k in p.Macros)result.Macros[k.Key]=Add(result.Macros.GetValueOrDefault(k.Key),k.Value);
            }
            return result;
        }
    }
    private static long Add(long a,long b)=>a>long.MaxValue-b?long.MaxValue:a+b;
}
