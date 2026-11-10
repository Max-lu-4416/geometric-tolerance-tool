using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Data;

namespace Tolerance.App;

internal static class ContextHelp
{
    private static ToolTip? active;

    internal static void Attach(FrameworkElement target,string text)
    {
        var label=UI.Text(text+"\nF1 查看说明 · Esc 关闭",13);
        label.SetBinding(TextBlock.FontSizeProperty,new Binding("FontSize"){Source=target});
        var tip=new ToolTip{Content=label,MaxWidth=440,PlacementTarget=target,Placement=PlacementMode.Bottom,Focusable=false};
        tip.SetResourceReference(Control.BackgroundProperty,"Surface");
        tip.SetResourceReference(Control.BorderBrushProperty,"Hairline");
        target.ToolTip=tip;
        AutomationProperties.SetHelpText(target,text);
        target.PreviewKeyDown+=(_,e)=>
        {
            if(e.Key==Key.F1){Close();active=tip;tip.IsOpen=true;e.Handled=true;}
            else if(e.Key==Key.Escape&&tip.IsOpen){tip.IsOpen=false;if(active==tip)active=null;e.Handled=true;}
        };
        target.LostKeyboardFocus+=(_,_)=>{tip.IsOpen=false;if(active==tip)active=null;};
        target.PreviewMouseDown+=(_,_)=>{tip.IsOpen=false;if(active==tip)active=null;};
        target.Unloaded+=(_,_)=>{tip.IsOpen=false;if(active==tip)active=null;};
        tip.Opened+=(_,_)=>{if(active!=tip)Close();active=tip;};
        tip.Closed+=(_,_)=>{if(active==tip)active=null;};
    }

    internal static bool Close(){var previous=active;active=null;bool open=previous?.IsOpen==true;if(previous is not null)previous.IsOpen=false;return open;}
}
