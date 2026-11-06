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
        foreach(string theme in new[]{"light","dark","system"})foreach(bool large in new[]{false,true})
        {
            w.Settings.Theme=theme;w.Settings.LargeFont=large;w.ApplySettings();
            foreach(string page in new[]{"shaft","hole","shaft-fit","hole-fit","geometry","settings"})
            {
                w.Navigate(page);Flush();
                if(w.SizePages.TryGetValue(page,out var sizePage))
                {
                    string selected=page is "hole" or "shaft-fit"?"7":"6";
                    var grade=Descendants<Button>(sizePage).First(b=>Equals(b.Content,selected)&&b.IsEnabled);grade.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Flush();
                    foreach(var e in Descendants<TextBlock>(sizePage).Where(e=>e.IsVisible))
                    {var bounds=e.TransformToAncestor(sizePage).TransformBounds(new Rect(e.RenderSize));Check(bounds.Bottom<=700&&bounds.Right<=1180,"Page text within card: "+e.Text);}
                }
                Check(!Descendants<ScrollBar>(w).Any(s=>s.IsVisible),"No page scrolling "+page);
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
            }
        }
        w.Width=w.MinWidth;w.Height=w.MinHeight;w.Settings.LargeFont=true;w.Settings.Theme="light";w.ApplySettings();
        foreach(string page in new[]{"shaft","hole","shaft-fit","hole-fit","geometry","settings"}){w.Navigate(page);Flush();Check(!Descendants<ScrollBar>(w).Any(s=>s.IsVisible),"Minimum no scroll");Render(w,Path.Combine(directory,page+"-minimum-150.png"),1.5);}
        w.Settings.DecimalPlaces=6;w.Settings.CopyUnit="μm";w.Settings.Topmost=true;w.ApplySettings();Check(w.Topmost,"Topmost");
        w.Navigate("hole-fit");Check(w.SizePages["hole-fit"].FullText.Contains("0.009000"),"Precision setting reflects fit range");
        bool Pump(Func<bool> action)
        {
            bool ok=false;var frame=new DispatcherFrame();var deadline=DateTime.UtcNow.AddSeconds(3);var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)};
            timer.Tick+=(_,_)=>{try{ok=action();}catch(ExternalException){}if(ok||DateTime.UtcNow>=deadline)frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);timer.Stop();return ok;
        }
        IDataObject? clipboard=null;Check(Pump(()=>{clipboard=Clipboard.GetDataObject();return true;}),"Capture clipboard");
        try
        {
            var page=w.SizePages["hole-fit"];
            Button CopyButton(string name)=>Descendants<Button>(page).Single(b=>Equals(b.Content,name));
            CopyButton("复制孔 ES").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Pump(()=>Clipboard.GetText()=="25.000000"),"Um numeric copy");
            w.Settings.CopyUnit="mm";w.ApplySettings();
            CopyButton("复制轴 es").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Pump(()=>Clipboard.GetText()=="-0.009000"),"Mm signed numeric copy");
            CopyButton("复制尺寸标注").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Pump(()=>Clipboard.GetText().Contains("H7/g6")),"Dimension annotation");
            CopyButton("复制完整结果").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Pump(()=>Clipboard.GetText().Contains("0.009000 ～ 0.050000 mm")&&!Clipboard.GetText().Contains("手册")),"Complete fit copy");
        }
        finally{Check(Pump(()=>{if(clipboard is null)Clipboard.Clear();else Clipboard.SetDataObject(clipboard,true);return true;}),"Restore clipboard");}
        var restartPath=Path.Combine(directory,"restart-test.json");var restartStore=new SettingsStore(restartPath);
        var restartSettings=new ToolSettings{StartupPage="shaft-fit",LastPage="hole",Theme="dark",LargeFont=true,DecimalPlaces=4};restartSettings.Inputs["shaft-fit"]=new(){Nominal="51.25",HoleZone="G",ShaftZone="h",HoleGrade=8,ShaftGrade=7};
        Check(restartStore.Save(restartSettings),"Prepare restart");var restarted=new ShellWindow(restartPath);
        Check(restarted.ActivePage=="shaft-fit"&&restarted.SizePages["shaft-fit"].Input.Nominal=="51.25"&&restarted.Settings.LargeFont&&restarted.Settings.Theme=="dark","Restart restores page, inputs and preferences");restarted.Close();
        w.Settings.RememberInputs=false;w.ApplySettings();Check(w.Store.Load().Inputs.Count==0,"Clear remembered input");
        w.Reset();Check(!w.Topmost&&w.Settings.Theme=="light"&&NumberDisplay.MinimumDecimals==0&&w.Settings.RememberInputs,"Reset defaults");
        Check(w.SizePages["shaft-fit"].Input.HoleZone=="G"&&w.Geometry.ViewModel.SelectedGradeCode=="6","Reset inputs");
        w.Navigate("geometry");Flush();
        File.WriteAllText(Path.Combine(directory,"shell-verification.txt"),$"PASS: {checks} application-owned WPF checks. Six pages; light/dark/system; standard/large font; minimum window; 100%/150% rendering; no page scroll bars; invalid results/plot/copies; selection retention; settings, topmost, defaults and precision. No computer use, UI Automation or desktop capture APIs.");
    }
    private static void Render(ShellWindow w,string path,double scale)
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
