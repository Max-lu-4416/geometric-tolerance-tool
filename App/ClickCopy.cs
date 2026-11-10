using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Tolerance.Core;

namespace Tolerance.App;

internal sealed record CopyValue(string Text,string Name,string Unit,bool Enabled=true)
{
    internal bool CanCopy=>Enabled&&!string.IsNullOrWhiteSpace(Text)&&Text!="—";
    internal string SuccessMessage=>$"已复制{Name}：{Text}{(Unit.Length==0?"":" "+Unit)}";
    internal static string[] Align(string[] values)
    {
        int places=values.Select(t=>t.Contains('.')?t.Length-t.IndexOf('.')-1:0).DefaultIfEmpty().Max();
        return values.Select(t=>decimal.TryParse(t,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var n)
            ?(t.StartsWith('+')?"+":"")+NumberDisplay.Format(n,places):t).ToArray();
    }
}

internal static class CopyText
{
    internal const string DisplayInstructions="点击数值复制，不含单位";
    internal const string Instructions="点击数值复制，不含单位，单位以显示为准。";
    internal const string TitleInstructions="点击尺寸标题复制标注。";
    internal const string WithTitle=Instructions+TitleInstructions;
}

// Text-sized targets: no cell-wide hit area or permanent button chrome.
internal sealed class ClickCopy
{
    internal Button Button { get; }
    internal TextBlock Label { get; }
    internal CopyValue Value { get; private set; }=new("—","","",false);
    internal ClickCopy(TextBlock label,Action<CopyValue> copy,Button? button=null)
    {
        Label=label;
        Button=button??new Button{Content=label};
        Button.Style=(Style)Application.Current.FindResource("ClickCopyButton");
        Button.Click+=(_,_)=>{if(Button.IsEnabled&&Value.CanCopy)copy(Value);};
        void Decoration()=>Label.TextDecorations=Button.IsEnabled&&(Button.IsMouseOver||Button.IsKeyboardFocused)?TextDecorations.Underline:null;
        Button.MouseEnter+=(_,_)=>Decoration();Button.MouseLeave+=(_,_)=>Decoration();
        Button.GotKeyboardFocus+=(_,_)=>Decoration();Button.LostKeyboardFocus+=(_,_)=>Decoration();
        Button.IsEnabledChanged+=(_,_)=>Decoration();
        // WPF Button handles Space; explicitly support Enter without a default button.
        Button.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter&&Button.IsEnabled){Button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));e.Handled=true;}};
    }
    internal void Update(CopyValue value)
    {
        Value=value;Label.SetCurrentValue(TextBlock.TextProperty,value.Text);Button.IsEnabled=value.CanCopy;
        Button.ToolTip=value.CanCopy?$"点击复制{value.Name}：{value.Text}{(value.Unit.Length==0?"":" "+value.Unit)}":null;
        System.Windows.Automation.AutomationProperties.SetName(Button,value.Name+(value.Unit.Length==0?"":" / "+value.Unit));
    }
}
