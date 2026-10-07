using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LunarSync.Core;
using Microsoft.Win32;

namespace LunarSync.Installer;
internal static class Program
{
    private static string Root=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","LunarSync");
    private const string RegistryPath=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\LunarSync";
    [STAThread] private static void Main(string[] args)
    {
        var app=new Application();bool uninstall=args.Contains("--uninstall");
        var window=new Window{Title=uninstall?"Désinstaller LunarSync":"Installer LunarSync",Width=620,Height=680,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=B("#101216"),Foreground=B("#F4EFE3"),FontFamily=new FontFamily("Segoe UI")};
        var stack=new StackPanel{Margin=new Thickness(46)};window.Content=stack;
        stack.Children.Add(new Image{Source=new BitmapImage(new Uri("pack://application:,,,/logo.png")),Height=102,Margin=new Thickness(0,0,0,20)});
        TextBlock Text(string text,double size=14,string color="#F4EFE3")=>new(){Text=text,FontSize=size,Foreground=B(color),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16)};
        stack.Children.Add(Text(uninstall?"Au revoir, LunarSync.":"Bienvenue dans LunarSync.",29));
        stack.Children.Add(Text(uninstall?"L’application sera retirée de cet ordinateur. Tes préférences seront conservées.":"Tes raccourcis, réunis au même endroit.\nDes macros préparées par Lunar et un panneau pour garder le contrôle.",15,"#A5A7AE"));
        stack.Children.Add(Text(uninstall?"Choisis « Quitter LunarSync » dans son icône près de l’horloge avant de continuer.":"Installation pour ton compte Windows.\nFermer la fenêtre conserve les macros en arrière-plan.",13,"#A5A7AE"));
        var desktop=new CheckBox{Content="Créer un raccourci sur le Bureau",IsChecked=true,Foreground=B("#F4EFE3"),Margin=new Thickness(0,0,0,20),Visibility=uninstall?Visibility.Collapsed:Visibility.Visible};stack.Children.Add(desktop);
        var startup=new CheckBox{Content="Lancer LunarSync avec Windows",IsChecked=true,Foreground=B("#F4EFE3"),Margin=new Thickness(0,0,0,20),Visibility=uninstall?Visibility.Collapsed:Visibility.Visible};stack.Children.Add(startup);
        try{if(File.Exists(SettingsFile))startup.IsChecked=JsonFiles.Read<UserSettings>(SettingsFile).StartWithWindows;}catch{}
        var status=Text("",12,"#A5A7AE");stack.Children.Add(status);
        var button=new Button{Content=uninstall?"Désinstaller":"Installer LunarSync",Background=B("#1D4168"),Foreground=B("#F4EFE3"),BorderBrush=B("#315579"),Padding=new Thickness(20,14,20,14),FontSize=15};stack.Children.Add(button);
        bool done=false;
        button.Click+=async(_,_)=>
        {
            if(done){if(!uninstall)Process.Start(new ProcessStartInfo(Path.Combine(Root,"app","LunarSync.exe")){UseShellExecute=true});window.Close();return;}
            try
            {
                using var existing=TryOpenMutex();if(existing!=null)throw new IOException("Choisis « Quitter LunarSync » dans son icône près de l’horloge, puis réessaie.");
                button.IsEnabled=false;status.Text=uninstall?"Suppression de l’application…":"Installation en cours…";
                bool makeShortcut=desktop.IsChecked==true,startWithWindows=startup.IsChecked==true;
                // Shortcut COM runs on the STA UI thread; archive extraction is done in the background.
                if(uninstall)await Task.Run(Uninstall);else{await Task.Run(InstallFiles);CreateShortcuts(makeShortcut);WriteRegistry();ConfigureStartup(startWithWindows);}
                done=true;status.Text=uninstall?"LunarSync a été désinstallé. Tes préférences sont conservées.":"Installation terminée. Toutes les macros démarrent désactivées.";button.Content=uninstall?"Fermer":"Ouvrir LunarSync";
            }
            catch(Exception ex){status.Text=ex.Message;}
            finally{button.IsEnabled=true;}
        };
        app.Run(window);
    }
    private static SolidColorBrush B(string color)=>new((Color)ColorConverter.ConvertFromString(color));
    private static Mutex? TryOpenMutex(){try{return Mutex.OpenExisting(@"Local\LunarSync.Application");}catch(WaitHandleCannotBeOpenedException){return null;}}
    private static void InstallFiles()
    {
        if(Directory.Exists(Root)&&Directory.EnumerateFileSystemEntries(Root).Any()&&!File.Exists(Path.Combine(Root,"lunarsync.install")))throw new IOException("Le dossier d’installation contient des fichiers qui n’appartiennent pas à LunarSync.");
        Directory.CreateDirectory(Root);
        string zip=Path.Combine(Root,"package-"+Guid.NewGuid().ToString("N")+".zip"),next=Path.Combine(Root,"next-"+Guid.NewGuid().ToString("N"));
        using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("LunarSync.Payload.zip")??throw new IOException("Paquet absent."))
        using(var output=File.Create(zip))resource.CopyTo(output);
        try{UpdateSecurity.ExtractPackage(zip,next);}finally{File.Delete(zip);}
        string app=Path.Combine(Root,"app"),backup=Path.Combine(Root,"backup-"+Guid.NewGuid().ToString("N"));bool previous=Directory.Exists(app);
        if(previous)Directory.Move(app,backup);
        try{Directory.Move(next,app);}
        catch{if(previous)Directory.Move(backup,app);throw;}
        File.WriteAllText(Path.Combine(Root,"lunarsync.install"),"LunarSync:4e59a586-1480-4fb6-8658-2910ad30ecda");
        string self=Environment.ProcessPath??throw new IOException("Chemin de l’installateur absent.");
        File.Copy(self,Path.Combine(Root,"LunarSync-Uninstall.exe"),true);
    }
    private static string StartShortcut=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"LunarSync.lnk");
    private static string DesktopShortcut=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"LunarSync.lnk");
    private static void CreateShortcuts(bool desktop)
    {
        var type=Type.GetTypeFromProgID("WScript.Shell")??throw new IOException("Création des raccourcis indisponible.");dynamic shell=Activator.CreateInstance(type)!;
        foreach(string path in desktop?new[]{StartShortcut,DesktopShortcut}:new[]{StartShortcut})
        {dynamic link=shell.CreateShortcut(path);link.TargetPath=Path.Combine(Root,"app","LunarSync.exe");link.WorkingDirectory=Path.Combine(Root,"app");link.Description="LunarSync · macros par Lunar";link.IconLocation=link.TargetPath+",0";link.Save();}
    }
    private static void WriteRegistry()
    {
        var version=Assembly.GetExecutingAssembly().GetName().Version!;
        using var key=Registry.CurrentUser.CreateSubKey(RegistryPath);key.SetValue("DisplayName","LunarSync");key.SetValue("DisplayVersion",$"{version.Major}.{version.Minor}.{version.Build}");key.SetValue("Publisher","Lunar");key.SetValue("InstallLocation",Root);key.SetValue("DisplayIcon",Path.Combine(Root,"app","LunarSync.exe"));key.SetValue("UninstallString","\""+Path.Combine(Root,"LunarSync-Uninstall.exe")+"\" --uninstall");key.SetValue("NoModify",1);key.SetValue("NoRepair",1);
    }
    private static string SettingsFile=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LunarSync","settings.json");
    private static void ConfigureStartup(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if(enabled)key.SetValue("LunarSync","\""+Path.Combine(Root,"app","LunarSync.exe")+"\" --startup");else key.DeleteValue("LunarSync",false);
        UserSettings settings;try{settings=File.Exists(SettingsFile)?JsonFiles.Read<UserSettings>(SettingsFile):new();}catch{settings=new();}
        settings.StartWithWindows=enabled;JsonFiles.Write(SettingsFile,settings);
    }
    private static void Uninstall()
    {
        if(!File.Exists(Path.Combine(Root,"lunarsync.install")))throw new IOException("Installation LunarSync introuvable.");
        string root=Path.GetFullPath(Root)+Path.DirectorySeparatorChar;
        foreach(var directory in Directory.EnumerateDirectories(Root))
        {
            string full=Path.GetFullPath(directory);
            if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new IOException("Chemin de suppression invalide.");
            if(Path.GetFileName(full)=="app"||Path.GetFileName(full).StartsWith("backup-")||Path.GetFileName(full).StartsWith("next-"))Directory.Delete(full,true);
        }
        if(File.Exists(StartShortcut))File.Delete(StartShortcut);if(File.Exists(DesktopShortcut))File.Delete(DesktopShortcut);Registry.CurrentUser.DeleteSubKeyTree(RegistryPath,false);
        using(var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true))run?.DeleteValue("LunarSync",false);
        // The running uninstaller is kept until it can be removed by the user.
    }
}
