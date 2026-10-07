using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LunarSync.Core;

namespace LunarSync;
internal sealed class GamePanel : Window
{
    private static readonly System.Windows.Media.Brush EnabledColor=Ui.Brush("#75D69A"),DisabledColor=Ui.Brush("#F08080");
    private readonly AppServices services;
    private readonly Dictionary<string,(TextBlock Status,TextBlock Info)> rows=[];
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(120)};
    private readonly TextBlock profile=Ui.Text("",11,"#A6BDDA",true);
    internal GamePanel(AppServices services)
    {
        Ui.Theme(this);
        this.services=services;Title="LunarSync · En jeu";Width=450;Height=810;MinWidth=380;MinHeight=560;Topmost=services.Settings.PanelTopmost;
        var root=new DockPanel{Margin=new(24)};Content=root;
        var header=Ui.Stack(Ui.Row(Ui.Heading("LUNARSYNC",20),Ui.Logo(40)),Ui.Space(5),Ui.Text("PANNEAU EN JEU",10,"#A5A7AE",true),Ui.Space(8),profile,Ui.Space(18));DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var bottom=new StackPanel();var topmost=new CheckBox{Content="Toujours au premier plan",IsChecked=Topmost};topmost.Checked+=(_,_)=>Topmost=true;topmost.Unchecked+=(_,_)=>Topmost=false;
        bottom.Children.Add(topmost);bottom.Children.Add(Ui.Button("Déplacer sur le deuxième écran",MoveToSecond));bottom.Children.Add(Ui.Space(10));bottom.Children.Add(Ui.Button("Tout désactiver",services.Engine.StopAll));DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
        var list=new StackPanel();root.Children.Add(new ScrollViewer{Content=list});
        foreach(var d in services.Catalog)
        {
            var status=Ui.Text("—",10,"#A5A7AE",true);var info=Ui.Text("—",10,"#A5A7AE");
            var card=Ui.Card(Ui.Stack(Ui.Row(Ui.Text(d.Name,13,bold:true),status),Ui.Space(5),info),new Thickness(12));card.Margin=new(0,0,0,8);list.Children.Add(card);rows[d.Id]=(status,info);
        }
        timer.Tick+=(_,_)=>Refresh();timer.Start();Refresh();
        Loaded+=(_,_)=>
        {
            if(services.Settings.PanelLeft>=0&&services.Settings.PanelTop>=0){Left=services.Settings.PanelLeft;Top=services.Settings.PanelTop;}
            if(Left< SystemParameters.VirtualScreenLeft||Left>SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-100||Top>SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight-100){Left=SystemParameters.VirtualScreenLeft+40;Top=SystemParameters.VirtualScreenTop+40;}
        };
        Closed+=(_,_)=>{timer.Stop();services.Settings.PanelTopmost=Topmost;services.Settings.PanelLeft=Left;services.Settings.PanelTop=Top;services.Save();};
    }
    private void Refresh()
    {
        profile.Text=services.ActiveProfile.Name+" · "+services.ActiveMacros.Count(c=>c.Configured)+" macro(s) configurée(s)";
        foreach(var status in services.Engine.Snapshot())
        {
            var row=rows[status.Id];var c=services.ActiveMacros.Single(c=>c.Id==status.Id);
            row.Status.Text=!status.Configured?"À CONFIGURER":status.Holding?"MAINTENU":status.Enabled?"ACTIVÉE":"DÉSACTIVÉE";
            row.Status.Foreground=!status.Configured?Ui.Muted:status.Enabled?EnabledColor:DisabledColor;
            row.Info.Text=Keys.Name(c.ToggleKey)+" pour activer  ·  "+Keys.Name(c.BaseKey)+(status.Cycles>0?"  ·  "+status.Cycles+" actions":"");
        }
    }
    private void MoveToSecond()
    {
        var screens=System.Windows.Forms.Screen.AllScreens;
        if(screens.Length<2){Ui.Alert(this,"Un seul écran est détecté. Tu peux déplacer ce panneau librement.");return;}
        var target=screens.FirstOrDefault(s=>!s.Primary)??screens[1];
        var source=System.Windows.PresentationSource.FromVisual(this);var matrix=source?.CompositionTarget?.TransformFromDevice??System.Windows.Media.Matrix.Identity;
        var point=matrix.Transform(new Point(target.WorkingArea.Left+30,target.WorkingArea.Top+30));Left=point.X;Top=point.Y;
    }
}
