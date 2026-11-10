using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Tolerance.App;

internal static class ClickCopyVerification
{
    internal static void Run(ShellWindow window,Action<bool,string> check)
    {
        bool Pump(Func<bool> action)
        {
            bool ok=false;var frame=new DispatcherFrame();var deadline=DateTime.UtcNow.AddSeconds(3);var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};
            timer.Tick+=(_,_)=>{try{ok=action();}catch(ExternalException){}if(ok||DateTime.UtcNow>=deadline)frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);timer.Stop();return ok;
        }
        IDataObject? saved=null;check(Pump(()=>{saved=Clipboard.GetDataObject();return true;}),"Capture click-copy clipboard");
        try
        {
            foreach(string id in new[]{"shaft","hole","shaft-fit","hole-fit"})
            {
                window.Navigate(id);var page=window.SizePages[id];
                foreach(int places in new[]{0,3,4,6})foreach(string legacyUnit in new[]{"mm","μm"})
                {
                    window.Settings.DecimalPlaces=places;window.Settings.CopyUnit=legacyUnit;window.ApplySettings();window.UpdateLayout();
                    var targets=page.NumericCopies.ToArray();check(targets.Count(t=>t.Value.CanCopy)==(id.EndsWith("-fit")?12:10),"Every result number is clickable: "+id);
                    foreach(var target in targets.Where(t=>t.Value.CanCopy))
                    {
                        check(target.Label.Text==target.Value.Text,"Display and copy share formatted source");
                        check(target.Button.Style==window.FindResource("ClickCopyButton"),"Number has no button chrome");
                        target.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        check(Pump(()=>Clipboard.GetText()==target.Value.Text&&window.CopyStatus==target.Value.SuccessMessage),"Exact displayed copy and feedback: "+id+"/"+target.Value.Name+"/"+target.Value.Unit+" actual="+window.CopyStatus);
                    }
                    page.TitleCopy.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    check(Pump(()=>Clipboard.GetText()==page.TitleCopy.Value.Text),"Full dimension title copy");
                }
                var actions=ShellVerification.Descendants<Button>(page).Where(b=>b.Content is string s&&s.StartsWith("复制")).ToArray();
                check(actions.Length==1&&Equals(actions[0].Content,"复制完整结果"),"Only complete-result copy button remains");
                var first=page.NumericCopies.First(t=>t.Value.CanCopy);first.Button.Focus();window.UpdateLayout();
                check(first.Button.IsKeyboardFocused&&first.Label.TextDecorations==TextDecorations.Underline,"Keyboard focus underlines number");
                check(Pump(()=>{Clipboard.SetText("keyboard sentinel");return true;}),"Prepare keyboard clipboard");
                var source=PresentationSource.FromVisual(window)!;
                first.Button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});
                check(Pump(()=>Clipboard.GetText()==first.Value.Text),"Enter copies number");
                check(Pump(()=>{Clipboard.SetText("keyboard sentinel");return true;}),"Prepare Space clipboard");
                first.Button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Space){RoutedEvent=Keyboard.KeyDownEvent});
                first.Button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Space){RoutedEvent=Keyboard.KeyUpEvent});
                check(Pump(()=>Clipboard.GetText()==first.Value.Text),"Space copies number");
                var nominal=page.Input.Nominal;page.Input.Nominal="0";page.Refresh();
                check(page.NumericCopies.All(t=>!t.Button.IsEnabled)&&!page.TitleCopy.Button.IsEnabled,"Invalid values and title cannot copy");
                check(Pump(()=>{Clipboard.SetText("disabled sentinel");return true;}),"Prepare invalid clipboard");
                first.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(Clipboard.GetText()=="disabled sentinel","Disabled target cannot overwrite clipboard even with a programmatic click");
                page.Input.Nominal=nominal;page.Refresh();
            }
            var shaft=window.SizePages["shaft"];window.Navigate("shaft");
            shaft.Restore(new(){ShaftZone="n",ShaftGrade=6});
            check(shaft.NumericCopies.Any(t=>t.Value.Text.StartsWith('+')&&t.Value.CanCopy),"Positive sign preserved");
            shaft.Restore(new(){Nominal="50.12345678901234567890123456"});window.UpdateLayout();
            shaft.TitleCopy.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(Pump(()=>Clipboard.GetText()==shaft.TitleCopy.Value.Text)&&Clipboard.GetText().Contains("12345678901234567890123456"),"Long annotation copies full precision");
            foreach(var target in shaft.NumericCopies.Where(t=>t.Value.Name.Contains("尺寸")&&t.Value.CanCopy))
            {
                target.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Pump(()=>Clipboard.GetText()==target.Value.Text),"Long limit-size copy preserves every digit");
            }
            window.Navigate("geometry");window.UpdateLayout();
            var geometryButtons=ShellVerification.Descendants<Button>(window.Design).Where(b=>b.Content is string s&&s.StartsWith("复制")).ToArray();
            check(geometryButtons.Length==1&&Equals(geometryButtons[0].Content,"复制完整结果"),"Geometry only retains full-result button");
            window.Navigate("settings");window.UpdateLayout();
            check(!ShellVerification.Descendants<TextBlock>(window.Design).Any(t=>t.Text=="默认复制单位"),"Obsolete copy-unit setting removed");
        }
        finally{check(Pump(()=>{if(saved is null)Clipboard.Clear();else Clipboard.SetDataObject(saved,true);return true;}),"Restore click-copy clipboard");}
        window.Reset();window.Navigate("geometry");
    }
}
