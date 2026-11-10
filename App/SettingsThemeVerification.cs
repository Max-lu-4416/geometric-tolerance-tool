using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Tolerance.Core;

namespace Tolerance.App;

internal static class SettingsThemeVerification
{
    internal static void Run(string directory,Action<bool,string> check)
    {
        bool systemDark=false;
        using var clipboard=new ClipboardService(_=>{},_=>{});
        var shell=new ShellWindow(Path.Combine(directory,"settings-theme-regression.json"),clipboard,()=>systemDark);
        shell.Show();
        try
        {
            void Flush()=>shell.Dispatcher.Invoke(()=>shell.UpdateLayout(),DispatcherPriority.ContextIdle);
            Button Option(string name)=>ShellVerification.Descendants<Button>(shell.Host).Single(b=>Equals(b.Content,name));
            void Click(string name){var b=Option(name);b.Focus();b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Flush();}
            foreach(bool large in new[]{false,true,false})
            {
                shell.Settings.LargeFont=large;shell.ApplySettings();
                foreach(string page in new[]{"shaft","hole","shaft-fit","hole-fit","geometry","settings"}){shell.Navigate(page);Flush();}
            }
            Click("深色");Click("浅色");Click("大号");Click("标准");
            var root=shell.Host.Content;var themeButton=Option("深色");
            int cached=FontCount(shell);
            for(int i=0;i<100;i++)
            {
                shell.Navigate("shaft");shell.Navigate("settings");Flush();
                check(ReferenceEquals(root,shell.Host.Content)&&ReferenceEquals(themeButton,Option("深色")),"GT-002 settings tree reused on visit "+i);
            }
            for(int i=0;i<100;i++)
            {
                string name=(i%4) switch{0=>"深色",1=>"浅色",2=>"大号",_=>"标准"};
                Click(name);
                check(ReferenceEquals(root,shell.Host.Content),"GT-002 setting change preserves tree "+i);
                check(Keyboard.FocusedElement is Visual focus&&PresentationSource.FromVisual(focus)!=null,"GT-003 focus stays attached "+i);
            }
            Collect();
            check(FontCount(shell)<=cached,"GT-002 live font cache remains bounded");
            var transient=CacheTransient(shell);Collect();
            check(!transient.IsAlive,"GT-002 font cache releases retired controls");

            // Actual WPF keyboard event paths; an attached Click observer detects duplicates.
            var source=PresentationSource.FromVisual(shell);
            var dark=Option("深色");int activations=0;dark.Click+=(_,_)=>activations++;
            dark.Focus();
            dark.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Enter){RoutedEvent=Keyboard.KeyDownEvent});Flush();
            check(activations==1&&shell.Settings.Theme=="dark"&&dark.IsKeyboardFocused,"GT-003 Enter activates once without losing focus");
            dark.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Space){RoutedEvent=Keyboard.KeyDownEvent});
            dark.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Space){RoutedEvent=Keyboard.KeyUpEvent});Flush();
            check(activations==2&&dark.IsKeyboardFocused,"GT-003 Space activates once");
            check(dark.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)),"GT-003 forward Tab traversal");
            check(Option("跟随系统").IsKeyboardFocused,"GT-003 original next option order");
            check(((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous))&&dark.IsKeyboardFocused,"GT-003 Shift+Tab traversal");
            dark.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.F1){RoutedEvent=Keyboard.PreviewKeyDownEvent});
            check(((ToolTip)dark.ToolTip).IsOpen,"GT-003 F1 opens help after setting changes");
            dark.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});
            check(!((ToolTip)dark.ToolTip).IsOpen&&activations==2,"GT-003 Esc closes help without another activation");

            string[] fullText=shell.SizePages.Values.Select(p=>p.FullText).Append(shell.Geometry.ViewModel.FullResult).ToArray();
            string[] inputs=shell.SizePages.Values.Select(p=>Snapshot(p.Input)).ToArray();
            object?[] results=Results(shell);
            shell.Settings.Theme="system";systemDark=false;shell.ApplySettings();Flush();
            var brush=Application.Current.Resources["Accent"];
            for(int i=0;i<100;i++)
            {
                shell.ApplyTheme();
                check(ReferenceEquals(brush,Application.Current.Resources["Accent"]),"GT-004 unchanged palette identity "+i);
                check(Results(shell).Zip(results).All(p=>ReferenceEquals(p.First,p.Second)),"GT-004 no query refresh "+i);
            }
            systemDark=true;shell.ApplyTheme();Flush();
            check(!ReferenceEquals(brush,Application.Current.Resources["Accent"]),"GT-004 actual system transition updates resources");
            check(((SolidColorBrush)Application.Current.Resources["Accent"]).Color==(Color)ColorConverter.ConvertFromString("#93B8FF"),"GT-004 dark palette applied");
            check(Results(shell).Zip(results).All(p=>ReferenceEquals(p.First,p.Second)),"GT-004 system transition preserves query instances");
            foreach(string theme in new[]{"light","dark","system"})foreach(bool large in new[]{false,true})
            {
                shell.Settings.Theme=theme;shell.Settings.LargeFont=large;shell.ApplySettings();
                foreach(string page in new[]{"shaft","hole","shaft-fit","hole-fit","geometry","settings"}){shell.Navigate(page);Flush();}
                check(shell.SizePages.Values.Select(p=>p.FullText).Append(shell.Geometry.ViewModel.FullResult).SequenceEqual(fullText),"GT-004 visual settings preserve exact copy text");
                check(Results(shell).Zip(results).All(p=>ReferenceEquals(p.First,p.Second)),"GT-004 visual settings preserve query identity");
                check(shell.SizePages.Values.Select(p=>Snapshot(p.Input)).SequenceEqual(inputs),"GT-004 visual settings preserve inputs");
                check(Option(large?"大号":"标准") is { } b&&AutomationProperties.GetItemStatus(b)=="已选中","GT-002 reused settings selected state");
            }
            var palette=Application.Current.Resources["Accent"];
            check(!Appearance.Apply(systemDark),"GT-004 application-level palette also skips unchanged values");
            check(ReferenceEquals(palette,Application.Current.Resources["Accent"]),"GT-004 resources unchanged after idempotent apply");
            shell.Reset();Flush();
            check(ReferenceEquals(root,shell.Host.Content)&&AutomationProperties.GetItemStatus(Option("标准"))=="已选中"&&AutomationProperties.GetItemStatus(Option("浅色"))=="已选中","GT-002 reset refreshes cached selections from new Settings instance");
            shell.Settings.Theme="dark";shell.ApplySettings();
            check(shell.Store.Load().Theme=="dark","GT-002 persistence unchanged with reused view");
        }
        finally{shell.Close();}
        var closed=ClosedWindow(directory);
        Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);Collect();
        check(!closed.IsAlive,"GT-002 closed secondary shell is collectible");
    }
    private static object?[] Results(ShellWindow shell)=>shell.SizePages.Values.Select(p=>(object?)p.Fit??p.Hole??p.Shaft).Append(shell.Geometry.ViewModel.Result??(object?)shell.Geometry.ViewModel.GeneralResult).ToArray();
    private static string Snapshot(PageInput input)=>System.Text.Json.JsonSerializer.Serialize(input);
    private static int FontCount(ShellWindow shell)
    {
        var field=typeof(ShellWindow).GetField("originalFonts",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        return ((System.Collections.IEnumerable)field!.GetValue(shell)!).Cast<object>().Count();
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static WeakReference CacheTransient(ShellWindow shell)
    {var node=new TextBlock{FontSize=17};shell.ApplyFonts(node);return new(node);}
    [MethodImpl(MethodImplOptions.NoInlining)] private static WeakReference ClosedWindow(string directory)
    {var shell=new ShellWindow(Path.Combine(directory,"collectible-settings.json"));shell.Show();shell.Navigate("settings");shell.Close();return new(shell);}
    private static void Collect(){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
}
