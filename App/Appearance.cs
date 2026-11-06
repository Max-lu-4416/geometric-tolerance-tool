using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tolerance.Core;
namespace Tolerance.App;

internal static class Appearance
{
    internal const double Caption=12;
    internal static void Apply(bool dark)
    {
        string[] keys=["Ink","Secondary","Muted","Accent","Hairline","Surface","CanvasBackground","ControlSurface","SelectionSurface","PrimarySurface","PrimaryInk","MatrixSurface","MatrixPreferred","MatrixSecondary","MatrixDisabled","MatrixInk","MatrixDisabledInk","MatrixBorder","Error","PlotHole","PlotShaft"];
        string[] values=dark
            ? ["#E8EDF4","#BAC5D1","#A1ADBB","#93B8FF","#3E4855","#242A32","#191D23","#2C343F","#263C59","#E1E7F0","#1C2530","#252D37","#233F32","#443D27","#20262E","#E0E7EF","#83909E","#4A5665","#FFAAA1","#245DDB","#9D5B17"]
            : ["#20252B","#505C68","#626F7C","#245DDB","#D8DEE5","#FFFFFF","#F5F6F8","#F0F2F5","#E9F0FF","#245DDB","#FFFFFF","#FFFFFF","#E5F3E5","#FFF2C7","#E6EAEE","#24303B","#66727E","#D2D9E2","#A7332C","#245DDB","#9D5B17"];
        for(int i=0;i<keys.Length;i++)Application.Current.Resources[keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i]));
    }
    internal static void MatrixState(Button button,TextBlock check,SelectionRank rank,bool selected)
    {
        button.SetResourceReference(Control.BackgroundProperty,!button.IsEnabled?"MatrixDisabled":rank==SelectionRank.Preferred?"MatrixPreferred":rank==SelectionRank.Secondary?"MatrixSecondary":"MatrixSurface");
        button.SetResourceReference(Control.ForegroundProperty,button.IsEnabled?"MatrixInk":"MatrixDisabledInk");
        button.SetResourceReference(Control.BorderBrushProperty,selected?"Accent":"MatrixBorder");
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
