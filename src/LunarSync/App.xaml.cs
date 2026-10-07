using System.Diagnostics;
using System.Windows;
using LunarSync.Core;

namespace LunarSync;
public partial class App : System.Windows.Application
{
    private Mutex? singleton;
    private EventWaitHandle? showSignal;
    private RegisteredWaitHandle? showWait;
    internal static bool ExitRequested;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode=ShutdownMode.OnExplicitShutdown;
        if(e.Args.FirstOrDefault()=="--apply-update")
        {
            try{await UpdateClient.ApplyStagedAsync(e.Args);}catch(Exception ex){MessageBox.Show(ex.Message,"Mise à jour interrompue",MessageBoxButton.OK,MessageBoxImage.Error);}
            Shutdown();return;
        }
        showSignal=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\LunarSync.ShowWindow");
        singleton=new Mutex(true,@"Local\LunarSync.Application",out bool created);
        if(!created){if(!e.Args.Contains("--startup"))showSignal.Set();Shutdown();return;}
        DispatcherUnhandledException+=(_,args)=>
        {
            if(MainWindow is MainWindow main)main.StopMacros();
            MessageBox.Show("Les macros ont été arrêtées.\n\n"+args.Exception.Message,"LunarSync",MessageBoxButton.OK,MessageBoxImage.Error);args.Handled=true;
        };
        try
        {
            var window=new MainWindow();MainWindow=window;
            showWait=ThreadPool.RegisterWaitForSingleObject(showSignal,(_,_)=>Dispatcher.BeginInvoke(window.ShowFromTray),null,Timeout.Infinite,false);
            SessionEnding+=(_,_)=>{ExitRequested=true;window.StopMacros();};
            if(!e.Args.Contains("--startup"))window.Show();
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"LunarSync — démarrage impossible",MessageBoxButton.OK,MessageBoxImage.Error);Shutdown();}
    }
    protected override void OnExit(ExitEventArgs e)
    {
        ExitRequested=true;showWait?.Unregister(null);showSignal?.Dispose();
        if(MainWindow is MainWindow main)main.DisposeResources();
        singleton?.Dispose();base.OnExit(e);
    }
}
