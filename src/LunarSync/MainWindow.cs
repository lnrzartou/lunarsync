using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LunarSync.Core;
using Microsoft.Win32;

namespace LunarSync;
public sealed class MainWindow : Window
{
    private readonly AppServices services;
    private readonly UpdateClient updates;
    private readonly ContentControl content=new();
    private readonly TextBlock footer=Ui.Text("Tout est prêt",12,"#A5A7AE");
    private readonly Dictionary<string,Button> nav=[];
    private readonly Dictionary<string,(TextBlock Status,Button Toggle)> macroControls=[];
    private readonly DispatcherTimer statusTimer=new(){Interval=TimeSpan.FromMilliseconds(150)};
    private readonly DispatcherTimer devicesTimer=new(){Interval=TimeSpan.FromSeconds(5)};
    private readonly DispatcherTimer updatesTimer=new(){Interval=TimeSpan.FromHours(6)};
    private List<KeyboardDevice> devices=[];
    private string page="Clavier";
    private bool keyboardReady,updateBusy,disposed,backgroundNotice;
    private readonly System.Windows.Forms.NotifyIcon tray=new();
    private readonly DispatcherTimer saveTimer=new(){Interval=TimeSpan.FromSeconds(30)};
    private GamePanel? panel;
    private TextBlock? updateMessage;
    private string? notifiedVersion;
    public MainWindow()
    {
        Ui.Theme(this);
        Title="LunarSync";Width=1320;Height=900;MinWidth=1040;MinHeight=720;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        services=new();updates=new();
        services.Error+=message=>Dispatcher.BeginInvoke(()=>{services.Engine.SetReady(false);footer.Text=message;});
        var root=new Grid();root.ColumnDefinitions.Add(new(){Width=new GridLength(226)});root.ColumnDefinitions.Add(new());Content=root;
        var sidebar=new DockPanel{Margin=new(20,26,20,20)};
        var sideBackground=new Border{Background=Ui.Brush("#15181D"),BorderBrush=Ui.Brush("#292F38"),BorderThickness=new(0,0,1,0),Child=sidebar};root.Children.Add(sideBackground);
        var brand=new StackPanel{Margin=new(0,0,0,32)};
        brand.Children.Add(Ui.Logo(70));brand.Children.Add(Ui.Space(12));
        var brandText=Ui.Text("LUNARSYNC",19,bold:true);brandText.HorizontalAlignment=HorizontalAlignment.Center;brand.Children.Add(brandText);
        var byline=Ui.Text("TES TOUCHES. TON RYTHME.",9,"#A5A7AE");byline.Margin=new(0,6,0,0);byline.HorizontalAlignment=HorizontalAlignment.Center;brand.Children.Add(byline);DockPanel.SetDock(brand,Dock.Top);sidebar.Children.Add(brand);
        var sideBottom=Ui.Stack(Ui.Text("PAR LUNAR",10,"#A5A7AE",true),Ui.Space(7),Ui.Text("Version "+AppServices.Version,12),Ui.Space(16),Ui.Button("Tout désactiver",StopMacros),Ui.Space(8),Ui.Text("Arrêt rapide : Ctrl + Alt + F12",10,"#A5A7AE"),Ui.Space(12),Ui.Button("Quitter LunarSync",QuitApplication));DockPanel.SetDock(sideBottom,Dock.Bottom);sidebar.Children.Add(sideBottom);
        var menu=new StackPanel();sidebar.Children.Add(menu);
        foreach(var label in new[]{"Clavier","Macros","Statistiques","Panneau en jeu","Nouveautés","Mises à jour","Système"})
        {
            var b=Ui.Button(label,()=>Navigate(label));b.HorizontalContentAlignment=HorizontalAlignment.Left;b.Margin=new(0,0,0,7);b.Padding=new(16,12,16,12);b.BorderThickness=new(0);nav[label]=b;menu.Children.Add(b);
        }
        var body=new DockPanel{Margin=new(32,26,32,20)};Grid.SetColumn(body,1);root.Children.Add(body);
        var top=Ui.Row(Ui.Text("LUNAR  /  CONTROL CENTER",11,"#A5A7AE",true),Ui.Badge("●  MOTEUR LOCAL", "#9ABADA"));top.Margin=new(0,0,0,22);DockPanel.SetDock(top,Dock.Top);body.Children.Add(top);
        footer.Margin=new(0,15,0,0);DockPanel.SetDock(footer,Dock.Bottom);body.Children.Add(footer);body.Children.Add(content);
        RefreshDevices();Navigate("Clavier");
        statusTimer.Tick+=(_,_)=>{if(IsVisible)RefreshStatus();};statusTimer.Start();
        devicesTimer.Tick+=(_,_)=>RefreshDevices();devicesTimer.Start();
        updatesTimer.Tick+=async(_,_)=>await CheckUpdates();updatesTimer.Start();
        Loaded+=(_,_)=>{if(services.LoadWarning!=null){Ui.Alert(this,services.LoadWarning);services.LoadWarning=null;}};
        SystemEvents.SessionSwitch+=SessionSwitch;
        SetupTray();
        if(updates.Configured)Dispatcher.BeginInvoke(async()=>await CheckUpdates());
        try{StartupManager.SetEnabled(services.Settings.StartWithWindows);}catch(Exception ex){footer.Text="Démarrage Windows : "+ex.Message;}
        saveTimer.Tick+=(_,_)=>{try{services.SaveStatistics();}catch(Exception ex){footer.Text="Statistiques : "+ex.Message;}};saveTimer.Start();
        Closing+=(_,e)=>
        {
            if(App.ExitRequested)return;e.Cancel=true;Hide();
            if(!backgroundNotice){tray.ShowBalloonTip(3500,"LunarSync reste actif","Les macros continuent en arrière-plan. Pour arrêter le logiciel, choisis Quitter LunarSync dans son icône.",System.Windows.Forms.ToolTipIcon.Info);backgroundNotice=true;}
        };
    }
    private void SetupTray()
    {
        tray.Icon=System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)??System.Drawing.SystemIcons.Application;tray.Text="LunarSync · macros en arrière-plan";
        var menu=new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Ouvrir LunarSync",null,(_,_)=>Dispatcher.BeginInvoke(ShowFromTray));
        menu.Items.Add("Panneau en jeu",null,(_,_)=>Dispatcher.BeginInvoke(OpenPanel));
        menu.Items.Add("Tout désactiver",null,(_,_)=>Dispatcher.BeginInvoke(StopMacros));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Quitter LunarSync",null,(_,_)=>Dispatcher.BeginInvoke(QuitApplication));
        tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>Dispatcher.BeginInvoke(ShowFromTray);tray.Visible=true;
    }
    internal void ShowFromTray(){Show();WindowState=WindowState.Normal;Activate();}
    internal void QuitApplication(){App.ExitRequested=true;StopMacros();System.Windows.Application.Current.Shutdown();}
    internal void DisposeResources()
    {
        if(disposed)return;disposed=true;SystemEvents.SessionSwitch-=SessionSwitch;statusTimer.Stop();devicesTimer.Stop();updatesTimer.Stop();saveTimer.Stop();
        panel?.Close();tray.Visible=false;tray.Dispose();
        try{services.Dispose();}catch(Exception ex){Debug.WriteLine(ex);}
    }
    private void SessionSwitch(object sender,SessionSwitchEventArgs e)
    {if(e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff)Dispatcher.BeginInvoke(StopMacros);}
    internal void StopMacros(){services.Engine.StopAll();RefreshStatus();}
    private void Navigate(string label)
    {
        page=label;macroControls.Clear();foreach(var item in nav)item.Value.Background=item.Key==label?Ui.Blue:Ui.Brush("#15181D");
        content.Content=label switch{"Clavier"=>KeyboardPage(),"Macros"=>MacrosPage(),"Statistiques"=>new StatisticsView(services),"Panneau en jeu"=>PanelPage(),"Nouveautés"=>NewsPage(),"Système"=>SystemPage(),_=>UpdatesPage()};RefreshStatus();
    }
    private StackPanel Page(string title,string subtitle)
    {
        var p=new StackPanel();p.Children.Add(Ui.Heading(title,34));p.Children.Add(Ui.Space(8));p.Children.Add(Ui.Text(subtitle,14,"#A5A7AE"));p.Children.Add(Ui.Space(24));return p;
    }
    private UIElement ProfileBar()
    {
        var box=new StackPanel();
        box.Children.Add(Ui.Row(Ui.Label("CONFIGURATION ACTIVE"),Ui.Text(services.ActiveProfile.Name+"  /  10",12,"#A6BDDA",true)));
        var slots=new System.Windows.Controls.Primitives.UniformGrid{Columns=10,Margin=new(0,8,0,13)};
        foreach(var profile in services.Settings.Profiles)
        {
            int slot=profile.Slot;bool selected=slot==services.Settings.SelectedProfile;
            var b=Ui.Button(slot.ToString("00"),()=>{if(slot==services.Settings.SelectedProfile)return;services.SwitchProfile(slot);Navigate(page);},selected);
            b.ToolTip=profile.Name+" · "+profile.Macros.Count(c=>c.Configured)+" macro(s) configurée(s)";b.Margin=new(0,0,7,0);b.Padding=new(4,10,4,10);b.FontFamily=new FontFamily("Consolas");
            b.BorderBrush=selected?Ui.Cream:Ui.Brush("#3A4350");b.BorderThickness=new(selected?2:1);slots.Children.Add(b);
        }
        box.Children.Add(slots);
        int configured=services.ActiveMacros.Count(c=>c.Configured);
        var info=Ui.Text(configured==0?"Configuration vide · aucune macro attribuée, toutes les touches restent libres.":$"{configured} / {services.Catalog.Count} macros configurées · changer de configuration désactive tous les modes.",12,"#A5A7AE");
        if(page=="Macros"&&configured>0)
        {
            var clear=Ui.Button("Vider",()=>{if(MessageBox.Show(this,"Retirer les réglages de "+services.ActiveProfile.Name+" ? Les autres configurations restent disponibles.","Vider la configuration",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){services.ClearProfile();Navigate(page);}});
            clear.Padding=new(12,5,12,5);box.Children.Add(Ui.Row(info,clear));
        }
        else box.Children.Add(info);
        return Ui.Card(box,new Thickness(18));
    }
    private UIElement SystemPage()
    {
        var p=Page("Toujours à portée de touche.","Le moteur reste disponible dans la zone de notification Windows.");
        var start=new CheckBox{Content="Lancer LunarSync avec Windows",IsChecked=services.Settings.StartWithWindows,IsEnabled=StartupManager.IsInstalled};
        start.Click+=(_,_)=>
        {
            try{StartupManager.SetEnabled(start.IsChecked==true);services.Settings.StartWithWindows=start.IsChecked==true;services.Save();}
            catch(Exception ex){start.IsChecked=services.Settings.StartWithWindows;Ui.Alert(this,ex.Message);}
        };
        var lifecycle=Ui.Stack(Ui.Label("DISPONIBILITÉ"),start,Ui.Space(12),Ui.Text(StartupManager.IsInstalled?"Au démarrage de Windows, LunarSync rejoint discrètement la zone de notification. Les modes démarrent désactivés.":"Le démarrage Windows devient disponible après l’installation. La version portable continue à fonctionner en arrière-plan pendant cette session.",13,"#A5A7AE"),Ui.Space(22));
        lifecycle.Children.Add(Ui.Row(Ui.Badge("×  FERMER LA FENÊTRE"),Ui.Badge("→  ARRIÈRE-PLAN","#A5C6E7")));lifecycle.Children.Add(Ui.Space(12));
        lifecycle.Children.Add(Ui.Text("Fermer la fenêtre conserve les macros actives. Double-clique sur l’icône près de l’horloge, ou relance LunarSync, pour retrouver ton panneau. « Quitter LunarSync » arrête les macros et termine le logiciel.",13));
        p.Children.Add(Ui.Card(lifecycle));p.Children.Add(Ui.Space(22));
        var engine=Ui.Stack(Ui.Row(Ui.Heading("AutoHotkey, puis LunarSync.",24),Ui.MacroGlyph("repeat-f-click")),Ui.Space(15),Ui.Text("AutoHotkey est un outil d’automatisation Windows : il exécute des scripts qui associent des touches à des actions. Il nous a servi à créer et tester les premières macros.",14),Ui.Space(16),Ui.CompactFlow(new("", "", "",MacroKind.Hold,70,38,35),new(){BaseKey=70}),Ui.Space(16),Ui.Text("LunarSync utilise maintenant son propre moteur natif. Tes amis n’ont besoin ni d’AutoHotkey, ni de scripts à installer. Les profils, les états et les compteurs sont gérés dans cette application.",13,"#A5A7AE"),Ui.Space(12),Ui.Text("Si l’ancien script AutoHotkey fonctionne encore, quitte-le avant d’utiliser les mêmes raccourcis ici, pour éviter les doubles actions.",12,"#C8B893"));
        p.Children.Add(Ui.Card(engine));p.Children.Add(Ui.Space(18));p.Children.Add(Ui.Text("Les préférences et compteurs restent sur ce PC. Aucun texte saisi ni historique de frappe n’est conservé. Le verrouillage de Windows désactive les macros.",12,"#A5A7AE"));
        return new ScrollViewer{Content=p};
    }
    private void RefreshDevices()
    {
        List<KeyboardDevice> found;
        try{found=KeyboardDevices.Get();}catch{found=[];}
        bool changed=string.Join("|",found.Select(d=>d.Id))!=string.Join("|",devices.Select(d=>d.Id));devices=found;
        if(devices.Count==1&&services.Settings.KeyboardId!=devices[0].Id){services.Settings.KeyboardId=devices[0].Id;if(devices[0].SuggestedLayout!=null)services.Settings.Layout=devices[0].SuggestedLayout!;services.Save();changed=true;}
        bool ready=devices.Any(d=>d.Id==services.Settings.KeyboardId);
        if(ready!=keyboardReady){keyboardReady=ready;services.Engine.SetReady(ready);}
        if(changed&&page=="Clavier")Navigate("Clavier");
    }
    private UIElement KeyboardPage()
    {
        var p=Page("Ton clavier, en un regard.","Retrouve tes raccourcis sur un aperçu de ton clavier.");
        p.Children.Add(ProfileBar());p.Children.Add(Ui.Space(20));
        var deviceArea=new StackPanel();
        string count=devices.Count switch{0=>"AUCUN CLAVIER DÉTECTÉ",1=>"1 CLAVIER CONNECTÉ",_=>devices.Count+" CLAVIERS CONNECTÉS"};
        deviceArea.Children.Add(Ui.Label(count));
        if(devices.Count==0)
        {
            deviceArea.Children.Add(Ui.Text("Branche un clavier pour activer tes macros.",19,bold:true));deviceArea.Children.Add(Ui.Space(12));deviceArea.Children.Add(Ui.Button("Actualiser",RefreshDevices));
        }
        else
        {
            var picker=new ComboBox{ItemsSource=devices,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=services.Settings.KeyboardId};
            deviceArea.Children.Add(picker);deviceArea.Children.Add(Ui.Space(9));
            deviceArea.Children.Add(Ui.Text(keyboardReady?"Clavier sélectionné · aperçu uniquement":"Sélectionne un clavier pour continuer. Ce choix est obligatoire.",12,keyboardReady?"#A5A7AE":"#DAC49B"));
            var detail=devices.FirstOrDefault(d=>d.Id==services.Settings.KeyboardId)?.Detail;if(detail!=null)deviceArea.Children.Add(Ui.Text(detail,10,"#7D8591"));
            picker.SelectionChanged+=(_,_)=>
            {
                if(picker.SelectedItem is not KeyboardDevice d)return;services.Engine.StopAll();services.Settings.KeyboardId=d.Id;if(d.SuggestedLayout!=null)services.Settings.Layout=d.SuggestedLayout;services.Save();keyboardReady=true;services.Engine.SetReady(true);Navigate("Clavier");
            };
        }
        p.Children.Add(Ui.Card(deviceArea));p.Children.Add(Ui.Space(20));
        var preview=new StackPanel();
        var selectors=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        var format=new ComboBox{ItemsSource=new[]{"60%","65%","TKL","Complet"},SelectedItem=services.Settings.Layout,Width=110,Margin=new(0,0,10,0)};
        var language=new ComboBox{ItemsSource=new[]{"AZERTY","QWERTY"},SelectedItem=services.Settings.LanguageLayout,Width=120};selectors.Children.Add(format);selectors.Children.Add(language);
        preview.Children.Add(Ui.Row(Ui.Text("APERÇU DU CLAVIER",11,"#A5A7AE",true),selectors));preview.Children.Add(Ui.Space(20));
        preview.Children.Add(KeyboardPreview.Build(services.Settings.Layout,services.Settings.LanguageLayout,services.Catalog,services.ActiveMacros));
        preview.Children.Add(Ui.Space(14));preview.Children.Add(Ui.Text("●  Raccourci attribué à une macro configurée",12,"#A08ADB"));
        preview.Children.Add(Ui.Space(8));preview.Children.Add(Ui.Text("Aperçu schématique. Ajuste le format si ton modèle ne le communique pas à Windows. La sélection sert à l’aperçu ; les raccourcis fonctionnent dans Windows.",11,"#8C94A0"));p.Children.Add(Ui.Card(preview));
        format.SelectionChanged+=(_,_)=>{if(format.SelectedItem is string value){services.Settings.Layout=value;services.Save();Navigate("Clavier");}};
        language.SelectionChanged+=(_,_)=>{if(language.SelectedItem is string value){services.Settings.LanguageLayout=value;services.Save();Navigate("Clavier");}};
        p.Children.Add(Ui.Space(20));p.Children.Add(Ui.Button("Voir les macros",()=>Navigate("Macros"),true));return new ScrollViewer{Content=p};
    }
    private UIElement MacrosPage()
    {
        var p=Page("Tes macros.","Une collection préparée par Lunar. Choisis tes touches, puis active le mode qui te convient.");
        p.Children.Add(ProfileBar());p.Children.Add(Ui.Space(24));
        if(Process.GetProcessesByName("AutoHotkey64").Length>0)
        {p.Children.Add(Ui.Card(Ui.Text("AutoHotkey est ouvert. Ferme l’ancien script avant d’utiliser les mêmes raccourcis dans LunarSync.",12,"#DAC49B"),new Thickness(14)));p.Children.Add(Ui.Space(16));}
        var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new(){Width=new GridLength(18)});grid.ColumnDefinitions.Add(new());
        for(int i=0;i<services.Catalog.Count;i++)
        {
            if(i%2==0)grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
            var d=services.Catalog[i];var c=services.ActiveMacros.Single(c=>c.Id==d.Id);
            var stack=new StackPanel();var status=Ui.Text("NON CONFIGURÉE",10,"#A5A7AE",true);
            stack.Children.Add(Ui.Row(Ui.Text(d.Kind==MacroKind.Hold?"MAINTIEN SYNCHRONISÉ":"SÉQUENCE EN BOUCLE",10,"#A5A7AE",true),status));stack.Children.Add(Ui.Space(16));stack.Children.Add(Ui.Row(Ui.Heading(d.Name,22),Ui.MacroGlyph(d.Id,38)));stack.Children.Add(Ui.Space(10));
            var description=Ui.Text(d.Description,12,"#A5A7AE");description.MinHeight=70;stack.Children.Add(description);stack.Children.Add(Ui.Space(14));
            stack.Children.Add(Ui.CompactFlow(d,c));
            stack.Children.Add(Ui.Space(10));stack.Children.Add(Ui.Text("ON / OFF  ·  "+Keys.Name(c.ToggleKey),11,"#AFA6CC",true));
            stack.Children.Add(Ui.Space(5));stack.Children.Add(Ui.Text(d.Kind==MacroKind.Hold?"Maintenu jusqu’au relâchement":$"{c.Cps:0.##} / s   ·   {1000/c.Cps:0.##} ms / cycle"+(c.Irregular?"   ·   irrégulier":""),11,"#8C94A0"));stack.Children.Add(Ui.Space(18));
            var buttons=new StackPanel{Orientation=Orientation.Horizontal};var configure=Ui.Button(c.Configured?"Configurer":"Configurer",()=>Configure(d));configure.Margin=new(0,0,10,0);buttons.Children.Add(configure);
            var toggle=Ui.Button("Activer",()=>{services.Engine.Toggle(d.Id);RefreshStatus();},true);toggle.IsEnabled=c.Configured&&keyboardReady;buttons.Children.Add(toggle);stack.Children.Add(buttons);
            var card=Ui.MacroCard(stack);card.Margin=new(5,5,5,20);Grid.SetRow(card,i/2);Grid.SetColumn(card,(i%2)*2);grid.Children.Add(card);macroControls[d.Id]=(status,toggle);
        }
        p.Children.Add(grid);return new ScrollViewer{Content=p};
    }
    private void Configure(MacroDefinition d)
    {
        services.Engine.SetReady(false);
        var dialog=new MacroDialog(services,d){Owner=this};dialog.ShowDialog();
        services.Engine.SetReady(keyboardReady);Navigate("Macros");
    }
    private UIElement PanelPage()
    {
        var p=Page("Garde le contrôle.","Un panneau indépendant à placer sur ton deuxième écran.");
        var demo=new StackPanel();demo.Children.Add(Ui.Text("LUNARSYNC  /  EN JEU",11,"#A5A7AE",true));demo.Children.Add(Ui.Space(24));
        foreach(var name in new[]{"Activée","Désactivée","Non configurée"})
        {var row=Ui.Row(Ui.Text("●  "+name,17,name=="Activée"?"#8DB5DA":name=="Désactivée"?"#F4EFE3":"#808792"),Ui.Text(name=="Activée"?"Mode prêt":"—",13,"#A5A7AE"));row.Margin=new(0,0,0,18);demo.Children.Add(row);}
        demo.Children.Add(Ui.Text("Le panneau affiche l’état réel de toutes les macros et les appuis en cours.",13,"#A5A7AE"));p.Children.Add(Ui.Card(demo));p.Children.Add(Ui.Space(24));
        p.Children.Add(Ui.Button("Ouvrir le panneau en jeu",OpenPanel,true));p.Children.Add(Ui.Space(12));p.Children.Add(Ui.Text("Déplace la fenêtre sur l’écran de ton choix. Tu peux aussi la garder au premier plan.",13,"#A5A7AE"));return new ScrollViewer{Content=p};
    }
    private void OpenPanel(){if(panel==null){panel=new GamePanel(services);panel.Closed+=(_,_)=>panel=null;panel.Show();}else{panel.Show();panel.Activate();}}
    private UIElement NewsPage()
    {
        var p=Page("Nouveautés.","L’espace des prochaines annonces de Lunar.");p.Children.Add(new Border{MinHeight=340,CornerRadius=new(14),BorderBrush=Ui.Brush("#2A3039"),BorderThickness=new(1),Background=Ui.Brush("#15181D")});return p;
    }
    private UIElement UpdatesPage()
    {
        var p=Page("Toujours à jour.","Les nouvelles versions se téléchargent, se vérifient, puis redémarrent LunarSync.");
        var card=new StackPanel();card.Children.Add(Ui.Label("VERSION INSTALLÉE"));card.Children.Add(Ui.Text(AppServices.Version.ToString(),36,bold:true));card.Children.Add(Ui.Space(18));
        updateMessage=Ui.Text(updates.Status,17,bold:true);card.Children.Add(updateMessage);card.Children.Add(Ui.Space(12));
        if(!updates.Configured)card.Children.Add(Ui.Text("Lunar n’a pas encore ouvert le canal de publication. Les mises à jour en ligne seront disponibles après sa configuration.",13,"#A5A7AE"));
        if(updates.Available!=null){card.Children.Add(Ui.Text(updates.Available.Notes,13,"#A5A7AE"));card.Children.Add(Ui.Space(15));var install=Ui.Button("Mettre à jour et redémarrer",async()=>await InstallUpdate(),true);install.IsEnabled=!updateBusy;card.Children.Add(install);}
        else{var check=Ui.Button("Rechercher une mise à jour",async()=>await CheckUpdates(),true);check.IsEnabled=updates.Configured&&!updateBusy;card.Children.Add(Ui.Space(15));card.Children.Add(check);}
        p.Children.Add(Ui.Card(card));p.Children.Add(Ui.Space(24));p.Children.Add(Ui.Text("Les mises à jour sont acceptées uniquement après vérification de la signature de publication et de l’intégrité du téléchargement.",12,"#A5A7AE"));return new ScrollViewer{Content=p};
    }
    private async Task CheckUpdates()
    {
        if(updateBusy)return;updateBusy=true;
        try
        {
            await updates.CheckAsync();
            if(updates.Available!=null)
            {
                nav["Mises à jour"].Content="Mises à jour  •";
                if(notifiedVersion!=updates.Available.Version){notifiedVersion=updates.Available.Version;tray.ShowBalloonTip(7000,"LunarSync · mise à jour disponible","La version "+notifiedVersion+" est prête. Ouvre Mises à jour pour l’installer et redémarrer.",System.Windows.Forms.ToolTipIcon.Info);}
            }
        }
        catch(Exception ex){updates.ReportFailure(ex.Message);footer.Text="Mise à jour : "+ex.Message;}
        finally{updateBusy=false;if(page=="Mises à jour")Navigate(page);}
    }
    private async Task InstallUpdate()
    {
        if(updateBusy)return;updateBusy=true;StopMacros();if(updateMessage!=null)updateMessage.Text="Téléchargement et vérification…";
        try{await updates.InstallAsync();QuitApplication();}
        catch(Exception ex){Ui.Alert(this,"La mise à jour n’a pas été installée.\n"+ex.Message);}
        finally{updateBusy=false;}
    }
    private void RefreshStatus()
    {
        var snapshot=services.Engine.Snapshot();
        foreach(var state in snapshot)
        {
            if(!macroControls.TryGetValue(state.Id,out var controls))continue;
            controls.Status.Text=!state.Configured?"NON CONFIGURÉE":state.Holding?"EN MAINTIEN":state.Enabled?"ACTIVÉE":"DÉSACTIVÉE";
            controls.Status.Foreground=state.Enabled?Ui.Brush("#8DB5DA"):Ui.Muted;
            controls.Toggle.Content=state.Enabled?"Désactiver":"Activer";controls.Toggle.IsEnabled=state.Configured&&keyboardReady;
        }
        if(!updateBusy)footer.Text=!keyboardReady?"Sélectionne un clavier pour activer les macros.":$"{snapshot.Count(s=>s.Enabled)} macro(s) activée(s)  ·  {snapshot.Count(s=>s.Configured)} configurée(s)  ·  Ctrl + Alt + F12 pour tout arrêter";
    }
}
