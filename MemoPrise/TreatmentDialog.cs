using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using System.Diagnostics;
namespace MemoPrise;
public sealed class TreatmentDialog : Window
{
    public Treatment? Result {get;private set;}
    readonly Treatment? original;
    readonly StackPanel content=new() {Margin=new Thickness(26,20,26,20),MaxWidth=680};
    readonly StackPanel identity=new();
    readonly StackPanel calendar=new();
    readonly List<DosePage> doses=new();
    readonly RadioButton fixedMode=new() {Content="Elle reste la même",IsChecked=true,GroupName="posologie",FontSize=18,Margin=new Thickness(0,5,0,10)};
    readonly RadioButton changingMode=new() {Content="Elle change à une ou plusieurs dates",GroupName="posologie",FontSize=18,Margin=new Thickness(0,5,0,16)};
    readonly List<PeriodEditor> periods=new();
    readonly StackPanel periodPanel=new();
    readonly ScrollViewer scroller=new() {VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    readonly DispatcherTimer advanceTimer=new() {Interval=TimeSpan.FromMilliseconds(1000)};
    PeriodEditor? advanceTarget;
    readonly DispatcherTimer slideTimer=new() {Interval=TimeSpan.FromMilliseconds(16)};
    readonly Stopwatch slideClock=new();
    double slideFrom,slideTo;
    bool syncingPeriods;
    readonly TextBox name=new();
    readonly ComboBox count=new() {FontSize=20,Padding=new Thickness(8),Margin=new Thickness(0,6,0,18),Width=110,HorizontalAlignment=HorizontalAlignment.Left};
    readonly CheckBox[] days=new CheckBox[7];
    readonly DatePicker start=new() {FontSize=18,Margin=new Thickness(0,0,0,18)};
    readonly DatePicker end=new() {FontSize=18,Margin=new Thickness(0,0,0,18)};
    readonly StackPanel endField=new();
    readonly TextBox note=new() {AcceptsReturn=true,Height=70};
    readonly TextBlock progress=MainWindow.Text("",16);
    readonly ProgressBar stepProgress=new() {Height=5,Foreground=Theme.Solid("#628CBF"),Background=Theme.Solid("#D6E5F7"),BorderThickness=new Thickness(0),Margin=new Thickness(0,0,0,6)};
    readonly TextBlock error=MainWindow.Text("",16);
    readonly Button back;
    readonly Button next;
    int step;
    int Count=>(int)(count.SelectedItem??1);
    bool Changes=>changingMode.IsChecked==true;
    int SummaryStep=>Count+(Changes?3:2);
    public TreatmentDialog(Treatment? t)
    {
        original=t; Icon=AppIcon.WindowIcon; Title=t==null?"Ajouter un médicament":"Modifier le traitement"; Width=640; Height=760; MinWidth=540; MinHeight=520;
        WindowStartupLocation=WindowStartupLocation.CenterOwner; FontFamily=new FontFamily("Segoe UI"); Background=Theme.Background; Foreground=Theme.Text;
        UseLayoutRounding=true;
        var root=new DockPanel(); var header=new StackPanel {Margin=new Thickness(26,22,26,0),MaxWidth=680};
        header.Children.Add(MainWindow.Text(Title,26,true)); header.Children.Add(progress); header.Children.Add(stepProgress); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
        var footer=new StackPanel {Margin=new Thickness(26,10,26,18),MaxWidth=680}; DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
        error.Foreground=Brushes.Firebrick; AutomationProperties.SetAutomationId(error,"wizard-error"); footer.Children.Add(error);
        var buttons=new WrapPanel(); back=MainWindow.Button("Précédent",Previous); next=MainWindow.Primary(MainWindow.Button("Suivant",Next)); next.IsDefault=true; buttons.Children.Add(back); buttons.Children.Add(next); var cancel=MainWindow.Button("Annuler",()=>Close()); cancel.IsCancel=true; cancel.Background=Brushes.White; buttons.Children.Add(cancel); footer.Children.Add(buttons);
        AutomationProperties.SetAutomationId(next,"wizard-next"); AutomationProperties.SetAutomationId(back,"wizard-back");
        scroller.Content=content; AutomationProperties.SetAutomationId(scroller,"wizard-scroll"); root.Children.Add(scroller); Content=root;
        advanceTimer.Tick+=(_,_)=> {advanceTimer.Stop(); RevealPrises();};
        slideTimer.Tick+=(_,_)=> {
            double progress=Math.Min(1,slideClock.Elapsed.TotalSeconds/1.2);
            double eased=progress<0.5?4*progress*progress*progress:1-Math.Pow(-2*progress+2,3)/2;
            scroller.ScrollToVerticalOffset(slideFrom+(slideTo-slideFrom)*eased);
            if(progress>=1) StopSlide();
        };
        scroller.PreviewMouseWheel+=(_,_)=> {StopSlide(); advanceTimer.Stop(); advanceTarget=null;};
        Closed+=(_,_)=> {advanceTimer.Stop(); StopSlide();};
        // Clicking a label or empty area does not make a WPF TextBox lose focus.
        // Commit the duration on any click outside the field, even if focus stays there.
        AddHandler(Mouse.PreviewMouseDownEvent,new MouseButtonEventHandler((_,e)=> {
            if(e.ChangedButton!=MouseButton.Left) return;
            var source=periods.FirstOrDefault(p=>p.Duration.IsKeyboardFocusWithin);
            if(source==null) return;
            var node=e.OriginalSource as DependencyObject;
            while(node!=null) {if(node==source.Duration) return; node=node is Visual || node is System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);}
            QueueAdvance(source,true,true);
        }),true);
        name.Text=t?.Name??""; AutomationProperties.SetAutomationId(name,"medicine-name"); Field(identity,"Nom du médicament",name);
        identity.Children.Add(MainWindow.Text("Combien de prises par jour ?",21,true));
        foreach(var n in Enumerable.Range(1,24)) count.Items.Add(n);
        var entries=t==null?new List<DailyDose>():Schedule.Doses(t); count.SelectedItem=Math.Max(1,entries.Count);
        AutomationProperties.SetAutomationId(count,"intake-count"); identity.Children.Add(count);
        identity.Children.Add(MainWindow.Text("Vous choisirez l’heure et la quantité de chaque prise, une à une.",18));
        identity.Children.Add(MainWindow.Text("La posologie reste-t-elle la même ?",21,true)); identity.Children.Add(fixedMode); identity.Children.Add(changingMode);
        AutomationProperties.SetAutomationId(fixedMode,"fixed-mode"); AutomationProperties.SetAutomationId(changingMode,"changing-mode");
        changingMode.IsChecked=t?.Periods!=null;
        if(t?.Periods!=null) foreach(var period in t.Periods) CreatePeriod(period);
        for(int n=0;n<entries.Count;n++) AddDose(entries[n]);
        calendar.Children.Add(MainWindow.Text("Quels jours prendre ce médicament ?",23,true)); var dayPanel=new WrapPanel();
        foreach(var n in new[]{1,2,3,4,5,6,0}) {days[n]=new CheckBox {Content=CultureInfo.GetCultureInfo("fr-FR").DateTimeFormat.DayNames[n],IsChecked=((t?.Days??127)&(1<<n))!=0}; dayPanel.Children.Add(days[n]);} calendar.Children.Add(dayPanel);
        start.SelectedDate=t?.Start??DateTime.Today; end.SelectedDate=t?.End; note.Text=t?.Note??"";
        Field(calendar,"Date de début",start); Field(endField,"Date de fin (facultative)",end); calendar.Children.Add(endField); Field(calendar,"Consigne (facultative)",note);
        calendar.Children.Add(MainWindow.Text("Pour un traitement par périodes, la fin du traitement se règle uniquement dans la dernière période.",16));
        AutomationProperties.SetAutomationId(end,"treatment-end");
        calendar.Children.Add(MainWindow.Text("Une modification s’applique aux prochaines prises. L’historique reste conservé.",16));
        name.TextChanged+=(_,_)=>error.Text="";
        count.SelectionChanged+=(_,_)=> {error.Text=""; if(step==0) progress.Text=$"Étape 1 sur {SummaryStep+1}";};
        fixedMode.Checked+=(_,_)=> {endField.Visibility=Visibility.Visible; if(step==0) progress.Text=$"Étape 1 sur {SummaryStep+1}";};
        changingMode.Checked+=(_,_)=> {endField.Visibility=Visibility.Collapsed; if(step==0) progress.Text=$"Étape 1 sur {SummaryStep+1}";}; endField.Visibility=Changes?Visibility.Collapsed:Visibility.Visible;
        Render();
    }
    static void Field(StackPanel panel,string label,UIElement field) {panel.Children.Add(MainWindow.Text(label,17,true)); panel.Children.Add(field);}
    void AddDose(DailyDose? entry=null)
    {
        int n=doses.Count; int hour=entry==null?(n==0?8:n==1?12:n==2?20:(8+n)%24):int.Parse(entry.Time.Split(':')[0]); int minute=entry==null?0:int.Parse(entry.Time.Split(':')[1]);
        var panel=new StackPanel(); panel.Children.Add(MainWindow.Text("À quelle heure ?",23,true));
        var wheels=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,4,0,10)};
        var hours=new TimeWheel("Heures",24,hour); var minutes=new TimeWheel("Minutes",60,minute); wheels.Children.Add(hours); wheels.Children.Add(new TextBlock {Text=":",FontSize=26,VerticalAlignment=VerticalAlignment.Center}); wheels.Children.Add(minutes); panel.Children.Add(wheels);
        panel.Children.Add(MainWindow.Text("Utilisez les flèches ou la molette pour choisir l’heure.",16));
        var quantity=new TextBox {Text=entry?.Dose??"",Width=220,HorizontalAlignment=HorizontalAlignment.Left}; Field(panel,"Quantité pour cette prise (ex. 1 cachet, 2 cachets)",quantity);
        quantity.TextChanged+=(_,_)=>error.Text=""; hours.ValueChanged+=(_,_)=>error.Text=""; minutes.ValueChanged+=(_,_)=>error.Text="";
        panel.Children.Add(MainWindow.Text("La quantité peut être différente à chaque horaire.",17));
        AutomationProperties.SetAutomationId(hours,$"hours-{n+1}"); AutomationProperties.SetAutomationId(minutes,$"minutes-{n+1}"); AutomationProperties.SetAutomationId(quantity,$"dose-{n+1}");
        doses.Add(new DosePage(panel,hours,minutes,quantity));
    }
    void EnsureDoses() {while(doses.Count<Count) AddDose();}
    List<DailyDose> Entries() => doses.Take(Count).Select(d=>new DailyDose($"{d.Hours.Value:00}:{d.Minutes.Value:00}",d.Quantity.Text.Trim())).ToList();
    void Render()
    {
        advanceTimer.Stop(); advanceTarget=null; StopSlide();
        content.Children.Clear(); error.Text=""; back.IsEnabled=step>0; next.Content=step==SummaryStep?"Enregistrer":"Suivant";
        int total=SummaryStep+1; progress.Text=$"Étape {step+1} sur {total}";
        stepProgress.Maximum=total; stepProgress.Value=step+1;
        if(step==0) {content.Children.Add(MainWindow.Text("Le médicament",25,true)); content.Children.Add(identity);}
        else if(step<=Count) {EnsureDoses(); content.Children.Add(MainWindow.Text($"Prise {step} sur {Count}",25,true)); content.Children.Add(doses[step-1].Panel);}
        else if(step==Count+1) content.Children.Add(calendar);
        else if(Changes && step==Count+2)
        {
            EnsurePeriods(); content.Children.Add(MainWindow.Text("Quand la posologie change-t-elle ?",23,true)); content.Children.Add(MainWindow.Text("Indiquez la durée de chaque période. La suivante commence le lendemain. Vous pouvez aussi choisir directement sa date de début.",16)); content.Children.Add(periodPanel); RebuildPeriodPanel();
        }
        else
        {
            content.Children.Add(MainWindow.Text("Vérifiez votre programme",25,true)); content.Children.Add(MainWindow.Text(name.Text.Trim(),23,true));
            if(Changes) foreach(var period in GetPeriods()) {content.Children.Add(MainWindow.Text($"Du {period.Start:dd/MM/yyyy}"+(period.End==null?" · puis en continu":$" au {period.End:dd/MM/yyyy}"),19,true)); foreach(var entry in period.Prises) content.Children.Add(MainWindow.Text($"{entry.Time} → {entry.Dose}",20));}
            else foreach(var entry in Schedule.ValidateDoses(Entries())) content.Children.Add(MainWindow.Text($"{entry.Time}  →  {entry.Dose}",22,true));
            int mask=DayMask(); string dayNames=mask==127?"Tous les jours":string.Join(", ",Enumerable.Range(0,7).Where(n=>(mask&(1<<n))!=0).Select(n=>CultureInfo.GetCultureInfo("fr-FR").DateTimeFormat.DayNames[n]));
            content.Children.Add(MainWindow.Text(dayNames,18)); if(!Changes) content.Children.Add(MainWindow.Text($"À partir du {start.SelectedDate:dd/MM/yyyy}"+(end.SelectedDate==null?" · sans date de fin":$"\nJusqu’au {end.SelectedDate:dd/MM/yyyy}"),18));
            if(!string.IsNullOrWhiteSpace(note.Text)) content.Children.Add(MainWindow.Text(note.Text.Trim(),18));
            content.Children.Add(MainWindow.Text("Si tout est correct, cliquez sur Enregistrer. Pour corriger une prise, utilisez Précédent.",18));
        }
    }
    int DayMask()=>Enumerable.Range(0,7).Where(n=>days[n].IsChecked==true).Sum(n=>1<<n);
    void Previous() {if(step>0) {step--; Render();}}
    void Next()
    {
        try
        {
            if(step==0) {if(string.IsNullOrWhiteSpace(name.Text)) throw new Exception("Indiquez le nom du médicament."); EnsureDoses();}
            else if(step<=Count) {if(string.IsNullOrWhiteSpace(doses[step-1].Quantity.Text)) throw new Exception($"Indiquez la quantité pour la prise {step}."); if(step==Count) Schedule.ValidateDoses(Entries());}
            else if(step==Count+1) {if(DayMask()==0) throw new Exception("Choisissez au moins un jour de prise."); if(start.SelectedDate==null) throw new Exception("Choisissez une date de début."); if(!Changes && end.SelectedDate<start.SelectedDate) throw new Exception("La date de fin doit suivre la date de début.");}
            else if(Changes && step==Count+2) {var periodEntries=GetPeriods(); Schedule.ValidatePeriods(CreateResult(periodEntries));}
            else
            {
                Result=CreateResult(Changes?GetPeriods():null); Schedule.ValidatePeriods(Result);
                // Works for both ShowDialog() and the isolated GUI verification window.
                try {DialogResult=true;} catch(InvalidOperationException) {Close();}
                return;
            }
            step++; Render();
        }
        catch(Exception ex) {error.Text=ex.Message;}
    }
    sealed record DosePage(StackPanel Panel,TimeWheel Hours,TimeWheel Minutes,TextBox Quantity);
    Treatment CreateResult(List<TreatmentPeriod>? list)
    {
        var entries=list==null?Schedule.ValidateDoses(Entries()):list[0].Prises;
        return new Treatment(original?.Id??Guid.NewGuid().ToString("N"),name.Text.Trim(),entries[0].Dose,string.Join(";",entries.Select(p=>p.Time)),DayMask(),start.SelectedDate!.Value.Date,list==null?end.SelectedDate?.Date:list[^1].End,note.Text.Trim(),original?.Active??true,entries,list);
    }
    void CreatePeriod(TreatmentPeriod period)
    {
        var editor=new PeriodEditor(period,periods.Count==0); periods.Add(editor);
        editor.AdvanceRequested+=(immediate,nextPeriod)=>QueueAdvance(editor,immediate,nextPeriod);
        editor.DatesChanged+=()=> {error.Text=""; ReflowDates();};
        editor.StartChanged+=date=> {if(syncingPeriods) return; int index=periods.IndexOf(editor); if(index>0) {int days=(date-periods[index-1].StartPicker.SelectedDate!.Value.Date).Days; if(days<1) {error.Text="La nouvelle période doit commencer après la précédente."; return;} syncingPeriods=true; periods[index-1].Unlimited.IsChecked=false; periods[index-1].Duration.Text=days.ToString(CultureInfo.InvariantCulture); syncingPeriods=false;} ReflowDates();};
    }
    void EnsurePeriods()
    {
        if(periods.Count==0) {CreatePeriod(new TreatmentPeriod(start.SelectedDate!.Value.Date,start.SelectedDate.Value.Date.AddDays(13),Schedule.ValidateDoses(Entries()))); AddPeriod(false);}
        periods[0].SetFixedEntries(Schedule.ValidateDoses(Entries())); ReflowDates();
    }
    void ReflowDates()
    {
        if(syncingPeriods || periods.Count==0) return; syncingPeriods=true;
        try {periods[0].StartPicker.SelectedDate=start.SelectedDate; for(int n=0;n<periods.Count;n++) {periods[n].IsLast=n==periods.Count-1; if(n>0 && periods[n-1].TryEnd(out var last) && last!=null && last.Value<DateTime.MaxValue.Date) periods[n].StartPicker.SelectedDate=last.Value.AddDays(1);}}
        finally {syncingPeriods=false;}
    }
    void AddPeriod(bool render=true)
    {
        try {var prior=periods[^1]; if(prior.Unlimited.IsChecked==true) prior.Unlimited.IsChecked=false; var previous=prior.GetPeriod(); CreatePeriod(new TreatmentPeriod(previous.End!.Value.AddDays(1),null,previous.Prises.ToList())); ReflowDates(); if(render) {RebuildPeriodPanel(); QueueAdvance(periods[^1],true);}}
        catch(Exception ex) {error.Text=ex.Message;}
    }
    void RebuildPeriodPanel()
    {
        periodPanel.Children.Clear(); for(int n=0;n<periods.Count;n++) {var editor=periods[n]; editor.Number(n+1); periodPanel.Children.Add(editor); if(n>0) {var remove=MainWindow.Button("Supprimer cette période",()=> {periods.Remove(editor); ReflowDates(); RebuildPeriodPanel();}); System.Windows.Automation.AutomationProperties.SetAutomationId(remove,$"remove-period-{n+1}"); periodPanel.Children.Add(remove);}}
        var add=MainWindow.Button("+ Ajouter une autre période",()=>AddPeriod()); AutomationProperties.SetAutomationId(add,"add-period"); periodPanel.Children.Add(add);
    }
    List<TreatmentPeriod> GetPeriods()=>periods.Select(p=>p.GetPeriod()).ToList();
    void QueueAdvance(PeriodEditor source,bool immediate,bool nextPeriod=false)
    {
        advanceTimer.Stop(); advanceTarget=null;
        if(!Changes || step!=Count+2 || syncingPeriods || !source.TryEnd(out _)) return;
        int index=periods.IndexOf(source); if(index<0) return;
        advanceTarget=nextPeriod && index+1<periods.Count?periods[index+1]:source;
        if(immediate) Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(RevealPrises)); else advanceTimer.Start();
    }
    void RevealPrises()
    {
        var editor=advanceTarget; advanceTarget=null;
        if(editor==null || !periods.Contains(editor) || !Changes || step!=Count+2 || !IsVisible) return;
        if(periods.Any(p=>p.StartPicker.IsDropDownOpen || p.EndPicker.IsDropDownOpen)) {advanceTarget=editor; advanceTimer.Start(); return;}
        UpdateLayout(); var target=editor; if(!target.IsVisible) return;
        double top=target.TransformToAncestor(scroller).Transform(new Point()).Y;
        double destination=Math.Min(scroller.ScrollableHeight,Math.Max(scroller.VerticalOffset,scroller.VerticalOffset+top-16));
        if(slideTimer.IsEnabled && Math.Abs(destination-slideTo)<2) return;
        StopSlide();
        if(destination-scroller.VerticalOffset<1) return;
        slideFrom=scroller.VerticalOffset; slideTo=destination; slideClock.Restart(); slideTimer.Start();
    }
    void StopSlide() {slideTimer.Stop(); slideClock.Stop();}
}
