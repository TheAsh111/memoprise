using System;
using System.Linq;
using System.Threading;
using System.Windows;
namespace MemoPrise;
public partial class App : System.Windows.Application
{
 private Mutex? mutex;
 protected override void OnStartup(StartupEventArgs e)
 {
  base.OnStartup(e);
  FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage("fr-FR")));
  bool preview=e.Args.Contains("--screenshot");
  if(preview) {foreach(var filename in new[]{"ui-verification.txt","ui-error.txt"}) System.IO.File.Delete(System.IO.Path.Combine(AppContext.BaseDirectory,filename));}
  mutex=new Mutex(true,"Local\\MemoPrise-"+Environment.UserName+(preview?"-preview-"+Guid.NewGuid():""),out bool first);
  if(!first) { MessageBox.Show("MémoPrise fonctionne déjà. Cliquez sur son icône près de l’horloge."); Shutdown(); return; }
  DispatcherUnhandledException+=(_,args)=> { if(preview) {System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"ui-error.txt"),args.Exception.ToString()); Shutdown(1);} else MessageBox.Show("L’opération n’a pas pu être terminée.\n"+args.Exception.Message,"MémoPrise"); args.Handled=true; };
  var window=new MainWindow(e.Args.Contains("--screenshot")); MainWindow=window;
  if(!e.Args.Contains("--background")) window.Show();
  if(e.Args.Contains("--screenshot")) window.CaptureAndExit();
 }
 protected override void OnExit(ExitEventArgs e) { mutex?.Dispose(); base.OnExit(e); }
}
