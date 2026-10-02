using MemoPrise;
using System.Text.Json;
int count=0;
void Check(bool ok,string name) {if(!ok) throw new Exception(name); Console.WriteLine("OK — "+name); count++;}
var date=new DateTime(2026,10,1);
var treatment=new Treatment("a","Traitement A","1 comprimé","08:00;20:00",1<<(int)DayOfWeek.Thursday,date,date.AddDays(7),"note");
Check(Schedule.ForDay(treatment,date).Count()==2,"Deux horaires par jour");
Check(!Schedule.ForDay(treatment,date.AddDays(1)).Any(),"Respect des jours de semaine");
Check(!Schedule.ForDay(treatment,date.AddDays(-7)).Any(),"Respect de la date de début");
Check(!Schedule.ForDay(treatment,date.AddDays(14)).Any(),"Respect de la date de fin");
Check(!Schedule.ForDay(treatment with {Active=false},date).Any(),"Suspension");
var intake=Schedule.ForDay(treatment,date).First();
Check(Schedule.NeedsReminder(intake,date.AddHours(8)),"Rappel à l’heure prévue");
Check(!Schedule.NeedsReminder(intake,date.AddHours(7)),"Pas de rappel anticipé");
Check(!Schedule.NeedsReminder(intake with {Status="taken"},date.AddHours(9)),"Pas de rappel après validation");
Check(!Schedule.NeedsReminder(intake with {Snooze=date.AddHours(9)},date.AddHours(8)),"Report conservé");
Check(Schedule.NeedsReminder(intake with {Snooze=date.AddHours(9)},date.AddHours(9)),"Rappel après report");
var folder=Path.Combine(Path.GetTempPath(),"MemoPrise-tests-"+Guid.NewGuid()); Directory.CreateDirectory(folder); var db=Path.Combine(folder,"test.db");
using(var store=new Store(db)) {
 store.Save(treatment); store.Day(date); store.Day(date);
 Check(store.Intakes().Count==2,"Pas de doublon des prises");
 store.Save(intake with {Status="taken",Taken=date.AddHours(8).AddMinutes(3)});
 store.Setting("effective:a",DateTime.Now.ToString("O"),true); store.Save(treatment with {Name="Nouveau nom",Times="10:00"});
 Check(store.Day(date).Count==2 && store.Day(date).All(i=>i.Name=="Traitement A"),"Modification sans réécriture de l’historique");
 var backup=Path.Combine(folder,"backup.json"); store.Backup(backup);
 store.Save(treatment with {Name="Modification"}); store.Restore(backup);
 Check(store.Treatments().Single().Name=="Nouveau nom","Restauration complète");
 var invalid=Path.Combine(folder,"invalid.json"); File.WriteAllText(invalid,"{\"Version\":99}");
 try {store.Restore(invalid); throw new Exception("Sauvegarde invalide acceptée");} catch(InvalidDataException) {}
 Check(store.Treatments().Single().Name=="Nouveau nom","Sauvegarde invalide sans perte de données");
}
using(var store=new Store(db)) Check(store.Intakes().Single(i=>i.Key==intake.Key).Taken==date.AddHours(8).AddMinutes(3),"Validation persistante après redémarrage");
var variable=treatment with {Dose="1 cachet",Times="12:00;20:00",Prises=new List<DailyDose>{new("12:00","1 cachet"),new("20:00","2 cachets")}};
var variableIntakes=Schedule.ForDay(variable,date).ToList();
Check(variableIntakes[0].Due.Hour==12 && variableIntakes[0].Dose=="1 cachet" && variableIntakes[1].Due.Hour==20 && variableIntakes[1].Dose=="2 cachets","Quantité indépendante pour chaque horaire");
Check(Schedule.Doses(treatment).All(d=>d.Dose=="1 comprimé"),"Lecture des anciens traitements sans migration destructive");
Check(Schedule.ValidateDoses(new[]{new DailyDose("20:00","2 cachets"),new DailyDose("12:00","1 cachet")})[0].Time=="12:00","Classement chronologique des prises");
void Invalid(Action action,string label) {bool rejected=false; try {action();} catch(Exception ex) when(ex is InvalidDataException || ex is FormatException) {rejected=true;} Check(rejected,label);}
Invalid(()=>Schedule.ValidateDoses(new[]{new DailyDose("12:00","1 cachet"),new DailyDose("12:00","2 cachets")}),"Refus des horaires en double");
Invalid(()=>Schedule.ValidateDoses(new[]{new DailyDose("12:00","")}),"Quantité obligatoire par prise");
Invalid(()=>Schedule.ValidateDoses(new[]{new DailyDose("24:00","1 cachet")}),"Refus des horaires hors de la journée");
using(var store=new Store(Path.Combine(folder,"variable.db"))) {
 store.Save(variable); store.Day(date); store.Save(variableIntakes[0] with {Status="taken",Taken=date.AddHours(12)});
 store.Setting("effective:a",date.AddDays(1).ToString("O"),true);
 store.Save(variable with {Prises=new List<DailyDose>{new("12:00","4 cachets"),new("20:00","5 cachets")}});
 Check(store.Day(date).Single(i=>i.Due.Hour==12).Dose=="1 cachet" && store.Day(date).Single(i=>i.Due.Hour==20).Dose=="2 cachets","Modification des quantités sans changer l’historique");
 var future=store.Day(date.AddDays(7)); Check(future[0].Dose=="4 cachets" && future[1].Dose=="5 cachets","Nouvelles quantités sur les prochaines prises");
 var backup=Path.Combine(folder,"variable.json"); store.Backup(backup); store.Save(treatment); store.Restore(backup);
 Check(store.Treatments().Single().Prises![1].Dose=="5 cachets","Sauvegarde/restauration des quantités par horaire");
 var data=JsonSerializer.Deserialize<BackupData>(File.ReadAllText(backup))!;
 var oldBackup=Path.Combine(folder,"legacy.json"); File.WriteAllText(oldBackup,JsonSerializer.Serialize(data with {Version=1,Treatments=new List<Treatment>{treatment}})); store.Restore(oldBackup);
 Check(store.Treatments().Single().Prises==null && Schedule.Doses(store.Treatments().Single())[1].Dose=="1 comprimé","Restauration des sauvegardes de version 1.0");
}
var phases=new List<TreatmentPeriod> {
 new(date,date.AddDays(13),new(){new("08:00","1 cachet"),new("20:00","2 cachets")}),
 new(date.AddDays(14),date.AddDays(27),new(){new("08:00","2 cachets"),new("20:00","2 cachets")}),
 new(date.AddDays(28),null,new(){new("09:00","3 cachets")})
};
var changing=variable with {Days=127,End=null,Prises=phases[0].Prises,Periods=phases}; Schedule.ValidatePeriods(changing);
Check(Schedule.ForDay(changing,date).First().Dose=="1 cachet","Première quantité au premier jour");
Check(Schedule.ForDay(changing,date.AddDays(13)).First().Dose=="1 cachet","Première période pendant exactement 14 jours");
Check(Schedule.ForDay(changing,date.AddDays(14)).First().Dose=="2 cachets","Changement automatique au quinzième jour");
Check(Schedule.ForDay(changing,date.AddDays(27)).Count()==2,"Dernier jour inclus de la deuxième période");
Check(Schedule.ForDay(changing,date.AddDays(28)).Single().Due.Hour==9,"Plusieurs changements avec nouvel horaire et nombre de prises");
Check(Schedule.ForDay(changing,date.AddYears(1)).Single().Dose=="3 cachets","Dernière période permanente");
Check(!Schedule.ForDay(changing,date.AddDays(-1)).Any(),"Aucune prise avant les périodes");
Invalid(()=>Schedule.ValidatePeriods(changing with {Periods=new(){phases[0],phases[1] with {Start=date.AddDays(13)},phases[2]}}),"Refus des périodes qui se chevauchent");
Invalid(()=>Schedule.ValidatePeriods(changing with {Periods=new(){phases[0],phases[1] with {Start=date.AddDays(15)},phases[2]}}),"Refus des intervalles entre périodes");
Invalid(()=>Schedule.ValidatePeriods(changing with {Periods=new(){phases[0] with {End=null},phases[1],phases[2]}}),"Seule la dernière période peut être permanente");
var finite=changing with {End=date.AddDays(35),Periods=new(){phases[0],phases[1],phases[2] with {End=date.AddDays(35)}}}; Schedule.ValidatePeriods(finite);
Check(!Schedule.ForDay(finite,date.AddDays(36)).Any(),"Arrêt après la dernière période finie");
using(var store=new Store(Path.Combine(folder,"periods.db"))) {
 store.Save(changing); store.Day(date); store.Day(date.AddDays(14));
 Check(store.Intakes().Single(i=>i.Due==date.AddDays(14).AddHours(8)).Dose=="2 cachets","Historique contenant la posologie de chaque période");
 var backup=Path.Combine(folder,"periods.json"); store.Backup(backup); store.Save(treatment); store.Restore(backup);
 Check(store.Treatments().Single().Periods!.Count==3 && Schedule.ForDay(store.Treatments().Single(),date.AddDays(28)).Single().Dose=="3 cachets","Sauvegarde et restauration de plusieurs périodes");
 var data=JsonSerializer.Deserialize<BackupData>(File.ReadAllText(backup))!;
 File.WriteAllText(backup,JsonSerializer.Serialize(data with {Version=2,Treatments=new(){variable}})); store.Restore(backup);
 Check(store.Treatments().Single().Periods==null && Schedule.ForDay(store.Treatments().Single(),date).Last().Dose=="2 cachets","Compatibilité des sauvegardes 1.1");
}
using(var store=new Store(Path.Combine(folder,"delete.db"))) {
 var removable=treatment with {Id="remove",Days=127,Start=DateTime.Today.AddDays(-1),End=null}; store.Save(removable);
 var history=new Intake("confirmed-history","remove","Ancien médicament","1 cachet","",DateTime.Now.AddMinutes(-2),"taken",DateTime.Now.AddMinutes(-1)); store.Save(history);
 var missed=history with {Key="unconfirmed-history",Status="pending",Taken=null}; store.Save(missed);
 var tomorrow=store.Day(DateTime.Today.AddDays(1)); store.Save(tomorrow[0] with {Status="taken",Taken=DateTime.Now});
 store.DeleteTreatment("remove");
 Check(store.Treatments().Count==0,"Suppression de la boîte à pharmacie");
 Check(store.Intakes().Single(i=>i.Key==history.Key)==history,"Suppression sans perte de l’historique confirmé");
 Check(store.Intakes().Any(i=>i.Key==missed.Key),"Conservation des anciennes prises non confirmées");
 Check(!store.Intakes().Any(i=>i.TreatmentId=="remove" && i.Status=="pending" && i.Due>DateTime.Now),"Suppression des prochaines prises non validées");
 Check(store.Day(DateTime.Today.AddDays(1)).Count==1 && store.Day(DateTime.Today.AddDays(1))[0].Status=="taken","Une validation future déjà enregistrée reste dans l’historique");
 var backup=Path.Combine(folder,"deleted-backup.json"); store.Backup(backup); store.Restore(backup);
 Check(store.Treatments().Count==0 && store.Intakes().Any(i=>i.Key==history.Key),"Sauvegarde de l’historique après suppression");
}
Console.WriteLine($"{count} vérifications réussies.");
