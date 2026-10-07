using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LunarSync.Core;

namespace LunarSync;
internal sealed class StatisticsView : ScrollViewer
{
    private readonly AppServices services;
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
    private readonly TextBlock keyTotal=Ui.Number("0"),clickTotal=Ui.Number("0"),memory=Ui.Number("0 Mo"),memoryScale=Ui.Text("",11,"#A5A7AE");
    private readonly StackPanel keys=new(),macros=new();
    private readonly Canvas graph=new(){Height=100,ClipToBounds=true};
    private readonly Queue<double> samples=new();
    private bool all=true;
    internal StatisticsView(AppServices services)
    {
        this.services=services;
        var root=new StackPanel();Content=root;
        root.Children.Add(Ui.Heading("Chaque action compte.",34));root.Children.Add(Ui.Space(8));root.Children.Add(Ui.Text("Tes macros en chiffres. Compteurs cumulés, conservés sur ce PC.",14,"#A5A7AE"));root.Children.Add(Ui.Space(22));
        var filter=new ComboBox{ItemsSource=new[]{"Toutes les configurations",services.ActiveProfile.Name},SelectedIndex=0,Width=245};
        filter.SelectionChanged+=(_,_)=>{all=filter.SelectedIndex==0;Refresh();};
        root.Children.Add(Ui.Row(Ui.Badge("●  ACTUALISATION 1 S","#A5C6E7"),filter));root.Children.Add(Ui.Space(20));
        var metrics=new System.Windows.Controls.Primitives.UniformGrid{Columns=3};
        foreach(var pair in new[]{("TOUCHES & BOUTONS",keyTotal),("CLICS GAUCHES",clickTotal),("RAM · LUNARSYNC",memory)})
        {var card=Ui.Card(Ui.Stack(Ui.Label(pair.Item1),Ui.Space(8),pair.Item2),new Thickness(19));card.Margin=new(0,0,12,0);metrics.Children.Add(card);}
        root.Children.Add(metrics);root.Children.Add(Ui.Space(20));
        var ram=Ui.Stack(Ui.Row(Ui.Label("MÉMOIRE PHYSIQUE UTILISÉE"),memoryScale),graph,Ui.Space(8),Ui.Text("Dernières 60 secondes affichées · application et moteur compris · mise à jour tant que cet onglet est ouvert.",11,"#8C94A0"));
        root.Children.Add(Ui.Card(ram,new Thickness(18)));root.Children.Add(Ui.Space(20));
        var columns=new Grid();columns.ColumnDefinitions.Add(new());columns.ColumnDefinitions.Add(new(){Width=new GridLength(18)});columns.ColumnDefinitions.Add(new());
        columns.Children.Add(Ui.Card(Ui.Stack(Ui.Label("RÉPARTITION PAR TOUCHE"),Ui.Space(10),keys)));
        var other=Ui.Card(Ui.Stack(Ui.Label("UTILISATIONS PAR MACRO"),Ui.Space(10),macros));Grid.SetColumn(other,2);columns.Children.Add(other);root.Children.Add(columns);
        root.Children.Add(Ui.Space(20));root.Children.Add(Ui.Card(Ui.Stack(Ui.Label("COMMENT LIRE LES COMPTEURS"),Ui.Text("F maintenu avec Drag macro : +1 F et +1 clic gauche. Relâcher ne rajoute rien. Les frappes ordinaires, les touches d’activation et la répétition automatique du clavier ne sont pas comptées.",12,"#A5A7AE"),Ui.Space(9),Ui.Text("Une utilisation = un maintien déclenché ou un cycle de répétition terminé. Si plusieurs maintiens partagent déjà le clic gauche, seules les pressions réellement envoyées sont ajoutées. Les chiffres mesurent les entrées du moteur, pas les actions acceptées par le jeu.",12,"#A5A7AE")),new Thickness(18)));
        timer.Tick+=(_,_)=>Refresh();Loaded+=(_,_)=>{Refresh();if(IsVisible)timer.Start();};Unloaded+=(_,_)=>timer.Stop();
        IsVisibleChanged+=(_,_)=>{if(IsVisible&&IsLoaded){Refresh();timer.Start();}else timer.Stop();};graph.SizeChanged+=(_,_)=>DrawGraph();
    }
    private void Refresh()
    {
        var counts=services.Statistics.Snapshot(all?null:services.Settings.SelectedProfile);
        keyTotal.Text=counts.Keys.Where(k=>k.Key!=1).Sum(k=>(decimal)k.Value).ToString("N0");clickTotal.Text=counts.Keys.GetValueOrDefault(1).ToString("N0");
        using(var process=Process.GetCurrentProcess())
        {double mb=process.WorkingSet64/1048576d;memory.Text=mb.ToString("0.0")+" Mo";samples.Enqueue(mb);while(samples.Count>60)samples.Dequeue();}
        DrawGraph();keys.Children.Clear();macros.Children.Clear();
        long maximum=counts.Keys.Values.DefaultIfEmpty(1).Max();
        if(counts.Keys.Count==0)keys.Children.Add(Ui.Text("Aucune action pour le moment.\nTes premières macros feront apparaître les barres ici.",13,"#8C94A0"));
        foreach(var entry in counts.Keys.OrderByDescending(k=>k.Value))
        {
            var row=Ui.Stack(Ui.Row(Ui.Text(Keys.Name(entry.Key),13),Ui.Number(entry.Value.ToString("N0"),15)),Ui.Space(7));
            row.Children.Add(new ProgressBar{Minimum=0,Maximum=Math.Max(1,maximum),Value=entry.Value,Height=4,BorderThickness=new(0),Foreground=entry.Key==1?Ui.Purple:Ui.Brush("#759CC5"),Background=Ui.Brush("#293341")});row.Children.Add(Ui.Space(15));keys.Children.Add(row);
        }
        foreach(var d in services.Catalog)
        {var row=Ui.Row(Ui.Text(d.Name,12),Ui.Number(counts.Macros.GetValueOrDefault(d.Id).ToString("N0"),15));row.Margin=new(0,0,0,15);macros.Children.Add(row);}
    }
    private void DrawGraph()
    {
        graph.Children.Clear();double width=graph.ActualWidth;if(width<=0||samples.Count==0)return;
        double ceiling=Math.Max(64,Math.Ceiling(samples.Max()/32)*32);memoryScale.Text="0 — "+ceiling.ToString("0")+" Mo";
        for(int i=0;i<4;i++)graph.Children.Add(new Line{X1=0,X2=width,Y1=i*30+5,Y2=i*30+5,Stroke=Ui.Brush("#2A3543"),StrokeThickness=1});
        var points=new PointCollection();int index=0;
        foreach(double value in samples)points.Add(new Point(index++*width/59,95-Math.Clamp(value/ceiling,0,1)*85));
        if(points.Count==1)points.Add(new Point(Math.Min(width,8),points[0].Y));
        var fill=new PointCollection(points);fill.Insert(0,new Point(0,100));fill.Add(new Point(points.Last().X,100));
        graph.Children.Add(new Polygon{Points=fill,Fill=new LinearGradientBrush(Color.FromArgb(80,114,154,202),Color.FromArgb(0,114,154,202),90)});
        graph.Children.Add(new Polyline{Points=points,Stroke=Ui.Brush("#A7C7EE"),StrokeThickness=2,StrokeLineJoin=PenLineJoin.Round});
    }
}
