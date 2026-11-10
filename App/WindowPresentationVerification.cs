using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Tolerance.Core;

namespace Tolerance.App;

internal static class WindowPresentationVerification
{
    internal static void Run(ShellWindow window,string directory,Action<bool,string> check)
    {
        // These parameterized cases exercise DIP/work-area policy, not a physical DPI change.
        foreach(double scale in new[]{1d,1.25,1.5,2})foreach(var pixels in new[]{new Size(1280,720),new Size(1920,1080),new Size(2560,1440)})
        {
            var work=new Size(pixels.Width/scale,pixels.Height/scale);
            var limit=ContentWindowSizing.Limit(new Size(1180,770),new Thickness(16),new Size(16,39),work);
            check(limit.Width<=work.Width&&limit.Height<=work.Height,"Window cap fits simulated work area/DPI");
            check(limit.Width<=1228&&limit.Height<=841,"Content cap does not grow on a larger monitor");
            check(Math.Abs((limit.Width-48)/1180-(limit.Height-71)/770)<.000001,"Small desktops shrink the entire canvas uniformly without surplus side space");
        }
        window.Reset();window.Navigate("hole-fit");window.Activate();
        void Flush()=>window.Dispatcher.Invoke(()=>window.UpdateLayout(),DispatcherPriority.ContextIdle);
        void Command(int command){SendMessage(new WindowInteropHelper(window).Handle,0x0112,command,0);Flush();}
        var initial=new Size(window.Width,window.Height);
        var results=window.SizePages.Values.Select(p=>p.FullText).ToArray();
        check(double.IsFinite(window.MaxWidth)&&double.IsFinite(window.MaxHeight),"Finite content-based window limits");
        check(window.WindowStartupLocation==WindowStartupLocation.CenterScreen,"Default startup is centered");
        window.Width=window.MaxWidth+1000;window.Height=window.MaxHeight+1000;Flush();
        check(window.ActualWidth<=window.MaxWidth+.5&&window.ActualHeight<=window.MaxHeight+.5,"Oversize requests are constrained without cropping");
        Command(0xF030);
        check(window.WindowState==WindowState.Maximized&&window.ActualWidth<=window.MaxWidth+.5&&window.ActualHeight<=window.MaxHeight+.5,"Native maximize honors content limit");
        ShellVerification.Render(window,Path.Combine(directory,"window-native-maximized.png"),1);
        Command(0xF120);
        window.Width=window.MinWidth;window.Height=window.MinHeight;Flush();
        var small=new Size(window.ActualWidth,window.ActualHeight);
        var outer=(FrameworkElement)window.Content;
        var bounds=window.Design.TransformToAncestor(outer).TransformBounds(new Rect(window.Design.RenderSize));
        check(bounds.Left>=-.5&&bounds.Top>=-.5&&bounds.Right<=outer.ActualWidth+.5&&bounds.Bottom<=outer.ActualHeight+.5,"Smaller window scales the entire design into view");
        ShellVerification.Render(window,Path.Combine(directory,"window-small.png"),1);
        Command(0xF030);Command(0xF120);
        check(Math.Abs(window.ActualWidth-small.Width)<1&&Math.Abs(window.ActualHeight-small.Height)<1,"Native restore retains the user's smaller window size");
        var hwnd=new WindowInteropHelper(window).Handle;
        SendMessage(hwnd,0x00A3,2,0);Flush();
        check(window.WindowState==WindowState.Maximized&&window.ActualWidth<=window.MaxWidth+.5,"Caption double-click maximizes within cap");
        SendMessage(hwnd,0x00A3,2,0);Flush();
        check(window.WindowState==WindowState.Normal&&Math.Abs(window.ActualWidth-small.Width)<1,"Caption double-click restores smaller bounds");
        Command(0xF020);check(window.WindowState==WindowState.Minimized,"Native minimize remains available");Command(0xF120);
        check(window.WindowState==WindowState.Normal,"Native restore from minimize");
        window.Width=window.MaxWidth;window.Height=window.MaxHeight;Flush();
        check(window.SizePages.Values.Select(p=>p.FullText).SequenceEqual(results),"Window state changes preserve all query/copy results");
        check(((CornerRadius)Application.Current.Resources["PanelCornerRadius"]).TopLeft==8&&((CornerRadius)Application.Current.Resources["ControlCornerRadius"]).TopLeft==4&&((CornerRadius)Application.Current.Resources["MatrixCornerRadius"]).TopLeft<=2,"Separate Fluent panel/control/matrix radii");
        foreach(string theme in new[]{"light","dark"})
        {
            window.Settings.Theme=theme;window.ApplySettings();
            foreach(string page in new[]{"shaft","hole","shaft-fit","hole-fit","geometry","settings"})
            {
                window.Navigate(page);System.Windows.Input.Keyboard.ClearFocus();Flush();
                foreach(double scale in new[]{1d,1.25,1.5,2})ShellVerification.Render(window,Path.Combine(directory,$"fluent-{page}-{theme}-dpi{scale*100:0}.png"),scale);
                check(((FrameworkElement)window.Host.Content).IsVisible,"Six pages remain visible at capped size");
            }
            window.Navigate("hole-fit");Flush();
            var selected=window.SizePages["hole-fit"].MatrixCells.Single(c=>SelectionVisual.GetIsSelected(c.Button)).Button;
            selected.Focus();Flush();check(selected.IsKeyboardFocused&&SelectionVisual.GetIsSelected(selected),"Matrix keyboard focus preserves selected query");
            ShellVerification.Render(window,Path.Combine(directory,$"fluent-matrix-focus-{theme}.png"),1);
            var navigation=window.Design.Children.OfType<Grid>().Single(g=>g.Name=="NavigationBar");
            var tab=navigation.Children.OfType<Button>().Single(b=>Equals(b.Content,"基孔制配合"));tab.Focus();Flush();
            check(((Border)tab.Template.FindName("NavFocus",tab)).Visibility==Visibility.Visible,"Navigation has a dedicated visible focus state");
            ShellVerification.Render(window,Path.Combine(directory,$"fluent-navigation-focus-{theme}.png"),1);
        }
        string legacy=Path.Combine(directory,"legacy-window-fields.json");
        File.WriteAllText(legacy,JsonSerializer.Serialize(new ToolSettings()).Insert(1,"\"WindowWidth\":99999,\"WindowHeight\":99999,\"WindowState\":\"Maximized\","));
        var restored=new ShellWindow(legacy,new ClipboardService(_=>{},_=>{}));
        try{restored.Show();restored.UpdateLayout();check(restored.WindowState==WindowState.Normal&&restored.ActualWidth<=restored.MaxWidth+.5&&restored.ActualHeight<=restored.MaxHeight+.5,"Unknown legacy window dimensions cannot restore oversized bounds");}
        finally{restored.Close();}
        File.WriteAllText(Path.Combine(directory,"window-presentation.json"),JsonSerializer.Serialize(new{dpi=GetDpiForWindow(hwnd),window.MaxWidth,window.MaxHeight,initial,small,restore=window.RestoreBounds,renderScales=new[]{100,125,150,200},physicalDpiChanged=false},new JsonSerializerOptions{WriteIndented=true}));
        window.Reset();window.Width=initial.Width;window.Height=initial.Height;window.Navigate("geometry");window.Activate();Flush();
    }
    [DllImport("user32.dll")]private static extern nint SendMessage(nint hwnd,int message,nint wParam,nint lParam);
    [DllImport("user32.dll")]private static extern uint GetDpiForWindow(nint hwnd);
}
