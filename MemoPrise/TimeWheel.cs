using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace MemoPrise;
public sealed class TimeWheel : Border
{
    readonly TextBlock display;
    readonly int limit;
    int value;
    public event EventHandler? ValueChanged;
    public int Value {get=>value; set {this.value=(value%limit+limit)%limit; display.Text=this.value.ToString("00"); AutomationProperties.SetName(this,$"{Label} : {this.value:00}"); ValueChanged?.Invoke(this,EventArgs.Empty);}}
    public string Label {get;}
    public TimeWheel(string label,int limit,int initial)
    {
        Label=label; this.limit=limit; Width=74; Padding=new Thickness(4); Margin=new Thickness(4); CornerRadius=new CornerRadius(8); Background=Theme.Hero; Focusable=true;
        var p=new StackPanel(); Child=p; p.Children.Add(new TextBlock {Text=label,FontSize=13,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,2)});
        var up=MainWindow.Button("▲",()=>Value++); up.HorizontalAlignment=HorizontalAlignment.Stretch; up.Margin=new Thickness(0); up.Padding=new Thickness(2); up.FontSize=12; up.Height=22; AutomationProperties.SetName(up,"Augmenter les "+label.ToLowerInvariant()); p.Children.Add(up);
        display=new TextBlock {FontSize=26,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,2,0,2)}; p.Children.Add(display);
        var down=MainWindow.Button("▼",()=>Value--); down.HorizontalAlignment=HorizontalAlignment.Stretch; down.Margin=new Thickness(0); down.Padding=new Thickness(2); down.FontSize=12; down.Height=22; AutomationProperties.SetName(down,"Diminuer les "+label.ToLowerInvariant()); p.Children.Add(down);
        Value=initial; AutomationProperties.SetHelpText(this,"Molette de la souris ou flèches haut et bas pour choisir la valeur.");
        MouseWheel+=(_,e)=> {Value+=Math.Sign(e.Delta); e.Handled=true; Focus();};
        KeyDown+=(_,e)=> {if(e.Key==Key.Up || e.Key==Key.Down) {Value+=e.Key==Key.Up?1:-1; e.Handled=true;}};
    }
}
