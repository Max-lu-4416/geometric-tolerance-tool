using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Tolerance.App;

internal static class InteractionPolishVerification
{
    internal static void Run(ShellWindow window,string directory,Action<bool,string> check)
    {
        foreach(string theme in new[]{"light","dark"})foreach(bool large in new[]{false,true})
        {
            window.Settings.Theme=theme;window.Settings.LargeFont=large;window.ApplySettings();
            foreach(var page in window.SizePages.Values)
            {
                window.Navigate(page.PageId);page.SetMatrixMode(true);window.UpdateLayout();
                var matrix=page.Children.OfType<Grid>().Single(g=>Grid.GetRow(g)==3&&g.ColumnDefinitions.Count>3);
                var buttons=page.DeviationCells.Select(c=>c.Button).Concat(page.MatrixCells.Select(c=>c.Button)).ToArray();
                var selected=buttons.Single(SelectionVisual.GetIsSelected);
                var headers=matrix.Children.OfType<Border>().Where(b=>Grid.GetRow(b)==0||Grid.GetColumn(b)==0).ToArray();
                foreach(var header in headers)
                {
                    bool active=Grid.GetRow(header)==0&&Grid.GetColumn(header)==Grid.GetColumn(selected)||Grid.GetColumn(header)==0&&Grid.GetRow(header)==Grid.GetRow(selected);
                    check((AutomationProperties.GetItemStatus(header).Length>0)==active,"Matrix orientation follows exact row/column: "+page.PageId);
                    if(active)check(((SolidColorBrush)header.Background).Color==((SolidColorBrush)UI.Brush("SelectionSurface")).Color,"Active matrix header uses theme token");
                }
                foreach(var button in buttons)
                {
                    check(button.FocusVisualStyle==window.FindResource("KeyboardFocus"),"Matrix retains a separate keyboard focus indicator");
                    check(AutomationProperties.GetItemStatus(button).Contains("已选中")==SelectionVisual.GetIsSelected(button),"Selection announced independently of rank colour");
                }
                selected.ApplyTemplate();
                var outline=(Border)selected.Template.FindName("Selection",selected);
                check(outline.Visibility==Visibility.Visible&&outline.BorderThickness==new Thickness(2),"Selected matrix cell has a strong inset outline");
                var other=buttons.First(b=>b.IsEnabled&&b!=selected);other.Focus();window.UpdateLayout();
                check(other.IsKeyboardFocused&&!SelectionVisual.GetIsSelected(other)&&SelectionVisual.GetIsSelected(selected),"Keyboard focus never changes the query selection");
                var otherOutline=(Border)other.Template.FindName("Selection",other);
                check(otherOutline.Visibility==Visibility.Collapsed,"Focused unselected cell has no selection outline");
                var before=selected.RenderSize;selected.Focus();window.UpdateLayout();
                check(selected.RenderSize==before,"Focus has no layout shift");
                page.Input.Nominal="0";page.Refresh();window.UpdateLayout();
                check(SelectionVisual.GetIsSelected(selected)&&!selected.IsEnabled&&selected.Cursor==Cursors.Arrow,"Invalid size retains selection, disables click and hand cursor");
                check(headers.Count(h=>AutomationProperties.GetItemStatus(h).Length>0)==2,"Invalid size preserves orientation cues");
                page.Input.Nominal="50";page.Refresh();
                var input=ShellVerification.Descendants<TextBox>(page).Single(t=>t.IsVisible);
                check(AutomationProperties.GetName(input)=="公称尺寸 / mm","Nominal input exposes its meaning and unit");
                input.Focus();window.UpdateLayout();
                var character=input.GetRectFromCharacterIndex(0);
                check(!character.IsEmpty&&character.Top>=0&&character.Bottom<=input.ActualHeight,"Nominal input glyph fits the field without clipping");
                check(((Border)input.Template.FindName("FieldFocus",input)).Visibility==Visibility.Visible,"Input focus visible in both themes");
                check(input.FontFamily.Source=="Consolas","Input and result use compatible numeric typography");
                var full=ShellVerification.Descendants<Button>(page).Single(b=>Equals(b.Content,"复制完整结果"));
                check(full.FocusVisualStyle==window.FindResource("PrimaryKeyboardFocus"),"Primary action focus uses contrasting ink");
                full.Focus();window.UpdateLayout();
                check(((Border)input.Template.FindName("FieldFocus",input)).Visibility==Visibility.Collapsed,"Input focus clears on leaving the field");
                if(!large)FitPanelVerification.RenderElement(matrix,Path.Combine(directory,$"matrix-headers-{page.PageId}-{theme}.png"));
            }
            window.Navigate("settings");window.UpdateLayout();
            var settingsButtons=ShellVerification.Descendants<Button>(window.Host).Where(b=>AutomationProperties.GetName(b).Contains('：')).ToArray();
            check(settingsButtons.Length>10&&settingsButtons.Count(b=>AutomationProperties.GetItemStatus(b)=="已选中")==6,"Each settings group exposes one selected option");
            window.Navigate("geometry");window.UpdateLayout();
            var parameter=(TextBox)window.Geometry.FindName("ParameterBox");
            var parameterCharacter=parameter.GetRectFromCharacterIndex(0);
            check(!parameterCharacter.IsEmpty&&parameterCharacter.Top>=0&&parameterCharacter.Bottom<=parameter.ActualHeight,"Geometry parameter glyph fits the compact field");
        }
        window.Reset();window.Navigate("geometry");
    }
}
