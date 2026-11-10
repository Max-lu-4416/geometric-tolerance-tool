using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tolerance.App;

internal static class GeometryPolishVerification
{
    internal static void Run(ShellWindow window,string directory,Action<bool,string> check)
    {
        var nav=window.Design.Children.OfType<Grid>().Single(g=>g.Name=="NavigationBar");
        string[] pages=["shaft","hole","shaft-fit","hole-fit","geometry","settings"];
        var buttons=nav.Children.OfType<Button>().ToArray();
        check(buttons.Select(b=>b.Content?.ToString()).SequenceEqual(pages.Select(ShellWindow.PageTitle)),"Navigation names and ordering preserved");
        check(Grid.GetColumn(buttons[2])-Grid.GetColumn(buttons[1])==2&&Grid.GetColumn(buttons[4])-Grid.GetColumn(buttons[3])==2,"Navigation pairs visibly grouped");
        foreach(var page in pages)
        {
            window.Navigate(page);window.UpdateLayout();
            var selected=buttons.Single(b=>Equals(b.Content,ShellWindow.PageTitle(page)));
            check(((SolidColorBrush)selected.Background).Color==((SolidColorBrush)UI.Brush("SelectionSurface")).Color,"Selected navigation surface: "+page);
            check(buttons.Count(b=>((SolidColorBrush)b.BorderBrush).Color==((SolidColorBrush)UI.Brush("Accent")).Color)==1,"One selected navigation indicator");
        }
        window.Navigate("geometry");
        var vm=window.Geometry.ViewModel;vm.SelectedType=vm.Types.Single(t=>t.Id=="perpendicularity");vm.SelectedGradeCode="7";vm.ParameterText="50";vm.ShowGradeAdvice();
        window.UpdateLayout();
        var numeric=(ItemsControl)window.Geometry.FindName("GradeChoices");var general=(ItemsControl)window.Geometry.FindName("GeneralGrades");
        check(ShellVerification.Descendants<RadioButton>(numeric).Count()==13&&ShellVerification.Descendants<RadioButton>(general).Count()==3,"Numeric grades and HKL in separate groups");
        check(vm.Millimeters=="0.03"&&vm.MicrometerValue=="30","Reference verticality example unchanged");
        var result=(FrameworkElement)window.Geometry.FindName("ResultSection");var action=(Button)window.Geometry.FindName("CopyFull");
        var rule=(TextBlock)window.Geometry.FindName("RuleText");
        var context=ShellVerification.Descendants<TextBlock>(result).Single(t=>t.Text==vm.ResultContext);
        var contentBottom=(rule.IsVisible?rule:context).TransformToAncestor(result).TransformBounds(new Rect((rule.IsVisible?rule:context).RenderSize)).Bottom;
        var actionTop=action.TransformToAncestor(result).TransformBounds(new Rect(action.RenderSize)).Top;
        check(actionTop-contentBottom<=24&&actionTop>=contentBottom,"Full copy action stays near query result");
        foreach(string theme in new[]{"light","dark"})
        {
            window.Settings.Theme=theme;window.Settings.LargeFont=theme=="dark";window.ApplySettings();window.UpdateLayout();
            FitPanelVerification.RenderElement((FrameworkElement)window.Geometry.FindName("QueryPanel"),Path.Combine(directory,"geometry-query-"+theme+".png"));
            FitPanelVerification.RenderElement(nav,Path.Combine(directory,"navigation-"+theme+".png"));
        }
        window.Reset();window.Navigate("geometry");
    }
}
