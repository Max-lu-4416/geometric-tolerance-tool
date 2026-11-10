using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tolerance.Core;
namespace Tolerance.App;

internal static class Appearance
{
    internal const double Caption=12;
    private static ResourceDictionary? paletteOwner;
    private static bool? appliedDark;
    internal static bool Apply(bool dark)
    {
        var resources=Application.Current.Resources;
        if(ReferenceEquals(paletteOwner,resources)&&appliedDark==dark)return false;
        string[] keys=["Ink","Secondary","Muted","Accent","Hairline","Surface","CanvasBackground","ControlSurface","SelectionSurface","PrimarySurface","PrimaryInk","MatrixSurface","MatrixPreferred","MatrixSecondary","MatrixDisabled","MatrixInk","MatrixDisabledInk","MatrixBorder","Error","PlotHole","PlotShaft"];
        string[] values=dark
            ? ["#E6EBF0","#BAC5D1","#A1ADBB","#93B8FF","#414D5B","#252C34","#191F26","#2C343F","#263C59","#E1E7F0","#1C2530","#252D37","#233F32","#443D27","#20262E","#E0E7EF","#83909E","#4A5665","#FFAAA1","#245C91","#9D5B17"]
            : ["#202B36","#4F5E6C","#5D6C79","#245C91","#CDD5DD","#FFFFFF","#E8EDF1","#F0F3F6","#E4EEF7","#245C91","#FFFFFF","#FFFFFF","#E5EFE9","#F4EDD6","#E6EAEE","#24303B","#66727E","#C8D2DB","#A7332C","#245C91","#9D5B17"];
        for(int i=0;i<keys.Length;i++)Application.Current.Resources[keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i]));
        Application.Current.Resources["FeatureHoleInk"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark?"#93B8FF":"#245C91"));
        Application.Current.Resources["FeatureShaftInk"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark?"#F0BC84":"#9D5B17"));
        Application.Current.Resources["FocusRing"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark?"#D5E5FF":"#172C4F"));
        Application.Current.Resources["HoverSurface"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark?"#35414E":"#E6EBF0"));
        paletteOwner=resources;appliedDark=dark;
        return true;
    }
    internal static void MatrixState(Button button,TextBlock check,SelectionRank rank,bool selected)
    {
        button.SetResourceReference(Control.BackgroundProperty,!button.IsEnabled?"MatrixDisabled":rank==SelectionRank.Preferred?"MatrixPreferred":rank==SelectionRank.Secondary?"MatrixSecondary":"MatrixSurface");
        button.SetResourceReference(Control.ForegroundProperty,button.IsEnabled?"MatrixInk":"MatrixDisabledInk");
        button.SetResourceReference(Control.BorderBrushProperty,selected?"Accent":"MatrixBorder");
        SelectionVisual.SetIsSelected(button,selected);
        string status=(selected?"已选中 · ":"")+(rank==SelectionRank.Preferred?"优先":rank==SelectionRank.Secondary?"其次":button.Tag is FitPreset?"常用":"最后")+(button.IsEnabled?"":" · 当前尺寸不可用");
        System.Windows.Automation.AutomationProperties.SetItemStatus(button,status);
        button.BorderThickness=new Thickness(1);
        check.SetResourceReference(TextBlock.ForegroundProperty,"Accent");check.Visibility=selected?Visibility.Visible:Visibility.Hidden;
    }
    internal static FrameworkElement Legend(string brushKey,string label,bool outline=false)
    {
        var panel=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,12,0),VerticalAlignment=VerticalAlignment.Center};
        var swatch=new Border{Width=10,Height=10,CornerRadius=new CornerRadius(2),BorderThickness=new Thickness(1),Margin=new Thickness(0,0,6,0)};
        swatch.SetResourceReference(Border.BackgroundProperty,outline?"CanvasBackground":brushKey);swatch.SetResourceReference(Border.BorderBrushProperty,outline?"Accent":"MatrixBorder");panel.Children.Add(swatch);
        var text=UI.Text(label,Caption);text.SetResourceReference(TextBlock.ForegroundProperty,"Secondary");panel.Children.Add(text);return panel;
    }
    internal static void AlignColumn(IEnumerable<TextBlock> blocks)
    {
        var texts=blocks.ToArray();int places=texts.Select(t=>t.Text.Contains('.')?t.Text.Length-t.Text.IndexOf('.')-1:0).DefaultIfEmpty().Max();
        foreach(var t in texts)
        {
            if(!decimal.TryParse(t.Text,System.Globalization.NumberStyles.AllowLeadingSign|System.Globalization.NumberStyles.AllowDecimalPoint,System.Globalization.CultureInfo.InvariantCulture,out var value))continue;
            t.Text=(t.Text.StartsWith('+')?"+":"")+NumberDisplay.Format(value,places);
        }
    }
}
