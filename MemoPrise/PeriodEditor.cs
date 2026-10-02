using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
namespace MemoPrise;
public sealed class PeriodEditor : Border
{
    public readonly DatePicker StartPicker=new() {FontSize=17,MinWidth=145};
    public readonly TextBox Duration=new() {Text="14",Width=80,Margin=new Thickness(8,0,8,0)};
    public readonly CheckBox Unlimited=new() {Content="Continuer sans date de fin"};
    public readonly DatePicker EndPicker=new() {FontSize=17,MinWidth=145};
    readonly StackPanel ending=new();
    readonly StackPanel endDateField=new();
    readonly WrapPanel durationRow=new() {Margin=new Thickness(0,4,0,8)};
    bool isLast;
    bool syncingEnd;
    public bool IsLast {get=>isLast;set {if(isLast==value) return; isLast=value; ending.Visibility=value?Visibility.Visible:Visibility.Collapsed; UpdateEnding();}}
    readonly TextBlock title=MainWindow.Text("",21,true);
    readonly ComboBox count=new() {FontSize=17,Width=75,Padding=new Thickness(4),Margin=new Thickness(8)};
    readonly StackPanel dosePanel=new();
    readonly WrapPanel daily=new();
    readonly Button? addDose;
    public FrameworkElement PrisesSection=>fixedDoses?dosePanel:daily;
    readonly List<Row> rows=new();
    readonly bool fixedDoses;
    List<DailyDose> initial;
    int number;
    public event Action? DatesChanged;
    public event Action<DateTime>? StartChanged;
    public event Action<bool,bool>? AdvanceRequested;
    public PeriodEditor(TreatmentPeriod period,bool fixedDoses=false)
    {
        this.fixedDoses=fixedDoses; initial=period.Prises.ToList(); Background=Brushes.White; BorderBrush=Theme.Outline; BorderThickness=new Thickness(1); CornerRadius=new CornerRadius(14); Padding=new Thickness(16); Margin=new Thickness(0,0,0,16);
        var p=new StackPanel(); Child=p; p.Children.Add(title);
        var dates=new WrapPanel {Margin=new Thickness(0,0,0,10)}; dates.Children.Add(MainWindow.Text("À partir du ",17)); StartPicker.SelectedDate=period.Start; StartPicker.IsEnabled=!fixedDoses; dates.Children.Add(StartPicker); p.Children.Add(dates);
        durationRow.Children.Add(MainWindow.Text("Pendant",17)); durationRow.Children.Add(Duration); durationRow.Children.Add(MainWindow.Text("jours",17)); p.Children.Add(durationRow);
        if(period.End!=null) Duration.Text=((period.End.Value.Date-period.Start.Date).Days+1).ToString(CultureInfo.InvariantCulture);
        Unlimited.IsChecked=period.End==null; Duration.IsEnabled=Unlimited.IsChecked!=true;
        ending.Children.Add(Unlimited); endDateField.Children.Add(MainWindow.Text("Date de fin du traitement",17,true)); endDateField.Children.Add(EndPicker); ending.Children.Add(endDateField); p.Children.Add(ending); IsLast=true;
        if(!fixedDoses) {
            daily.Children.Add(MainWindow.Text("Prises par jour",17)); for(int n=1;n<=24;n++) count.Items.Add(n); count.SelectedItem=initial.Count; daily.Children.Add(count);
            addDose=MainWindow.Button("+ Ajouter une prise",()=> {if((int)count.SelectedItem<24) count.SelectedItem=(int)count.SelectedItem+1;});
            addDose.FontSize=16; addDose.Padding=new Thickness(12,8,12,8); daily.Children.Add(addDose); p.Children.Add(daily);
            p.Children.Add(MainWindow.Text("Chaque période peut avoir un nombre de prises différent.",16));
        }
        p.Children.Add(dosePanel); BuildRows();
        StartPicker.SelectedDateChanged+=(_,_)=> {UpdateEnding(); if(StartPicker.SelectedDate is DateTime day) StartChanged?.Invoke(day.Date);};
        Duration.TextChanged+=(_,_)=> {if(!syncingEnd) UpdateEnding(); DatesChanged?.Invoke(); if(!syncingEnd && Duration.IsKeyboardFocusWithin) AdvanceRequested?.Invoke(false,true);};
        Unlimited.Checked+=(_,_)=> {UpdateEnding(); DatesChanged?.Invoke();}; Unlimited.Unchecked+=(_,_)=> {UpdateEnding(); DatesChanged?.Invoke();};
        EndPicker.SelectedDateChanged+=(_,_)=> {if(syncingEnd || !IsLast) return; if(EndPicker.SelectedDate is DateTime finish && StartPicker.SelectedDate is DateTime begin && finish>=begin) {syncingEnd=true; Duration.Text=((finish.Date-begin.Date).Days+1).ToString(CultureInfo.InvariantCulture); syncingEnd=false;} DatesChanged?.Invoke();};
        count.SelectionChanged+=(_,_)=>BuildRows();
        Duration.LostKeyboardFocus+=(_,_)=> {if(TryEnd(out _)) AdvanceRequested?.Invoke(true,true);};
        Duration.PreviewKeyDown+=(_,e)=> {if(e.Key==Key.Enter && TryEnd(out var ignoredEnd)) {AdvanceRequested?.Invoke(true,true); e.Handled=true;}};
        void FollowDate(DatePicker picker)
        {
            DateTime? openedDate=null;
            picker.CalendarOpened+=(_,_)=>openedDate=picker.SelectedDate;
            picker.CalendarClosed+=(_,_)=> {if(picker.SelectedDate!=openedDate && picker.SelectedDate!=null) AdvanceRequested?.Invoke(true,false);};
            picker.SelectedDateChanged+=(_,_)=> {if(picker.IsKeyboardFocusWithin && !picker.IsDropDownOpen) AdvanceRequested?.Invoke(false,false);};
            picker.LostKeyboardFocus+=(_,_)=> {if(!picker.IsDropDownOpen && picker.SelectedDate!=null) AdvanceRequested?.Invoke(true,false);};
        }
        FollowDate(StartPicker); FollowDate(EndPicker);
        Unlimited.Click+=(_,_)=>AdvanceRequested?.Invoke(true,false);
    }
    public void Number(int n)
    {
        number=n; title.Text=$"Période {n}"; AutomationProperties.SetAutomationId(StartPicker,$"period-{n}-start"); AutomationProperties.SetAutomationId(Duration,$"period-{n}-duration"); AutomationProperties.SetAutomationId(Unlimited,$"period-{n}-unlimited"); AutomationProperties.SetAutomationId(count,$"period-{n}-count");
        AutomationProperties.SetAutomationId(EndPicker,$"period-{n}-end");
        if(addDose!=null) AutomationProperties.SetAutomationId(addDose,$"period-{n}-add-dose");
        for(int i=0;i<rows.Count;i++) {
            var row=rows[i]; row.Label.Text=$"Quantité · prise {i+1}";
            row.Remove.IsEnabled=(int)count.SelectedItem>1;
            row.Remove.ToolTip=row.Remove.IsEnabled?"Supprimer cette prise":"Conservez au moins une prise dans cette période";
            AutomationProperties.SetName(row.Remove,$"Supprimer la prise {i+1}");
            AutomationProperties.SetAutomationId(row.Remove,$"period-{n}-remove-dose-{i+1}");
            AutomationProperties.SetAutomationId(row.Confirm,$"period-{n}-confirm-remove-dose-{i+1}");
            AutomationProperties.SetAutomationId(row.Cancel,$"period-{n}-cancel-remove-dose-{i+1}");
            AutomationProperties.SetAutomationId(row.Hours,$"period-{n}-hours-{i+1}"); AutomationProperties.SetAutomationId(row.Minutes,$"period-{n}-minutes-{i+1}"); AutomationProperties.SetAutomationId(row.Quantity,$"period-{n}-dose-{i+1}");
        }
    }
    public void SetFixedEntries(List<DailyDose> entries) {initial=entries.ToList(); BuildRows();}
    void BuildRows()
    {
        dosePanel.Children.Clear();
        if(fixedDoses) {foreach(var entry in initial) dosePanel.Children.Add(MainWindow.Text($"{entry.Time} → {entry.Dose}",18,true)); return;}
        int total=(int)(count.SelectedItem??initial.Count);
        if(addDose!=null) addDose.IsEnabled=total<24;
        while(rows.Count<total)
        {
            int n=rows.Count;
            int newHour=new[]{8,12,20}.Concat(Enumerable.Range(0,24)).Distinct().First(h=>!rows.Any(r=>r.Hours.Value==h && r.Minutes.Value==0));
            var entry=n<initial.Count?initial[n]:new DailyDose($"{newHour:00}:00","");
            var hours=new TimeWheel("Heures",24,int.Parse(entry.Time.Split(':')[0])); var minutes=new TimeWheel("Minutes",60,int.Parse(entry.Time.Split(':')[1])); var quantity=new TextBox {Text=entry.Dose,Margin=new Thickness(12,0,0,0),Width=220,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};
            var row=new Grid {Margin=new Thickness(0,4,0,6)}; row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto}); row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto}); row.ColumnDefinitions.Add(new ColumnDefinition()); row.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto}); row.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            var confirmation=new StackPanel {Visibility=Visibility.Collapsed,Margin=new Thickness(4,8,4,8)};
            var question=MainWindow.Text("",16,true); confirmation.Children.Add(question); var choices=new WrapPanel(); confirmation.Children.Add(choices);
            Row? item=null;
            var remove=MainWindow.Button("",()=> {
                foreach(var other in rows) other.Confirmation.Visibility=Visibility.Collapsed;
                question.Text=$"Supprimer la prise de {hours.Value:00}:{minutes.Value:00} de cette période ?";
                confirmation.Visibility=Visibility.Visible;
            });
            remove.Content=new System.Windows.Shapes.Path {Data=Geometry.Parse("M 2,4 L 14,4 M 6,4 L 6,2 L 10,2 L 10,4 M 3,4 L 4,17 L 12,17 L 13,4 M 6,7 L 6.5,14 M 10,7 L 9.5,14"),Stroke=Theme.Solid("#597EA9"),StrokeThickness=1.6,Width=16,Height=18,Stretch=Stretch.Uniform};
            remove.Width=32; remove.Height=32; remove.Padding=new Thickness(7); remove.Margin=new Thickness(8,0,0,6);
            var confirm=MainWindow.Button("Supprimer cette prise",()=>RemoveDose(item!)); confirm.FontSize=16; confirm.Foreground=Theme.Solid("#914D62"); confirm.Background=Theme.Gradient("#FFF5F8","#F8E6ED");
            var cancel=MainWindow.Button("Annuler",()=>confirmation.Visibility=Visibility.Collapsed); cancel.FontSize=16; choices.Children.Add(cancel); choices.Children.Add(confirm);
            row.Children.Add(hours); Grid.SetColumn(minutes,1); row.Children.Add(minutes); var q=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            var label=MainWindow.Text($"Quantité · prise {n+1}",16); var heading=new WrapPanel {VerticalAlignment=VerticalAlignment.Center}; heading.Children.Add(label); heading.Children.Add(remove); q.Children.Add(heading); q.Children.Add(quantity); Grid.SetColumn(q,2); row.Children.Add(q);
            Grid.SetRow(confirmation,1); Grid.SetColumnSpan(confirmation,3); row.Children.Add(confirmation);
            item=new Row(row,hours,minutes,quantity,label,remove,confirm,cancel,confirmation); rows.Add(item);
        }
        foreach(var row in rows.Take(total)) dosePanel.Children.Add(row.Panel); Number(number);
    }
    void RemoveDose(Row row)
    {
        int total=(int)count.SelectedItem, index=rows.IndexOf(row);
        if(total<=1 || index<0 || index>=total) return;
        rows.RemoveAt(index); if(index<initial.Count) initial.RemoveAt(index);
        foreach(var remaining in rows) remaining.Confirmation.Visibility=Visibility.Collapsed;
        count.SelectedItem=total-1;
    }
    public bool TryEnd(out DateTime? end)
    {
        end=null; if(StartPicker.SelectedDate==null) return false;
        if(IsLast && Unlimited.IsChecked==true) return true;
        if(IsLast) {end=EndPicker.SelectedDate?.Date; return end!=null && end>=StartPicker.SelectedDate.Value.Date;}
        if(!int.TryParse(Duration.Text,out int days)||days<1||days>36500) return false;
        try {end=StartPicker.SelectedDate.Value.Date.AddDays(days-1); return true;} catch(ArgumentOutOfRangeException) {return false;}
    }
    void UpdateEnding()
    {
        bool unlimited=IsLast && Unlimited.IsChecked==true;
        Duration.IsEnabled=!unlimited; durationRow.Visibility=unlimited?Visibility.Collapsed:Visibility.Visible; endDateField.Visibility=unlimited?Visibility.Collapsed:Visibility.Visible;
        if(!syncingEnd) {syncingEnd=true; try {EndPicker.SelectedDate=StartPicker.SelectedDate is DateTime begin && int.TryParse(Duration.Text,out int days) && days>=1 && days<=36500 && begin.Date<=DateTime.MaxValue.Date.AddDays(-(days-1))?begin.Date.AddDays(days-1):null;} finally {syncingEnd=false;}}
    }
    public TreatmentPeriod GetPeriod()
    {
        if(!TryEnd(out var end)) throw new InvalidDataException($"Période {number} : indiquez une date et une durée entre 1 et 36500 jours.");
        var entries=fixedDoses?initial:rows.Take((int)count.SelectedItem).Select(r=>new DailyDose($"{r.Hours.Value:00}:{r.Minutes.Value:00}",r.Quantity.Text.Trim())).ToList();
        return new TreatmentPeriod(StartPicker.SelectedDate!.Value.Date,end,Schedule.ValidateDoses(entries));
    }
    sealed record Row(Grid Panel,TimeWheel Hours,TimeWheel Minutes,TextBox Quantity,TextBlock Label,Button Remove,Button Confirm,Button Cancel,StackPanel Confirmation);
}
