using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tolerance.Core;

namespace Tolerance.App;

internal static class IndustrialUiVerification
{
    internal static void Run(ShellWindow window,string directory,Action<bool,string> check)
    {
        foreach(string theme in new[]{"light","dark"})foreach(bool large in new[]{false,true})
        {
            window.Reset();window.Settings.Theme=theme;window.Settings.LargeFont=large;window.ApplySettings();
            foreach(var page in window.SizePages.Values)
            {
                window.Navigate(page.PageId);window.UpdateLayout();
                var before=page.FullText;
                var copies=page.NumericCopies.Select(c=>c.Value).ToArray();
                var input=ShellVerification.Descendants<TextBox>(page).Single(t=>t.IsVisible);
                var toolbar=(Grid)input.Parent;
                Rect Bounds(FrameworkElement e,Visual ancestor)=>e.TransformToAncestor(ancestor).TransformBounds(new Rect(e.RenderSize));
                var inputBounds=Bounds(input,toolbar);
                var unit=toolbar.Children.OfType<TextBlock>().Single(t=>t.Text=="mm");
                check(inputBounds.Right<=Bounds(unit,toolbar).Left,"Industrial input and unit do not overlap");
                foreach(var button in page.DeviationCells.Select(c=>c.Button).Concat(page.MatrixCells.Select(c=>c.Button)))
                    foreach(var text in ShellVerification.Descendants<TextBlock>(button).Where(t=>t.IsVisible))
                    {
                        var b=Bounds(text,button);
                        check(b.Left>=-.5&&b.Top>=-.5&&b.Right<=button.ActualWidth+.5&&b.Bottom<=button.ActualHeight+.5,$"Matrix label fits: {page.PageId}, {theme}, large={large}, text={text.Text}, bounds={b}, cell={button.RenderSize}");
                    }
                foreach(string nominal in new[]{"1","3","50","500","3150","0"})
                {
                    page.Input.Nominal=nominal;page.Refresh();window.UpdateLayout();
                    var drawing=VisualTreeHelper.GetDrawing(page.Plot);
                    check(drawing is not null&&!drawing.Bounds.IsEmpty&&drawing.Bounds.Left>=-.5&&drawing.Bounds.Top>=-.5&&drawing.Bounds.Right<=page.Plot.ActualWidth+.5&&drawing.Bounds.Bottom<=page.Plot.ActualHeight+.5,"Band plot ink stays inside viewport: "+nominal);
                    var lower=page.Children.OfType<Grid>().Single(g=>Grid.GetRow(g)==5);
                    check(Bounds(lower,page).Bottom<=page.ActualHeight+.5,"Inline notice leaves result panel inside the page: "+nominal);
                }
                page.Input.Nominal="50";page.Refresh();
                check(page.FullText==before&&page.NumericCopies.Select(c=>c.Value).SequenceEqual(copies),"Visual checks preserve complete and individual copy values");
                ShellVerification.Render(window,Path.Combine(directory,$"industrial-{page.PageId}-{theme}-{(large?"large":"standard")}.png"),1);
            }
            window.Navigate("geometry");window.UpdateLayout();
            ShellVerification.Render(window,Path.Combine(directory,$"industrial-geometry-{theme}-{(large?"large":"standard")}.png"),1);
            window.Navigate("settings");window.UpdateLayout();
            ShellVerification.Render(window,Path.Combine(directory,$"industrial-settings-{theme}-{(large?"large":"standard")}.png"),1);
        }
        window.Reset();window.Navigate("geometry");
    }
}
