using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LunarSync.Core;

namespace LunarSync;
internal static class KeyboardPreview
{
    internal static UIElement Build(string layout,string language,List<MacroDefinition> catalog,List<MacroConfig> macros)
    {
        bool full=layout=="Complet",function=layout is "TKL" or "Complet",nav=layout!="60%";
        var canvas=new Canvas{Width=full?1130:nav?900:745,Height=function?344:290};
        var marks=macros.Where(c=>c.Configured).GroupBy(c=>c.ToggleKey).ToDictionary(g=>g.Key,g=>string.Join("\n",g.Select(c=>catalog.Single(d=>d.Id==c.Id).Name)));
        void Key(string label,int vk,double x,double y,double width=43,double height=43)
        {
            var content=new Grid();content.Children.Add(new TextBlock{Text=label,FontSize=label.Length>5?9:12,Foreground=Ui.Cream,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});
            if(marks.TryGetValue(vk,out string? names))
            {content.Children.Add(new Ellipse{Width=6,Height=6,Fill=Ui.Purple,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new(0,5,5,0)});content.ToolTip=names;}
            var cap=new Border{Width=width,Height=height,CornerRadius=new(5),Background=Ui.Brush("#232830"),BorderBrush=Ui.Brush("#39414D"),BorderThickness=new(1),Child=content};Canvas.SetLeft(cap,x);Canvas.SetTop(cap,y);canvas.Children.Add(cap);
        }
        double y=20;
        if(function)
        {Key("Esc",27,20,y);for(int i=0;i<12;i++)Key("F"+(i+1),112+i,109+i*48+(i/4)*10,y);y+=62;}
        string[] top=["²","1","2","3","4","5","6","7","8","9","0","°","+"];
        for(int i=0;i<top.Length;i++)Key(top[i],i is >=1 and <=9?48+i:i==10?48:0,20+i*48,y);
        Key("Retour",8,644,y,82);
        Key("Tab",9,20,y+48,66);
        var letters=language=="AZERTY"?new[]{"AZERTYUIOP","QSDFGHJKLM","WXCVBN"}:new[]{"QWERTYUIOP","ASDFGHJKL","ZXCVBNM"};
        for(int i=0;i<letters[0].Length;i++)Key(letters[0][i].ToString(),letters[0][i],91+i*48,y+48);
        Key("[",0,571,y+48);Key("]",0,619,y+48);Key("Entrée",13,667,y+48,59,91);
        Key("Verr.",20,20,y+96,79);
        for(int i=0;i<letters[1].Length;i++)Key(letters[1][i].ToString(),letters[1][i],104+i*48,y+96);
        Key("ù",0,584,y+96);Key("*",0,632,y+96,30);
        Key("Maj",16,20,y+144,56);Key("<",0,81,y+144);
        for(int i=0;i<letters[2].Length;i++)Key(letters[2][i].ToString(),letters[2][i],129+i*48,y+144);
        double end=129+letters[2].Length*48;
        for(int i=0;i<3;i++)Key(new[]{",",";",":"}[i],0,end+i*48,y+144);
        Key("Maj",16,full||function?614:nav?662:614,y+144,full||function?112:nav?64:112);
        Key("Ctrl",17,20,y+192,58);Key("Win",0,83,y+192,53);Key("Alt",18,141,y+192,54);Key("Espace",32,200,y+192,285);Key("AltGr",18,490,y+192,61);Key("Fn",0,556,y+192,45);Key("Ctrl",17,606,y+192,120);
        if(nav)
        {
            double x=752;
            if(function)
            {
                Key("Inser",45,x,y);Key("Début",36,x+48,y);Key("Pg ↑",33,x+96,y);
                Key("Suppr",46,x,y+48);Key("Fin",35,x+48,y+48);Key("Pg ↓",34,x+96,y+48);
            }
            else{Key("Pg ↑",33,752,y);Key("Pg ↓",34,752,y+48);Key("Suppr",46,752,y+96);}
            Key("↑",38,x+48,y+144);Key("←",37,x,y+192);Key("↓",40,x+48,y+192);Key("→",39,x+96,y+192);
        }
        if(full)
        {
            string[][] pad=[["Num","/","*","−"],["7","8","9","+"],["4","5","6","+"],["1","2","3","↵"],["0","0",".","↵"]];
            for(int row=0;row<5;row++)for(int col=0;col<4;col++)Key(pad[row][col],0,927+col*48,y+row*48);
        }
        return new Viewbox{Child=canvas,Stretch=Stretch.Uniform,MaxHeight=340,HorizontalAlignment=HorizontalAlignment.Stretch};
    }
}
