using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using LunarSync.Core;

namespace LunarSync;
internal sealed class MacroDialog : Window
{
    internal MacroConfig? Result;
    private readonly AppServices services;
    private readonly List<KeyValuePair<int,string>> keyOptions=Keys.Names.OrderBy(k=>k.Key<=6?1:0).ThenBy(k=>k.Value,StringComparer.CurrentCulture).ToList();
    private ComboBox? captureTarget;
    internal MacroDialog(AppServices services,MacroDefinition definition)
    {
        Ui.Theme(this);this.services=services;
        Title="Configurer · "+definition.Name;Width=1020;Height=Math.Min(890,SystemParameters.WorkArea.Height-40);MinWidth=880;MinHeight=640;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var config=services.ActiveMacros.Single(c=>c.Id==definition.Id).Copy();
        var dock=new DockPanel();Content=dock;
        var footer=new Border{Padding=new(28,16,28,22),BorderBrush=Ui.Brush("#34404E"),BorderThickness=new(0,1,0,0)};DockPanel.SetDock(footer,Dock.Bottom);dock.Children.Add(footer);
        var root=new StackPanel{Margin=new(28)};dock.Children.Add(new ScrollViewer{Content=root});
        root.Children.Add(Ui.Row(Ui.Badge(services.ActiveProfile.Name.ToUpperInvariant()),Ui.MacroGlyph(definition.Id,36)));root.Children.Add(Ui.Space(16));
        root.Children.Add(Ui.Heading(definition.Name,32));root.Children.Add(Ui.Space(10));root.Children.Add(Ui.Text(definition.Description,13,"#A5A7AE"));root.Children.Add(Ui.Space(24));
        var columns=new Grid();columns.ColumnDefinitions.Add(new());columns.ColumnDefinitions.Add(new(){Width=new GridLength(24)});columns.ColumnDefinitions.Add(new());
        var fields=new StackPanel();columns.Children.Add(fields);var explain=new StackPanel();Grid.SetColumn(explain,2);columns.Children.Add(explain);root.Children.Add(columns);
        ComboBox Selector(int selected)=>new(){ItemsSource=keyOptions,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=selected,MinWidth=235,Margin=new(0,0,8,0)};
        var baseKey=Selector(config.BaseKey);var toggleKey=Selector(config.ToggleKey);
        fields.Children.Add(Ui.Label(definition.Kind==MacroKind.Hold?"TOUCHE OU BOUTON À MAINTENIR":"TOUCHE EFFECTUÉE PAR LA MACRO"));
        fields.Children.Add(Ui.Row(baseKey,Ui.Button("Capturer",()=>Capture(baseKey))));fields.Children.Add(Ui.Space(18));
        fields.Children.Add(Ui.Label("ACTIVATION / DÉSACTIVATION"));
        fields.Children.Add(Ui.Row(toggleKey,Ui.Button("Capturer",()=>Capture(toggleKey))));fields.Children.Add(Ui.Space(12));
        fields.Children.Add(Ui.Text("Un appui active le mode, le suivant le désactive. Un raccourci partagé commande toutes les macros qui lui sont attribuées dans cette configuration.",12,"#A5A7AE"));fields.Children.Add(Ui.Space(22));
        var gap=new TextBox{Text=config.GapMs.ToString(CultureInfo.InvariantCulture)};
        var press=new TextBox{Text=config.PressMs.ToString(CultureInfo.InvariantCulture)};
        var cps=new TextBox{Text=config.Cps.ToString("0.##",CultureInfo.InvariantCulture)};
        var period=new TextBox{Text=(1000/config.Cps).ToString("0.##",CultureInfo.InvariantCulture)};
        var irregular=new CheckBox{Content="Cadence irrégulière",IsChecked=config.Irregular};
        var variation=new TextBox{Text=config.VariationPercent.ToString(CultureInfo.InvariantCulture),IsEnabled=config.Irregular};
        bool syncing=false;
        double Number(TextBox box)=>double.Parse(box.Text.Replace(',','.'),CultureInfo.InvariantCulture);
        int Integer(TextBox box)=>int.Parse(box.Text,CultureInfo.InvariantCulture);
        cps.TextChanged+=(_,_)=>{if(syncing)return;if(double.TryParse(cps.Text.Replace(',','.'),CultureInfo.InvariantCulture,out var n)&&n>0){syncing=true;period.Text=(1000/n).ToString("0.##",CultureInfo.InvariantCulture);syncing=false;}};
        period.TextChanged+=(_,_)=>{if(syncing)return;if(double.TryParse(period.Text.Replace(',','.'),CultureInfo.InvariantCulture,out var n)&&n>0){syncing=true;cps.Text=(1000/n).ToString("0.###",CultureInfo.InvariantCulture);syncing=false;}};
        void Field(string title,UIElement field){fields.Children.Add(Ui.Label(title));fields.Children.Add(field);fields.Children.Add(Ui.Space(14));}
        if(definition.Kind!=MacroKind.Hold)
        {
            var pair=new Grid();pair.ColumnDefinitions.Add(new());pair.ColumnDefinitions.Add(new(){Width=new GridLength(14)});pair.ColumnDefinitions.Add(new());
            pair.Children.Add(Ui.Stack(Ui.Label(definition.Kind==MacroKind.Key?"APPUIS / SECONDE":"CYCLES / SECONDE · CPS"),cps));
            var periodField=Ui.Stack(Ui.Label("PÉRIODE · MS"),period);Grid.SetColumn(periodField,2);pair.Children.Add(periodField);fields.Children.Add(pair);fields.Children.Add(Ui.Space(16));
            Field("DURÉE DE CHAQUE APPUI · MS",press);
            if(definition.Kind==MacroKind.KeyAndClick)Field("TOUCHE → CLIC : PAUSE · MS",gap);
            fields.Children.Add(irregular);Field("VARIATION DES INTERVALLES · %",variation);
            irregular.Checked+=(_,_)=>variation.IsEnabled=true;irregular.Unchecked+=(_,_)=>variation.IsEnabled=false;
            fields.Children.Add(Ui.Text("Les intervalles varient autour de la période moyenne. Les CPS réels dépendent de Windows et de l’application cible.",11,"#8C94A0"));
        }
        else
        {
            Field("DÉLAI AVANT LE CLIC GAUCHE · MS",gap);
            fields.Children.Add(Ui.Text("Pas de CPS dans ce mode : le clic reste enfoncé. Il n’est pas répété tant que tu maintiens la touche.",12,"#A5A7AE"));
        }
        var flow=new StackPanel();
        var flowCard=Ui.MacroCard(flow);flowCard.Margin=new(5);explain.Children.Add(flowCard);explain.Children.Add(Ui.Space(20));
        void RefreshFlow()
        {
            if(baseKey.SelectedValue is not int key||toggleKey.SelectedValue is not int toggle)return;
            flow.Children.Clear();flow.Children.Add(Ui.Label("PARCOURS DES ACTIONS"));flow.Children.Add(Ui.Text(Keys.Name(toggle)+"  ·  mode ON / OFF",12,"#AFA6CC",true));flow.Children.Add(Ui.Space(16));
            void Step(string number,string title,string detail,bool last=false)
            {
                var badge=Ui.Badge(number,"#A5C6E7");badge.Margin=new(0,0,13,0);badge.VerticalAlignment=VerticalAlignment.Top;
                var row=new DockPanel();DockPanel.SetDock(badge,Dock.Left);row.Children.Add(badge);row.Children.Add(Ui.Stack(Ui.Text(title,14,bold:true),Ui.Space(4),Ui.Text(detail,11,"#A5A7AE")));flow.Children.Add(row);
                if(!last){var connector=Ui.Text("│",18,"#6483A8");connector.Margin=new(14,3,0,3);flow.Children.Add(connector);}
            }
            string keyName=Keys.Name(key);
            if(definition.Kind==MacroKind.Hold)
            {
                Step("01",keyName+" enfoncé","Ton appui d’origine est conservé.");
                Step("02","Clic gauche maintenu","Après "+gap.Text+" ms, tant que "+keyName+" reste enfoncé.");
                Step("03",keyName+" relâché → clic relâché","Le clic cesse quand le dernier déclencheur actif est relâché.",true);
            }
            else
            {
                Step("01",keyName+" ↓ puis ↑","Appui de "+press.Text+" ms.");
                if(definition.Kind==MacroKind.KeyAndClick)Step("02","Clic gauche ↓ puis ↑","Après "+gap.Text+" ms de pause · appui de "+press.Text+" ms.");
                Step("↻","Attente → cycle suivant",period.Text+" ms par cycle en moyenne · arrêt avec "+Keys.Name(toggle)+".",true);
            }
            if(definition.Id is "hold-f" or "repeat-f-click"){flow.Children.Add(Ui.Space(18));flow.Children.Add(Ui.Text("Confirmation de l’édition : assurée par le jeu si son option de confirmation au relâchement est activée. Le moteur ne reconnaît pas la forme de l’édition.",11,"#C8B893"));}
        }
        baseKey.SelectionChanged+=(_,_)=>RefreshFlow();toggleKey.SelectionChanged+=(_,_)=>RefreshFlow();
        gap.TextChanged+=(_,_)=>RefreshFlow();press.TextChanged+=(_,_)=>RefreshFlow();period.TextChanged+=(_,_)=>RefreshFlow();RefreshFlow();
        string recommendation=definition.Kind==MacroKind.Hold?"3 ms avant le clic":definition.DefaultCps.ToString("0")+" CPS · "+(1000/definition.DefaultCps).ToString("0.##")+" ms/cycle";
        var advice=Ui.Stack(Ui.Label("RÉGLAGES CONSEILLÉS"),Ui.Heading(recommendation,20),Ui.Space(10));
        advice.Children.Add(Ui.Text(definition.Kind==MacroKind.Hold?"Un délai court pour une réponse rapide, avec un clic continu qui suit précisément ton maintien.":"Appuis de 3 ms"+(definition.Kind==MacroKind.KeyAndClick?" et pause de 3 ms avant le clic.":".")+" Cette cadence garde une marge entre les actions. Augmenter les CPS peut provoquer des entrées ignorées ou une file d’attente dans l’application cible.",12,"#A5A7AE"));
        advice.Children.Add(Ui.Space(12));advice.Children.Add(Ui.Button("Rétablir les délais conseillés",()=>{cps.Text=definition.DefaultCps.ToString(CultureInfo.InvariantCulture);press.Text="3";gap.Text="3";variation.Text="15";irregular.IsChecked=false;}));
        advice.Children.Add(Ui.Space(12));advice.Children.Add(Ui.Text("Repères issus de nos réglages précédents, à vérifier dans ton environnement. Les millisecondes sont une cible de planification, pas une garantie de temps réel.",11,"#8C94A0"));explain.Children.Add(Ui.Card(advice,new Thickness(19)));
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        var cancel=Ui.Button("Annuler",Close);cancel.Margin=new(0,0,10,0);buttons.Children.Add(cancel);
        buttons.Children.Add(Ui.Button("Enregistrer",()=>
        {
            try
            {
                config.BaseKey=(int)baseKey.SelectedValue;config.ToggleKey=(int)toggleKey.SelectedValue;
                config.Cps=Number(cps);config.PressMs=Integer(press);config.GapMs=Integer(gap);config.Irregular=irregular.IsChecked==true;config.VariationPercent=Integer(variation);config.Configured=true;
                services.SaveMacro(config);Result=config;DialogResult=true;
            }
            catch(Exception ex){Ui.Alert(this,ex is FormatException?"Entre des nombres valides. Les délais en ms et la variation doivent être des entiers.":ex.Message);}
        },true));
        if(config.Configured)
        {
            var remove=Ui.Button("Retirer de cette configuration",()=>{config.Configured=false;services.SaveMacro(config);DialogResult=true;});
            footer.Child=Ui.Row(remove,buttons);
        }
        else footer.Child=buttons;
        services.Input.Captured=key=>Dispatcher.BeginInvoke(()=>{if(captureTarget!=null)captureTarget.SelectedValue=key;captureTarget=null;Title="Configurer · "+definition.Name;});
        Closed+=(_,_)=>{services.Input.CaptureMode=false;services.Input.Captured=null;};
    }
    private void Capture(ComboBox target){captureTarget=target;services.Input.CaptureMode=true;Title="Appuie sur une touche ou un bouton latéral…";}
}
