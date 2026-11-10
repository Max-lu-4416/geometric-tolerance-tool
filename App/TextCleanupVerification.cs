using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace Tolerance.App;

internal static class TextCleanupVerification
{
    internal static void Run(ShellWindow window,string directory,Action<bool,string> check)
    {
        string[] pages=["shaft","hole","shaft-fit","hole-fit","geometry","settings"];
        string[] removed=["几何公差查询","选择类型与等级，输入参数；点击等级可查看选用说明","设置已自动保存","设置自动保存","偏好自动保存到当前 Windows 用户","完整收录查询"];
        foreach(var pageId in pages)
        {
            window.Navigate(pageId);window.UpdateLayout();
            var labels=ShellVerification.Descendants<TextBlock>(window.Host).Where(t=>t.IsVisible).ToArray();
            ShellVerification.Render(window,Path.Combine(directory,"cleanup-"+pageId+".png"),1);
            check(!labels.Any(t=>removed.Contains(t.Text)),"Removed redundant normal text: "+pageId);
            check(!labels.Any(t=>t.Text==ShellWindow.PageTitle(pageId)),"No duplicate page heading: "+pageId);
            if(window.SizePages.TryGetValue(pageId,out var page))
            {
                page.SetMatrixMode(true);window.UpdateLayout();
                check(page.RowDefinitions[2].Height.Value==0&&page.RowDefinitions[2].ActualHeight<=1.1,$"Normal matrix instructions leave no empty row: {pageId}/{page.RowDefinitions[2].ActualHeight}");
                var input=ShellVerification.Descendants<TextBox>(page).Single(t=>t.IsVisible);
                var inputLeft=input.TransformToAncestor(page).Transform(new Point()).X;
                check(Math.Abs(inputLeft-98)<=1,"Nominal input aligns left after deleting page heading");
                var modes=page.Children.OfType<StackPanel>().Single(p=>Grid.GetRow(p)==1).Children.OfType<Button>().ToArray();
                foreach(var mode in modes)CheckHelp(mode,window,check);
                check(AutomationProperties.GetHelpText(modes[0]).Contains(pageId=="hole-fit"?"H8":"一格"),"Matrix help retains specific selection guidance");
                page.Input.Nominal="600";page.Refresh();window.UpdateLayout();
                check(page.RowDefinitions[2].ActualHeight>0,"Matrix size restriction remains visible");
                page.SetMatrixMode(false);window.UpdateLayout();
                check(page.RowDefinitions[2].Height.Value==0&&page.RowDefinitions[2].ActualHeight<=1.1,"Free mode hides irrelevant matrix restriction");
                check(!ShellVerification.Descendants<TextBlock>(page).Any(t=>t.IsVisible&&t.Text.StartsWith("自由选择包含")),"Free-mode overview moved to context help");
                page.Input.Nominal="0";page.Refresh();window.UpdateLayout();
                check(page.RowDefinitions[2].ActualHeight>0&&ShellVerification.Descendants<TextBlock>(page).Any(t=>t.IsVisible&&t.Text==page.Error),"Invalid query reason remains directly visible");
                page.Input.Nominal="50";page.Refresh();page.SetMatrixMode(true);
                check(ShellVerification.Descendants<TextBlock>(page).Any(t=>t.IsVisible&&t.Text==CopyText.DisplayInstructions),"Short copy instruction remains visible");
            }
            else if(pageId=="settings")
            {
                var options=ShellVerification.Descendants<Button>(window.Host).Where(b=>AutomationProperties.GetName(b).StartsWith("主题：")||AutomationProperties.GetName(b).StartsWith("小数显示（"));
                foreach(var option in options)CheckHelp(option,window,check);
                check(labels.Any(t=>t.Text=="关于")&&!labels.Any(t=>t.Text.StartsWith("图例与公差带保留")),"About retained; settings explanation moved to context help");
            }
            else
            {
                var panel=(FrameworkElement)window.Geometry.FindName("QueryPanel");
                var top=panel.TransformToAncestor(window.Host).Transform(new Point()).Y;
                check(top<2,"Geometry cards move up into removed heading space");
            }
        }

        var state=new FeedbackStatus();state.Loaded("设置文件不可用，已使用默认设置");state.Saved(true,"设置已自动保存");
        check(state.Text.Contains("已使用默认设置"),"Successful startup save preserves recovery warning");
        state.Report("已复制孔上偏差：+0.025 mm");state.Saved(false,"设置保存失败；当前会话仍可使用");
        check(state.Text.Contains("保存失败")&&state.Text.Contains("已复制孔上偏差"),"Save error and copy feedback coexist");
        state.Saved(true,"设置已自动保存");
        check(!state.Text.Contains("保存失败")&&state.Text.Contains("已复制孔上偏差"),"Save recovery clears only the save warning");

        string blocked=Path.Combine(directory,"blocked-settings-folder");File.WriteAllText(blocked,"block directory creation");
        string? copied=null;
        using(var clipboard=new ClipboardService(text=>copied=text,_=>{}))
        {
            var shell=new ShellWindow(Path.Combine(blocked,"settings.json"),clipboard);
            try
            {
                check(shell.CopyStatus.Contains("保存失败"),"Actual settings write failure remains visible");
                shell.Copy("+0.025","已复制孔上偏差：+0.025 mm");shell.SaveInputs();
                check(copied=="+0.025"&&shell.CopyStatus.Contains("保存失败")&&shell.CopyStatus.Contains("已复制孔上偏差"),"Automatic save cannot replace copy feedback");
                File.Delete(blocked);Directory.CreateDirectory(blocked);shell.SaveInputs();
                check(!shell.CopyStatus.Contains("保存失败")&&shell.CopyStatus.Contains("已复制孔上偏差"),"Actual save recovery preserves copy feedback");
                ((Button)shell.Geometry.FindName("CopyMm")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(copied=="0.01"&&shell.CopyStatus.Contains("公差值：0.01 mm"),"Geometry copy feedback reaches common footer");
                shell.Geometry.ViewModel.ParameterText="101";
                check(shell.CopyStatus.Length==0,"Query change clears stale copy feedback");
            }
            finally{shell.Close();}
        }
        string corrupt=Path.Combine(directory,"corrupt-ui-settings.json");File.WriteAllText(corrupt,"invalid JSON");
        var restored=new ShellWindow(corrupt);
        try{check(restored.CopyStatus.Contains("已使用默认设置"),"Actual corrupt settings recovery survives automatic startup saves");}
        finally{restored.Close();}
        window.Reset();window.Navigate("geometry");
        check(window.CopyStatus.Length==0,"Successful normal saves are silent");
    }

    private static void CheckHelp(Button target,ShellWindow window,Action<bool,string> check)
    {
        check(target.ToolTip is ToolTip&&AutomationProperties.GetHelpText(target).Length>0,"Same help available to mouse and accessibility");
        target.Focus();window.UpdateLayout();
        var source=PresentationSource.FromVisual(window)!;
        var open=new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.F1){RoutedEvent=Keyboard.PreviewKeyDownEvent};
        target.RaiseEvent(open);var tip=(ToolTip)target.ToolTip;
        check(open.Handled&&tip.IsOpen,"F1 opens context help");
        var close=new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent};
        target.RaiseEvent(close);check(close.Handled&&!tip.IsOpen,"Escape closes context help");
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.F1){RoutedEvent=Keyboard.PreviewKeyDownEvent});
        var navigation=window.Design.Children.OfType<Grid>().Single(g=>g.Name=="NavigationBar").Children.OfType<Button>().First();
        navigation.Focus();window.UpdateLayout();
        check(!tip.IsOpen,"Moving keyboard focus closes old help");
    }
}
