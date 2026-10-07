using Microsoft.Win32;

namespace LunarSync;
internal static class StartupManager
{
    private const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
    internal static string InstalledExecutable=>Path.Combine(AppServices.InstallRoot,"app","LunarSync.exe");
    internal static bool IsInstalled=>File.Exists(Path.Combine(AppServices.InstallRoot,"lunarsync.install"))&&string.Equals(Path.GetFullPath(Environment.ProcessPath??""),InstalledExecutable,StringComparison.OrdinalIgnoreCase);
    internal static void SetEnabled(bool enabled)
    {
        if(!IsInstalled)return;
        using var key=Registry.CurrentUser.CreateSubKey(RunKey);
        if(enabled)key.SetValue("LunarSync","\""+InstalledExecutable+"\" --startup");
        else key.DeleteValue("LunarSync",false);
    }
}
