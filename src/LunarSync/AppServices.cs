using System.Diagnostics;
using System.IO;
using LunarSync.Core;

namespace LunarSync;

internal sealed class AppServices : IDisposable
{
    internal static string DataRoot=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LunarSync");
    internal static string InstallRoot=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","LunarSync");
    internal static readonly Version Version=ReadVersion();
    private static Version ReadVersion(){var v=System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!;return new(v.Major,v.Minor,v.Build);}
    internal readonly List<MacroDefinition> Catalog;
    internal UserSettings Settings;
    internal MacroProfile ActiveProfile=>Settings.Profiles.Single(p=>p.Slot==Settings.SelectedProfile);
    internal List<MacroConfig> ActiveMacros=>ActiveProfile.Macros;
    internal readonly StatisticsTracker Statistics;
    internal readonly NativeInput Input;
    internal readonly MacroEngine Engine;
    private readonly EngineRunner runner;
    private readonly Stopwatch clock=Stopwatch.StartNew();
    internal string? LoadWarning;
    internal event Action<string>? Error;
    internal AppServices()
    {
        Catalog=JsonFiles.Read<List<MacroDefinition>>(Path.Combine(AppContext.BaseDirectory,"catalog.json"));
        if(Catalog.Count is <1 or >100||Catalog.Select(d=>d.Id).Distinct().Count()!=Catalog.Count||Catalog.Any(d=>string.IsNullOrWhiteSpace(d.Id)||d.Name.Length>100||!Enum.IsDefined(d.Kind)))throw new InvalidDataException("Catalogue invalide.");
        string settingsFile=Path.Combine(DataRoot,"settings.json");
        try{Settings=File.Exists(settingsFile)?JsonFiles.Read<UserSettings>(settingsFile):new();}
        catch{Settings=new();LoadWarning="Les préférences illisibles ont été réinitialisées. Les macros sont désactivées.";}
        Settings.Normalize(Catalog);
        UsageStatistics? counts=null;
        try{string file=Path.Combine(DataRoot,"statistics.json");if(File.Exists(file))counts=JsonFiles.Read<UsageStatistics>(file);}
        catch{LoadWarning="Les statistiques illisibles ont été réinitialisées. Tes configurations sont conservées.";}
        Statistics=new(counts,Settings.SelectedProfile);
        Input=new();Engine=new(Input,()=>clock.Elapsed.TotalMilliseconds);
        Engine.ActionKeyPressed+=Statistics.KeyPressed;Engine.MacroUsed+=Statistics.MacroUsed;
        Engine.Configure(Catalog,ActiveMacros);
        Input.OnInput=Engine.Input;Input.EmergencyStop=Engine.StopAll;
        runner=new(Engine);runner.Failed+=message=>Error?.Invoke(message);
    }
    internal void Save()=>JsonFiles.Write(Path.Combine(DataRoot,"settings.json"),Settings);
    internal void SaveStatistics()=>JsonFiles.Write(Path.Combine(DataRoot,"statistics.json"),Statistics.Export());
    internal void SwitchProfile(int slot)
    {
        if(slot is <1 or >10)throw new ArgumentOutOfRangeException(nameof(slot));
        Engine.StopAll();Settings.SelectedProfile=slot;Statistics.SelectProfile(slot);
        Engine.Configure(Catalog,ActiveMacros);Save();
    }
    internal void ClearProfile()
    {Engine.StopAll();ActiveProfile.Macros=Catalog.Select(MacroConfig.Default).ToList();Engine.Configure(Catalog,ActiveMacros);Save();}
    internal void SaveMacro(MacroConfig config)
    {
        var definition=Catalog.Single(d=>d.Id==config.Id);config.Validate(definition);
        var others=ActiveMacros.Where(c=>c.Configured&&c.Id!=config.Id);
        if(others.Any(c=>c.ToggleKey==config.BaseKey||c.BaseKey==config.ToggleKey))throw new InvalidDataException("Ce raccourci entre en conflit avec la touche de base d’une autre macro.");
        var old=ActiveMacros.Single(c=>c.Id==config.Id);ActiveMacros[ActiveMacros.IndexOf(old)]=config;
        Engine.Configure(Catalog,ActiveMacros);Save();
    }
    public void Dispose(){runner.Dispose();Engine.Dispose();Input.Dispose();Save();SaveStatistics();}
}
