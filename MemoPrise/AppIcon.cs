using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MemoPrise;
internal static class AppIcon
{
    static readonly Uri IconUri=new("pack://application:,,,/Assets/MemoPrise.ico",UriKind.Absolute);
    internal static ImageSource WindowIcon {get;}=BitmapFrame.Create(IconUri);
    internal static ImageSource Logo {get;}=new BitmapImage(new Uri("pack://application:,,,/Assets/MemoPrise.png",UriKind.Absolute));
    internal static System.Drawing.Icon CreateTrayIcon()
    {
        using var stream=Application.GetResourceStream(IconUri)!.Stream;
        using var icon=new System.Drawing.Icon(stream,32,32);
        return (System.Drawing.Icon)icon.Clone();
    }
}
