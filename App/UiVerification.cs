using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ToleranceType = Tolerance.Core.ToleranceType;

namespace Tolerance.App;

// Application-owned WPF tests and rendering only; no desktop automation.
internal static class UiVerification
{
    public static void Run(MainWindow window, string directory)
    {
        Directory.CreateDirectory(directory);
        int checks = 0;
        void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
        void Flush() => window.Dispatcher.Invoke(() => window.UpdateLayout(), DispatcherPriority.ContextIdle);
        var vm = window.ViewModel;
        var surface = (Grid)window.FindName("DesignSurface");
        var typePanel = (ItemsControl)window.FindName("TypeGroups");
        var gradePanel = (ItemsControl)window.FindName("GradeChoices");
        var parameter = (TextBox)window.FindName("ParameterBox");
        var dimension = (TextBox)window.FindName("SizeToleranceBox");
        var image = (Image)window.FindName("ParameterDiagram");
        var copyMm = (Button)window.FindName("CopyMm");
        var copyUm = (Button)window.FindName("CopyUm");
        var copyFull = (Button)window.FindName("CopyFull");
        RadioButton[] Types() => Descendants<RadioButton>(typePanel).ToArray();
        RadioButton[] Grades() => Descendants<RadioButton>(gradePanel).ToArray();
        Rect Bounds(FrameworkElement element) => element.TransformToAncestor(surface).TransformBounds(new Rect(element.RenderSize));
        void Fits()
        {
            foreach (var element in Types().Cast<FrameworkElement>().Concat(Grades()).Append(parameter).Append(copyMm).Append(copyUm).Append(copyFull).Append(image).Concat(Descendants<TextBlock>(surface)))
            {
                if (!element.IsVisible) continue;
                var b = Bounds(element);
                Check(b.Left >= -1 && b.Top >= -1 && b.Right <= surface.ActualWidth + 1 && b.Bottom <= surface.ActualHeight + 1,
                    $"No hidden/clipped content: {element.Name} / {(element as TextBlock)?.Text} / {b}");
            }
            var error = (TextBlock)window.FindName("ErrorText");
            var rule = (TextBlock)window.FindName("RuleText");
            Check(!error.IsVisible || Bounds(error).Bottom <= Bounds(copyMm).Top, "Error and copy actions do not overlap");
            Check(Bounds(rule).Bottom <= Bounds(copyMm).Top, "Rule and copy actions do not overlap");
        }
        void SelectType(string id)
        {
            Types().Single(b => b.Tag is ToleranceType t && t.Id == id).SetCurrentValue(ToggleButton.IsCheckedProperty, true); Flush();
        }
        void SelectGrade(string code)
        {
            var b = Grades().Single(b => b.Tag is string s && s == code);
            Check(b.IsEnabled, "Selectable grade");
            b.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Flush();
        }
        Flush();
        Check(vm.Millimeters == "0.01", "Initial numeric result");
        Check(!vm.AdviceRequested, "Guidance opens after grade click");
        Check(image.IsVisible && image.Source is BitmapSource initial && initial.PixelWidth > 0, "Parameter image visible immediately");
        Check(window.Content is Viewbox && !Descendants<Expander>(window).Any(), "Fixed scaled layout without expander");
        Check(!Descendants<ScrollBar>(window).Any(b => b.IsVisible), "No visible scroll bars");
        Check(Types().Length == 12 && Types().Count(b => b.IsEnabled) == 11, "Type choices preserved");
        Check(vm.TypeGroups.Select(g => g.Choices.Length).SequenceEqual(new[] { 2, 2, 3, 5 }), "Reference grouping");
        Check(Grades().Length == 16, "Numeric plus H/K/L grades");
        foreach (var type in vm.Types)
        {
            SelectType(type.Id); SelectGrade("6");
            Check(vm.SelectedType == type && Types().Count(b => b.IsChecked == true) == 1, "Exclusive type selection");
            Check(Grades().Single(b => Equals(b.Tag, "0")).IsEnabled == (type.TableId == "2-3-23"), "Zero grade eligibility");
            Check(image.Source is BitmapSource bitmap && bitmap.PixelWidth > 0, "Every type has focused diagram");
            foreach (int grade in vm.Grades)
            {
                SelectGrade(grade.ToString());
                Check(vm.SelectedGrade == grade && vm.Result?.Grade == grade && vm.Result.Type.Id == type.Id, "Numeric button drives query");
                Check(Grades().Count(b => b.IsChecked == true) == 1, "Exclusive grade selection");
                Check(vm.AdviceRequested && vm.AdviceTitle.Contains($"{grade} 级") && vm.AdviceExamples.Length > 0, "Each click shows grade-specific guidance");
            }
            foreach (var code in new[] { "H", "K", "L" })
            {
                parameter.Text = "100"; dimension.Text = "0.05"; SelectGrade(code);
                Check(vm.IsGeneral && vm.SelectedGradeCode == code, "General grade mode");
                Check(Grades().Count(b => b.IsChecked == true) == 1, "One numeric-or-letter selection");
                bool supported = type.Id is not ("cylindricity" or "coaxiality" or "angularity" or "total-runout");
                Check(vm.HasResult == supported && copyMm.IsEnabled == supported && copyUm.IsEnabled == supported && copyFull.IsEnabled == supported, "General availability and copy eligibility");
                Check(vm.AdviceTitle.Contains(code) && vm.AdviceExamples.Length > 0, "H/K/L advice");
                if (!supported) Check(vm.Error.Length > 0 && vm.Millimeters == "—", "Unsupported general mode gives no stale value");
                Fits();
            }
        }
        SelectType("roundness"); SelectGrade("0"); parameter.Text = "3"; Flush();
        Check(vm.Millimeters == "0.0001", "Roundness grade zero");
        SelectType("straightness"); Check(vm.SelectedGradeCode == "1", "Zero clamps when changing type");
        SelectType("roundness"); SelectGrade("K"); dimension.Text = ""; Flush();
        Check(!vm.HasResult && !copyMm.IsEnabled && vm.Error.Contains("直径"), "Roundness requires diameter tolerance");
        dimension.Text = "0.05"; Flush(); Check(vm.Millimeters == "0.05" && !parameter.IsEnabled, "Roundness minimum rule and no length");
        SelectType("parallelism"); SelectGrade("H"); parameter.Text = "100"; dimension.Text = "0.25"; Flush();
        Check(vm.Millimeters == "0.25", "Parallelism maximum rule");
        dimension.Text = "1000000000000000000000000"; Flush(); Fits();
        Check(vm.HasResult, "Large exact decimal value remains readable and copyable");
        dimension.Text = "0.25"; Flush();
        foreach (var invalid in new[] { "", "0", "3001", "bad" })
        {
            parameter.Text = invalid; Flush(); Check(!vm.HasResult && !copyMm.IsEnabled, "General invalid length clears query"); Fits();
        }
        SelectType("circular-runout"); SelectGrade("L");
        Check(vm.Millimeters == "0.5" && !parameter.IsEnabled && !dimension.IsVisible, "Fixed circular runout with no inputs");
        SelectType("flatness"); SelectGrade("6"); parameter.Text = "100"; Flush();
        Check(!vm.IsGeneral && vm.Millimeters == "0.01" && !dimension.IsVisible, "Numeric mode recovers without extra field");
        Fits();
        foreach (var text in Descendants<TextBlock>(surface).Where(t => t.IsVisible))
            Check(!text.Text.Contains("手册"), "No removed source labels reintroduced");

        bool PumpUntil(Func<bool> action)
        {
            var frame = new DispatcherFrame(); bool ok = false; var deadline = DateTime.UtcNow.AddSeconds(3);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            timer.Tick += (_, _) => { try { ok = action(); } catch (ExternalException) { } if (ok || DateTime.UtcNow >= deadline) frame.Continue = false; };
            timer.Start(); Dispatcher.PushFrame(frame); timer.Stop(); return ok;
        }
        IDataObject? original = null;
        Check(PumpUntil(() => { original = Clipboard.GetDataObject(); return true; }), "Capture clipboard");
        try
        {
            copyMm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(PumpUntil(() => Clipboard.GetText() == "0.01"), "Numeric mm copy");
            copyUm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(PumpUntil(() => Clipboard.GetText() == "10"), "Numeric um copy");
            SelectGrade("K"); parameter.Text = "100"; Flush();
            copyFull.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(PumpUntil(() => Clipboard.GetText().Contains("未注公差等级：K") && Clipboard.GetText().Contains("0.2 mm = 200 μm") && !Clipboard.GetText().Contains("手册")), "General full copy");
        }
        finally { Check(PumpUntil(() => { if (original is null) Clipboard.Clear(); else Clipboard.SetDataObject(original, true); return true; }), "Restore clipboard"); }

        SelectType("flatness"); SelectGrade("6"); parameter.Text = "100"; Flush();
        Render(window, Path.Combine(directory, "ui-100.png"), 1);
        Render(window, Path.Combine(directory, "ui-150.png"), 1.5);
        SelectGrade("K"); Flush(); Render(window, Path.Combine(directory, "ui-hkl.png"), 1);
        window.Width = 1100; window.Height = 660; Flush();
        Fits(); Check(!Descendants<ScrollBar>(window).Any(b => b.IsVisible), "No scroll bars at minimum size");
        var host = (Viewbox)window.FindName("LayoutHost");
        var visibleBounds = surface.TransformToAncestor(host).TransformBounds(new Rect(surface.RenderSize));
        Check(visibleBounds.Right <= host.ActualWidth + 1 && visibleBounds.Bottom <= host.ActualHeight + 1, "Whole fixed layout fits resized window");
        Render(window, Path.Combine(directory, "ui-minimum.png"), 1);
        File.WriteAllText(Path.Combine(directory, "ui-verification.txt"), $"PASS: {checks} in-process WPF checks. Numeric and H/K/L selection, all grade guidance, 11 always-visible diagrams, conditional inputs, unsupported items, bounds, unit copies, fixed no-scroll layout and minimum window. Rendered 100%/150%, H/K/L and minimum-size previews. No computer-use APIs, desktop automation or Windows settings changes.");
    }
    private static void Render(Window window, string path, double scale)
    {
        window.UpdateLayout(); var client = (FrameworkElement)window.Content;
        var surface = (FrameworkElement)window.FindName("DesignSurface");
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            dc.DrawRectangle(window.Background, null, new Rect(0, 0, client.ActualWidth, client.ActualHeight));
            double fit = Math.Min(client.ActualWidth / surface.ActualWidth, client.ActualHeight / surface.ActualHeight);
            var dest = new Rect((client.ActualWidth - surface.ActualWidth * fit) / 2, (client.ActualHeight - surface.ActualHeight * fit) / 2, surface.ActualWidth * fit, surface.ActualHeight * fit);
            var brush = new VisualBrush(surface) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, surface.ActualWidth, surface.ActualHeight), Stretch = Stretch.Fill };
            dc.DrawRectangle(brush, null, dest);
        }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(client.ActualWidth * scale), (int)Math.Ceiling(client.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32); bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); encoder.Save(file);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); if (child is T match) yield return match; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
}
