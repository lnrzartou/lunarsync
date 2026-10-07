using System.Text.Json;
using System.Text.Json.Serialization;

namespace LunarSync.Core;

[JsonConverter(typeof(JsonStringEnumConverter<MacroKind>))]
public enum MacroKind { Hold, Key, KeyAndClick }
public sealed record MacroDefinition(string Id, string Name, string Description, MacroKind Kind,
    int DefaultBase, int DefaultToggle, double DefaultCps);

public sealed class MacroConfig
{
    public string Id { get; set; } = "";
    public bool Configured { get; set; }
    public int BaseKey { get; set; }
    public int ToggleKey { get; set; }
    public double Cps { get; set; } = 35;
    public int PressMs { get; set; } = 3;
    public int GapMs { get; set; } = 3;
    public bool Irregular { get; set; }
    public int VariationPercent { get; set; } = 15;
    public MacroConfig Copy() => (MacroConfig)MemberwiseClone();
    public static MacroConfig Default(MacroDefinition d) => new() { Id=d.Id, BaseKey=d.DefaultBase, ToggleKey=d.DefaultToggle, Cps=d.DefaultCps };
    public void Validate(MacroDefinition d)
    {
        if (Id != d.Id || !Keys.IsAllowed(BaseKey) || !Keys.IsAllowed(ToggleKey)) throw new InvalidDataException("Touche non prise en charge.");
        if (BaseKey == ToggleKey) throw new InvalidDataException("La touche de base et le raccourci doivent être différents.");
        if (d.Kind != MacroKind.Hold && BaseKey <= 6) throw new InvalidDataException("Cette macro répète une touche du clavier.");
        if (!double.IsFinite(Cps) || Cps < 1 || Cps > 100 || PressMs < 2 || PressMs > 250 || GapMs < 0 || GapMs > 250 || VariationPercent is < 1 or > 40)
            throw new InvalidDataException("Cadence : 1–100 CPS. Appui : 2–250 ms. Intervalle : 0–250 ms. Variation : 1–40 %.");
        if (d.Kind != MacroKind.Hold && 1000 / Cps < MinimumPeriod(d.Kind))
            throw new InvalidDataException("La durée des appuis et intervalles dépasse la période choisie. Diminue les CPS ou les délais.");
    }
    public double MinimumPeriod(MacroKind kind) => kind == MacroKind.KeyAndClick ? PressMs * 2 + GapMs : PressMs;
}

public sealed class UserSettings
{
    public int SchemaVersion { get; set; } = 2;
    public string? KeyboardId { get; set; }
    public string Layout { get; set; } = "TKL";
    public string LanguageLayout { get; set; } = "AZERTY";
    public bool PanelTopmost { get; set; }
    public double PanelLeft { get; set; } = -1;
    public double PanelTop { get; set; } = -1;
    public bool StartWithWindows { get; set; } = true;
    public int SelectedProfile { get; set; } = 1;
    public List<MacroProfile> Profiles { get; set; } = [];
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public List<MacroConfig>? Macros { get; set; }

    public void Normalize(IReadOnlyList<MacroDefinition> catalog)
    {
        var existing=(Profiles??[]).Where(p=>p!=null&&p.Slot is >=1 and <=10).GroupBy(p=>p.Slot).ToDictionary(g=>g.Key,g=>g.First());
        Profiles=Enumerable.Range(1,10).Select(slot=>
        {
            var source=existing.GetValueOrDefault(slot)?.Macros??(slot==1?Macros:null)??[];
            var saved=source.Where(c=>c!=null).GroupBy(c=>c.Id).ToDictionary(g=>g.Key,g=>g.First());
            return new MacroProfile{Slot=slot,Macros=catalog.Select(d=>
            {
                var c=saved.GetValueOrDefault(d.Id,MacroConfig.Default(d)).Copy();
                try{c.Validate(d);return c;}catch{return MacroConfig.Default(d);}
            }).ToList()};
        }).ToList();
        SelectedProfile=Math.Clamp(SelectedProfile,1,10);Macros=null;SchemaVersion=2;
    }
}

public sealed class MacroProfile
{
    public int Slot { get; set; }
    public List<MacroConfig> Macros { get; set; } = [];
    [JsonIgnore] public string Name=>$"Configuration {Slot:00}";
}

public static class JsonFiles
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive=true, PropertyNamingPolicy=JsonNamingPolicy.CamelCase, WriteIndented=true, MaxDepth=24 };
    public static T Read<T>(string file) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(file), Options) ?? throw new InvalidDataException("Fichier vide.");
    public static void Write<T>(string file,T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        string tmp=file+".tmp";
        File.WriteAllBytes(tmp,JsonSerializer.SerializeToUtf8Bytes(value,Options));
        File.Move(tmp,file,true);
    }
}

public static class Keys
{
    public static readonly IReadOnlyDictionary<int,string> Names=Build();
    private static Dictionary<int,string> Build()
    {
        var d=new Dictionary<int,string> { [5]="Bouton souris 4",[6]="Bouton souris 5",[8]="Retour arrière",[9]="Tab",[13]="Entrée",[16]="Maj",[17]="Ctrl",[18]="Alt",[20]="Verr. maj",[32]="Espace",[33]="Page ↑",[34]="Page ↓",[35]="Fin",[36]="Début",[37]="←",[38]="↑",[39]="→",[40]="↓",[45]="Inser",[46]="Suppr" };
        for(int k=48;k<=57;k++)d[k]=((char)k).ToString();
        for(int k=65;k<=90;k++)d[k]=((char)k).ToString();
        for(int k=112;k<=123;k++)d[k]="F"+(k-111);
        return d;
    }
    public static bool IsAllowed(int vk)=>Names.ContainsKey(vk);
    public static string Name(int vk)=>vk==1?"Clic gauche":Names.GetValueOrDefault(vk,"?");
    public static int Normalize(int vk)=>vk switch { 160 or 161=>16,162 or 163=>17,164 or 165=>18,_=>vk };
}
