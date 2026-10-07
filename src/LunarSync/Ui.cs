using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Effects;
using LunarSync.Core;

namespace LunarSync;
internal static class Ui
{
    internal static SolidColorBrush Brush(string hex)=>new((Color)ColorConverter.ConvertFromString(hex));
    internal static readonly Brush Cream=Brush("#F4EFE3"),Muted=Brush("#A5A7AE"),Blue=Brush("#1D4168"),Purple=Brush("#A08ADB");
    internal static void Theme(Window window)
    {
        window.Background=Brush("#0D1117");window.Foreground=Cream;window.FontFamily=new FontFamily("Segoe UI Variable Text, Segoe UI");window.FontSize=14;
        window.SourceInitialized+=(_,_)=>{int enabled=1;DwmSetWindowAttribute(new WindowInteropHelper(window).Handle,20,ref enabled,sizeof(int));};
    }
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    internal static TextBlock Text(string text,double size=14,string? color=null,bool bold=false)=>new(){Text=text,FontSize=size,Foreground=color==null?Cream:Brush(color),FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap};
    internal static TextBlock Heading(string text,double size=28){var t=Text(text,size,bold:true);t.FontFamily=new FontFamily("Bahnschrift, Segoe UI");return t;}
    internal static TextBlock Number(string text,double size=32){var t=Text(text,size);t.FontFamily=new FontFamily("Cascadia Mono, Consolas");return t;}
    internal static Button Button(string text,Action action,bool primary=false)
    {var b=new Button{Content=text};if(primary){b.Background=Blue;b.BorderBrush=Brush("#315579");}b.Click+=(_,_)=>action();return b;}
    internal static Border Card(UIElement child,Thickness? padding=null)=>new(){Background=Brush("#1B1E23"),BorderBrush=Brush("#2A3039"),BorderThickness=new(1),CornerRadius=new(14),Padding=padding??new Thickness(22),Child=child};
    internal static Border MacroCard(UIElement child)=>new()
    {
        Background=new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#1A2533"),(Color)ColorConverter.ConvertFromString("#13181F"),70),
        BorderBrush=Brush("#EEECE4"),BorderThickness=new(1),CornerRadius=new(14),Padding=new(21),Child=child,
        Effect=new DropShadowEffect{Color=Colors.White,BlurRadius=12,ShadowDepth=0,Opacity=.19}
    };
    internal static Border Badge(string text,string color="#F4EFE3")=>new(){Background=Brush("#172537"),CornerRadius=new(6),Padding=new(9,6,9,6),Child=Text(text,11,color,true),VerticalAlignment=VerticalAlignment.Center};
    internal static UIElement CompactFlow(MacroDefinition d,MacroConfig c)
    {
        var flow=new WrapPanel();flow.Children.Add(Badge(Keys.Name(c.BaseKey)));
        var arrow=Text(d.Kind==MacroKind.Key?"  ↻  ":"  →  ",18,"#8DA9CA");arrow.VerticalAlignment=VerticalAlignment.Center;flow.Children.Add(arrow);
        flow.Children.Add(Badge(d.Kind==MacroKind.Key?$"{c.Cps:0.##} appuis/s":d.Kind==MacroKind.Hold?"Clic gauche maintenu":"Clic gauche"));return flow;
    }
    internal static Image MacroGlyph(string id,double size=40)
    {
        string shape=id switch
        {
            "hold-mouse4"=>"M 3,6 L 33,6 33,30 3,30 Z M 3,14 L 33,14 M 3,22 L 33,22 M 13,6 L 13,14 M 23,14 L 23,22 M 13,22 L 13,30",
            "hold-mouse5"=>"M 3,30 L 3,23 11,23 11,16 19,16 19,9 27,9 27,3 33,3 M 3,33 L 33,33 33,3",
            "hold-x"=>"M 2,19 L 18,9 34,19 18,29 Z M 10,14 L 26,24 M 26,14 L 10,24",
            "hold-c"=>"M 3,28 L 18,4 33,28 18,34 Z M 18,4 L 18,34 M 3,28 L 33,28",
            "repeat-e"=>"M 18,3 L 18,23 M 10,15 L 18,23 26,15 M 5,23 L 5,32 31,32 31,23",
            "hold-f"=>"M 4,4 L 4,29 11,22 17,34 23,31 17,20 28,20 Z",
            _=>"M 5,14 C 9,1 28,1 32,14 M 32,5 L 32,14 23,14 M 31,23 C 27,36 8,36 4,23 M 4,32 L 4,23 13,23"
        };
        var drawing=new GeometryDrawing(null,new Pen(Brush("#D5E1EF"),1.6){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round},Geometry.Parse(shape));
        return new Image{Source=new DrawingImage(drawing),Width=size,Height=size,Margin=new(12,0,0,0)};
    }
    internal static TextBlock Label(string text)=>new(){Text=text,Foreground=Muted,FontSize=12,Margin=new(0,0,0,8)};
    internal static Image Logo(double size=54)=>new(){Width=size,Height=size,Source=new BitmapImage(new Uri("pack://application:,,,/Assets/logo.png")),Stretch=Stretch.Uniform};
    internal static StackPanel Stack(params UIElement[] items){var p=new StackPanel();foreach(var i in items)p.Children.Add(i);return p;}
    internal static FrameworkElement Space(double height)=>new Border{Height=height};
    internal static DockPanel Row(UIElement left,UIElement right)
    {var d=new DockPanel();DockPanel.SetDock(right,Dock.Right);d.Children.Add(right);d.Children.Add(left);return d;}
    internal static void Alert(Window owner,string message)=>MessageBox.Show(owner,message,"LunarSync",MessageBoxButton.OK,MessageBoxImage.Information);
}
