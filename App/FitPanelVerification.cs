using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tolerance.Core;

namespace Tolerance.App;

internal static class FitPanelVerification
{
    internal static void Run(ShellWindow window,string directory,Action<bool,string> check)
    {
        var page=window.SizePages["hole-fit"];window.Navigate("hole-fit");
        var cases=new[]{
            (Name:"clearance",Hole:7,Zone:"g",Shaft:6,First:.009m,Second:.050m,Labels:new[]{"最小间隙 / mm","最大间隙 / mm"}),
            (Name:"zero-clearance",Hole:8,Zone:"h",Shaft:7,First:0m,Second:.064m,Labels:new[]{"最小间隙 / mm","最大间隙 / mm"}),
            (Name:"transition",Hole:7,Zone:"k",Shaft:6,First:.023m,Second:.018m,Labels:new[]{"最大间隙 / mm","最大过盈 / mm"}),
            (Name:"interference",Hole:6,Zone:"s",Shaft:5,First:.027m,Second:.054m,Labels:new[]{"最小过盈 / mm","最大过盈 / mm"})
        };
        foreach(var sample in cases)
        {
            page.Restore(new(){HoleGrade=sample.Hole,ShaftZone=sample.Zone,ShaftGrade=sample.Shaft});
            window.UpdateLayout();
            check(page.HasResult&&page.FitRangeNumbers.Select(t=>decimal.Parse(t.Text,CultureInfo.InvariantCulture)).SequenceEqual(new[]{sample.First,sample.Second}),"Fit range headline values: "+sample.Name);
            check(page.FitRangeLabels.SequenceEqual(sample.Labels),"Fit range headline meanings: "+sample.Name);
            check(page.FitRangeNumbers.All(t=>decimal.Parse(t.Text,CultureInfo.InvariantCulture)>=0),"Overlaps shown as positive quantities: "+sample.Name);
            if(sample.Name=="zero-clearance"||sample.Name=="transition"||sample.Name=="interference")
            {
                ShellVerification.Render(window,Path.Combine(directory,"result-"+sample.Name+".png"),1);
                RenderResult(page,Path.Combine(directory,"result-panel-"+sample.Name+".png"));
            }
        }
        foreach(var theme in new[]{"light","dark"})
        {
            window.Settings.Theme=theme;window.Settings.LargeFont=true;window.Settings.DecimalPlaces=6;window.ApplySettings();
            page.Restore(new(){Nominal="50.12345678901234567890123456",HoleGrade=8,ShaftZone="h",ShaftGrade=7});window.UpdateLayout();
            check(page.HasResult,"Long decimal query remains exact");
            foreach(var number in page.ResultNumbers)
            {
                DependencyObject parent=VisualTreeHelper.GetParent(number);
                while(parent is not Viewbox)parent=VisualTreeHelper.GetParent(parent);
                var view=(Viewbox)parent;
                var bounds=number.TransformToAncestor(view).TransformBounds(new Rect(number.RenderSize));
                check(bounds.Width<=view.ActualWidth+.5&&bounds.Height<=view.ActualHeight+.5,"Full numeric string fits its view");
                check(number.Text==number.ToolTip?.ToString(),"Full number is available without truncation");
            }
            ShellVerification.Render(window,Path.Combine(directory,"result-long-"+theme+".png"),1);
            page.Restore(new(){HoleGrade=8,ShaftZone="h",ShaftGrade=7});window.UpdateLayout();
            RenderResult(page,Path.Combine(directory,"result-panel-"+theme+"-large.png"));
        }
        page.Input.Nominal="0";page.Refresh();
        check(!page.HasResult&&page.FitRangeNumbers.All(t=>t.Text=="—")&&page.FullText.Length==0,"Invalid query clears the headline and copy text");
        window.Reset();window.Navigate("geometry");
    }

    internal static void RenderResult(SizePage page,string path)
    {
        var lower=page.Children.OfType<Grid>().Single(g=>Grid.GetRow(g)==5);
        RenderElement(lower,path);
        RenderElement((FrameworkElement)lower.Children[0],path.Replace("result-panel-","result-table-"));
    }

    internal static void RenderElement(FrameworkElement element,string path)
    {
        var visual=new DrawingVisual();using(var drawing=visual.RenderOpen())
        {
            drawing.DrawRectangle(UI.Brush("CanvasBackground"),null,new Rect(0,0,element.ActualWidth,element.ActualHeight));
            drawing.DrawRectangle(new VisualBrush(element){Stretch=Stretch.Fill},null,new Rect(0,0,element.ActualWidth,element.ActualHeight));
        }
        var bitmap=new RenderTargetBitmap((int)element.ActualWidth,(int)element.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);
    }
}
