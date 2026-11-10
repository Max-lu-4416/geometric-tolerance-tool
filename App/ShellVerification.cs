using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Tolerance.Core;
namespace Tolerance.App;
internal static class ShellVerification
{
    internal static void Run(ShellWindow w,string directory)
    {
        Directory.CreateDirectory(directory);int checks=0;
        void Check(bool ok,string name){checks++;if(!ok)throw new InvalidOperationException(name);}
        void Flush()=>w.Dispatcher.Invoke(()=>w.UpdateLayout(),DispatcherPriority.ContextIdle);
        Check(w.ActivePage=="geometry","First launch geometry");
        SettingsThemeVerification.Run(directory,Check);
        RefactorVerification.Run(w,directory,Check);
        FitPanelVerification.Run(w,directory,Check);
        UniformResultVerification.Run(w,directory,Check);
        ClickCopyVerification.Run(w,Check);
        GeometryPolishVerification.Run(w,directory,Check);
        InteractionPolishVerification.Run(w,directory,Check);
        TextCleanupVerification.Run(w,directory,Check);
        IndustrialUiVerification.Run(w,directory,Check);
        WindowPresentationVerification.Run(w,directory,Check);
        foreach(var p in w.SizePages.Values)
        {
            Check(p.HasResult,"Default result "+p.PageId);
            var old=p.Input.Nominal;var zone=p.Input.ShaftZone;int grade=p.Input.ShaftGrade;
            p.Input.Nominal="3150.1";p.Refresh();Check(!p.HasResult&&p.Plot.Hole is null&&p.Plot.Shaft is null,"Clear invalid result and plot");
            Check(p.Input.ShaftZone==zone&&p.Input.ShaftGrade==grade,"Keep invalid selection");
            Check(!Descendants<Button>(p).Any(b=>b.IsEnabled&&b.Content?.ToString()?.StartsWith("复制")==true),"Disable invalid copies");
            foreach(string value in new[]{"","-1","0","NaN","1e2","1,000","abc","50.00000000000000000000000000001"}){p.Input.Nominal=value;p.Refresh();Check(!p.HasResult&&p.Error.Length>0,"Invalid input "+value);}
            p.Input.Nominal=old;p.Refresh();
        }
        w.SizePages["shaft"].Input.Nominal="50.123";w.SaveInputs();
        var saved=w.Store.Load();Check(saved.Inputs["shaft"].Nominal=="50.123","Save input");w.SizePages["shaft"].Input.Nominal="50";
        foreach(var page in w.SizePages.Values.Where(p=>!p.PageId.EndsWith("-fit")))
        {
            w.Navigate(page.PageId);Flush();var kind=page.PageId=="hole"?FeatureKind.Hole:FeatureKind.Shaft;
            Check(page.DeviationCells.Count==BandSelection.For(kind).Count,"Single-band matrix complete");
            foreach(var cell in page.DeviationCells)
            {
                Check(cell.Button.IsEnabled,"All single bands valid at 50mm");cell.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Flush();
                Check((kind==FeatureKind.Hole?page.Hole:page.Shaft)?.Band==cell.Band.Band&&cell.Check.Visibility==Visibility.Visible,$"Single-band click selects zone and grade: expected {cell.Band.Band}, actual {(kind==FeatureKind.Hole?page.Hole:page.Shaft)?.Band}, check {cell.Check.Visibility}, error {page.Error}");
            }
            var initial=page.DeviationCells.Single(c=>c.Band.Band==(kind==FeatureKind.Hole?"H7":"g6"));initial.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            page.SetMatrixMode(false);page.SetMatrixMode(true);Check(page.HasResult,"Single matrix/free selection retains result");
            var limited=page.DeviationCells.Single(c=>c.Band.Band==(kind==FeatureKind.Hole?"A11":"a11"));limited.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));page.Input.Nominal="0.5";page.Refresh();
            Check(!page.HasResult&&!limited.Button.IsEnabled&&limited.Check.Visibility==Visibility.Visible&&page.Plot.Hole is null&&page.Plot.Shaft is null,"Single invalid size retains band and clears results");
            page.Input.Nominal="600";page.Refresh();Check(page.DeviationCells.Count(c=>c.Button.IsEnabled)==(kind==FeatureKind.Hole?31:41),"Large-size matrix availability");
            page.Input.Nominal="50";page.Refresh();initial.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        foreach(var page in w.SizePages.Values.Where(p=>p.PageId.EndsWith("-fit")))
        {
            w.Navigate(page.PageId);Flush();
            var basis=page.PageId=="shaft-fit"?BasisSystem.Shaft:BasisSystem.Hole;
            Check(page.MatrixCells.Count==CommonFits.For(basis).Count,"All common combinations appear once");
            Check(page.MatrixCells.Count(c=>c.Preset.IsPreferred)==13,"13 verified preferred fits per basis");
            foreach(var cell in page.MatrixCells)
            {
                Check(cell.Button.IsEnabled,"All default-size presets available");cell.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Flush();
                Check(page.Input.HoleZone==cell.Preset.HoleZone&&page.Input.HoleGrade==cell.Preset.HoleGrade&&page.Input.ShaftZone==cell.Preset.ShaftZone&&page.Input.ShaftGrade==cell.Preset.ShaftGrade,"Matrix click selects both bands");
                Check(page.HasResult&&cell.Check.Visibility==Visibility.Visible,"Matrix selection produces result and check");
            }
            var defaultCell=page.MatrixCells.Single(c=>basis==BasisSystem.Hole?c.Preset.HoleZone=="H"&&c.Preset.HoleGrade==7&&c.Preset.ShaftZone=="g"&&c.Preset.ShaftGrade==6:c.Preset.HoleZone=="G"&&c.Preset.HoleGrade==7&&c.Preset.ShaftZone=="h"&&c.Preset.ShaftGrade==6);
            defaultCell.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            foreach(var mode in new[]{false,true}){page.SetMatrixMode(mode);Flush();Check(page.HasResult&&page.Fit!.MinimumDifferenceUm==9,"Mode switch retains fit");}
            page.Input.Nominal="0";page.Refresh();Check(!page.HasResult&&page.MatrixCells.All(c=>!c.Button.IsEnabled)&&defaultCell.Check.Visibility==Visibility.Visible,"Invalid size disables matrix and retains highlight");
            page.Input.Nominal="501";page.Refresh();Check(page.MatrixCells.All(c=>!c.Button.IsEnabled),"Matrix limited to 500mm");
            page.Input.Nominal="50";page.Refresh();
            var limited=page.MatrixCells.First(c=>basis==BasisSystem.Hole?c.Preset.HoleGrade==6&&c.Preset.ShaftZone=="t":c.Preset.HoleZone=="T"&&c.Preset.ShaftGrade==5);
            limited.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));page.Input.Nominal="3";page.Refresh();
            Check(!limited.Button.IsEnabled&&!page.HasResult&&page.Plot.Hole is null&&limited.Check.Visibility==Visibility.Visible,"Missing small-size combination retains selection and clears result");
            page.Input.Nominal="50";page.Refresh();defaultCell.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(basis==BasisSystem.Hole)Check(page.MatrixCells.Any(c=>c.Preset.HoleGrade==8&&c.Preset.ShaftZone=="f"&&c.Preset.ShaftGrade==7)&&page.MatrixCells.Any(c=>c.Preset.HoleGrade==8&&c.Preset.ShaftZone=="f"&&c.Preset.ShaftGrade==8),"Distinct H8 subrows");
        }
        foreach(string theme in new[]{"light","dark","system"})foreach(bool large in new[]{false,true})
        {
            w.Settings.Theme=theme;w.Settings.LargeFont=large;w.ApplySettings();
            double Contrast(Brush foreground,Brush background)
            {
                double L(Brush brush){var c=((SolidColorBrush)brush).Color;double Channel(byte b){double x=b/255d;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}return .2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B);}
                double a=L(foreground),b=L(background);return (Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);
            }
            foreach(string fill in new[]{"MatrixSurface","MatrixPreferred","MatrixSecondary"})Check(Contrast(UI.Brush("MatrixInk"),UI.Brush(fill))>=4.5,"Matrix text contrast "+theme+fill);
            foreach(string fill in new[]{"Surface","ControlSurface","SelectionSurface","CanvasBackground"})
            {
                Check(Contrast(UI.Brush("Secondary"),UI.Brush(fill))>=4.5,"Supporting text contrast "+theme+fill);
                Check(Contrast(UI.Brush("FocusRing"),UI.Brush(fill))>=3,"Keyboard focus contrast "+theme+fill);
            }
            Check(Contrast(UI.Brush("PrimaryInk"),UI.Brush("PrimarySurface"))>=4.5,"Primary action text contrast");
            foreach(string page in new[]{"shaft","hole","shaft-fit","hole-fit","geometry","settings"})
            {
                w.Navigate(page);Flush();
                if(w.SizePages.TryGetValue(page,out var sizePage))
                {
                    string selected=page is "hole" or "shaft-fit"?"7":"6";
                    sizePage.SetMatrixMode(true);
                    if(!page.EndsWith("-fit")){var band=sizePage.DeviationCells.Single(c=>c.Band.Band==(page=="hole"?"H7":"g6"));band.Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
                    Flush();
                    var fontSample=Descendants<TextBlock>(sizePage).First(t=>t.IsVisible&&t.Text=="公称尺寸");double size=fontSample.FontSize;
                    for(int iteration=0;iteration<4;iteration++)w.ApplyFonts(w.Design);
                    Check(Math.Abs(fontSample.FontSize-size)<.0001,"Repeated font application does not compound");
                    Check(Math.Abs(size-(large?14*1.12:14))<.0001,"Stable base font scaling");
                    Check(sizePage.ResultNumbers.All(t=>t.TextAlignment==TextAlignment.Right),"Aligned numeric columns");
                    if(sizePage.Fit is not null)foreach(var role in new[]{"FeatureHoleInk","FeatureShaftInk"})
                        Check(Contrast(UI.Brush(role),UI.Brush("ControlSurface"))>=4.5,"Readable hole/shaft labels: "+role);
                    var cell=sizePage.DeviationCells.FirstOrDefault()?.Button??sizePage.MatrixCells.First().Button;
                    Check(((SolidColorBrush)cell.Foreground).Color==((SolidColorBrush)UI.Brush("MatrixInk")).Color,"Matrix text follows theme");
                    var primary=Descendants<Button>(sizePage).Single(b=>Equals(b.Content,"复制完整结果"));
                    Check(primary.Style==w.FindResource("PrimaryButton"),"Complete result is primary action");
                    var caption=Descendants<TextBlock>(primary).Single(t=>t.Text=="复制完整结果");
                    Check(Math.Abs(caption.FontSize-primary.FontSize)<.0001,"Template text inherits scaled font once");
                    decimal[] expected;
                    if(sizePage.Fit is {} f)expected=[f.Hole.UpperUm/1000m,f.Hole.LowerUm/1000m,f.Hole.ToleranceUm,f.Hole.MinimumMm,f.Hole.MaximumMm,f.Shaft.UpperUm/1000m,f.Shaft.LowerUm/1000m,f.Shaft.ToleranceUm,f.Shaft.MinimumMm,f.Shaft.MaximumMm];
                    else {var result=sizePage.Hole??sizePage.Shaft;expected=[result!.UpperUm/1000m,result.LowerUm/1000m,result.ToleranceUm/1000m,result.MinimumMm,result.MaximumMm,result.UpperUm,result.LowerUm,result.ToleranceUm];}
                    var shown=sizePage.ResultNumbers.Where(t=>!string.IsNullOrEmpty(t.Text)).Select(t=>decimal.Parse(t.Text,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    Check(shown.SequenceEqual(expected),"Aligned display retains exact numeric values");
                    foreach(var e in Descendants<TextBlock>(sizePage).Where(e=>e.IsVisible))
                    {var bounds=e.TransformToAncestor(sizePage).TransformBounds(new Rect(e.RenderSize));Check(bounds.Bottom<=700&&bounds.Right<=1180,"Page text within card: "+e.Text);}
                }
                Check(!Descendants<ScrollBar>(w).Any(s=>s.IsVisible),"No page scrolling "+page);
                if(page=="settings")UniformResultVerification.CheckAbout(w,Check);
                foreach(var e in Descendants<FrameworkElement>(w.Design).Where(e=>e.IsVisible&&(e is TextBlock||e is Button||e is TextBox)))
                {
                    var r=e.TransformToAncestor(w.Design).TransformBounds(new Rect(e.RenderSize));
                    Check(r.Left>=-1&&r.Top>=-1&&r.Right<=1181&&r.Bottom<=771,$"Outside layout {page}/{theme}/{large}: {e.GetType().Name} {(e as TextBlock)?.Text} {r}");
                    if(e is TextBlock text)
                    {
                        Check(!text.Text.Contains("手册")&&!text.Text.Contains("表 2-")&&!text.Text.Contains("2-223"),"Removed source metadata");
                        Check(!text.Text.Contains("PresetOption")&&!text.Text.Contains("FitPreset"),"No internal object representation in interface");
                    }
                }
                if(theme!="system")Render(w,Path.Combine(directory,$"{page}-{theme}-{(large?"large":"standard")}.png"),1);
                if(w.SizePages.ContainsKey(page))
                {
                    var p=w.SizePages[page];p.SetMatrixMode(false);Flush();
                    Check(!Descendants<ScrollBar>(w).Any(s=>s.IsVisible),"Free mode no scroll");
                    foreach(var e in Descendants<TextBlock>(p).Where(e=>e.IsVisible)){var b=e.TransformToAncestor(p).TransformBounds(new Rect(e.RenderSize));Check(b.Bottom<=701&&b.Right<=1181,"Free selector text within layout: "+e.Text);}
                    if(theme=="light")Render(w,Path.Combine(directory,$"{page}-free-{(large?"large":"standard")}.png"),1);
                    p.SetMatrixMode(true);
                }
            }
        }
        w.Width=w.MinWidth;w.Height=w.MinHeight;w.Settings.LargeFont=true;w.Settings.Theme="light";w.ApplySettings();
        foreach(string page in new[]{"shaft","hole","shaft-fit","hole-fit","geometry","settings"}){w.Navigate(page);Flush();Check(!Descendants<ScrollBar>(w).Any(s=>s.IsVisible),"Minimum no scroll");Render(w,Path.Combine(directory,page+"-minimum-150.png"),1.5);}
        w.Settings.DecimalPlaces=6;w.Settings.CopyUnit="μm";w.Settings.Topmost=true;w.ApplySettings();Check(w.Topmost,"Topmost");
        w.Navigate("hole-fit");Check(w.SizePages["hole-fit"].FullText.Contains("0.009000"),"Precision setting reflects fit range");
        var restartPath=Path.Combine(directory,"restart-test.json");var restartStore=new SettingsStore(restartPath);
        var restartSettings=new ToolSettings{StartupPage="shaft-fit",LastPage="hole",Theme="dark",LargeFont=true,DecimalPlaces=4};restartSettings.Inputs["shaft-fit"]=new(){Nominal="51.25",HoleZone="G",ShaftZone="h",HoleGrade=8,ShaftGrade=7};
        Check(restartStore.Save(restartSettings),"Prepare restart");var restarted=new ShellWindow(restartPath);
        Check(restarted.ActivePage=="shaft-fit"&&restarted.SizePages["shaft-fit"].Input.Nominal=="51.25"&&restarted.Settings.LargeFont&&restarted.Settings.Theme=="dark","Restart restores page, inputs and preferences");restarted.Close();
        w.Settings.RememberInputs=false;w.ApplySettings();Check(w.Store.Load().Inputs.Count==0,"Clear remembered input");
        w.Reset();Check(!w.Topmost&&w.Settings.Theme=="light"&&NumberDisplay.MinimumDecimals==0&&w.Settings.RememberInputs,"Reset defaults");
        Check(w.SizePages["shaft-fit"].Input.HoleZone=="G"&&w.Geometry.ViewModel.SelectedGradeCode=="6","Reset inputs");
        w.Navigate("geometry");Flush();
        w.Settings.LargeFont=true;w.ApplySettings();
        var geometryLarge=new MainWindow();geometryLarge.Show();geometryLarge.UpdateLayout();w.ApplyFonts(geometryLarge);geometryLarge.UpdateLayout();
        UiVerification.Run(geometryLarge,Path.Combine(directory,"geometry-large-regression"));geometryLarge.Close();
        w.Settings.LargeFont=false;w.ApplySettings();
        File.WriteAllText(Path.Combine(directory,"shell-verification.txt"),$"PASS: {checks} application-owned WPF checks. Six pages; matrix metadata and interactions; themed matrix contrast >=4.5; stable repeatable font scaling; aligned numeric columns; primary copy actions; free-mode switching; light/dark/system; standard/large font; minimum window; 100%/150% rendering; clipboard copies; settings and precision. No computer use, UI Automation or desktop capture APIs.");
    }
    internal static void Render(ShellWindow w,string path,double scale)
    {
        w.UpdateLayout();var root=(FrameworkElement)w.Content;var v=new DrawingVisual();
        using(var dc=v.RenderOpen())
        {
            dc.DrawRectangle(w.Background,null,new Rect(0,0,root.ActualWidth,root.ActualHeight));double factor=Math.Min(root.ActualWidth/1180,root.ActualHeight/770);
            var brush=new VisualBrush(w.Design){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,1180,770),Stretch=Stretch.Fill};
            dc.DrawRectangle(brush,null,new Rect((root.ActualWidth-1180*factor)/2,(root.ActualHeight-770*factor)/2,1180*factor,770*factor));
        }
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth*scale),(int)Math.Ceiling(root.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(v);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);
    }
    internal static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T t)yield return t;foreach(var nested in Descendants<T>(child))yield return nested;}}
}
