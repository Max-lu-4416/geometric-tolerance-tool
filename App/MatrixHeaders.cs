using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tolerance.App;

internal sealed partial class SizePage
{
    private readonly List<(Border Header,TextBlock Label,int Row,int Column)> matrixHeaders=[];

    private void AddMatrixHeader(Grid grid,string value,int row,int column)
    {
        var label=UI.Text(value,12,FontWeights.Medium);
        label.TextWrapping=TextWrapping.NoWrap;
        label.HorizontalAlignment=HorizontalAlignment.Center;
        label.VerticalAlignment=VerticalAlignment.Center;
        var header=new Border{Child=label,CornerRadius=new CornerRadius(2),Margin=new Thickness(1,0,1,0)};
        SetRow(header,row);SetColumn(header,column);grid.Children.Add(header);
        matrixHeaders.Add((header,label,row,column));
    }

    private void RefreshMatrixHeaders(Button? selected)
    {
        int selectedRow=selected is null?-1:GetRow(selected),selectedColumn=selected is null?-1:GetColumn(selected);
        foreach(var (header,label,row,column) in matrixHeaders)
        {
            bool active=row==0&&column==selectedColumn||column==0&&row==selectedRow;
            header.SetResourceReference(Border.BackgroundProperty,active?"SelectionSurface":row==0?"ControlSurface":"CanvasBackground");
            label.SetResourceReference(TextBlock.ForegroundProperty,active?"Accent":"Secondary");
            // Headers are orientation aids, never copy or selection targets.
            System.Windows.Automation.AutomationProperties.SetItemStatus(header,active?"当前选择所在行列":"");
        }
    }
}
