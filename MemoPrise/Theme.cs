using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace MemoPrise;
public static class Theme
{
    public static SolidColorBrush Solid(string color)=>(SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
    public static LinearGradientBrush Gradient(string first,string last)=>new(Solid(first).Color,Solid(last).Color,new Point(0,0),new Point(1,1));
    public static Brush Background=>Gradient("#F0F6FF","#F8FAFF");
    public static Brush Sidebar=>Gradient("#DFEDFF","#E8EDFA");
    public static Brush Hero=>Gradient("#E5F0FF","#EEF1FC");
    public static Brush Accent=>Gradient("#4772AA","#4A73AC");
    public static Brush Text=>Solid("#293F60");
    public static Brush Outline=>Solid("#DEE7F5");
    public static DropShadowEffect Shadow=>new() {Color=Solid("#7897C0").Color,BlurRadius=14,ShadowDepth=2,Opacity=.08};
}
