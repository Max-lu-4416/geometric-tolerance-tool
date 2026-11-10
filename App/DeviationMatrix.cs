using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tolerance.Core;
namespace Tolerance.App;

internal sealed partial class SizePage
{
    internal sealed record DeviationCell(BandChoice Band,Button Button,TextBlock Check);
    internal readonly List<DeviationCell> DeviationCells=[];
    private Grid BuildDeviationMatrix()
    {
        var grid=new Grid();grid.ColumnDefinitions.Add(new(){Width=new GridLength(76)});
        string[] order=["a","b","c","d","e","f","g","h","j","js","k","m","n","p","r","s","t","u","v","x","y","z"];
        string[] zones=order.Select(z=>singleKind==FeatureKind.Hole?z.ToUpperInvariant():z).ToArray();
        foreach(var zone in zones)grid.ColumnDefinitions.Add(new());
        grid.RowDefinitions.Add(new(){Height=new GridLength(24)});
        foreach(int grade in Enumerable.Range(1,13))grid.RowDefinitions.Add(new(){Height=new GridLength(22)});
        void Label(string value,int row,int col)
        {
            AddMatrixHeader(grid,value,row,col);
        }
        Label("IT 等级",0,0);for(int col=0;col<zones.Length;col++)Label(zones[col],0,col+1);
        var bands=BandSelection.For(singleKind);
        for(int grade=1;grade<=13;grade++)
        {
            Label(grade.ToString(),grade,0);
            for(int col=0;col<zones.Length;col++)
            {
                var band=bands.SingleOrDefault(b=>b.Zone==zones[col]&&b.Grade==grade);
                if(band is null)continue;
                var button=new Button{Style=(Style)FindResource("MatrixButton"),Padding=new Thickness(0),Margin=new Thickness(1,0,1,0),Tag=band,ToolTip=$"{band.Band} · 点击查看 IT{grade} 选用说明"};
                var content=new Grid{Width=42};
                content.Children.Add(new TextBlock{Text=band.Band,FontFamily=new FontFamily("Segoe UI"),FontSize=12,TextWrapping=TextWrapping.NoWrap,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});
                var check=new TextBlock{Text="✓",FontSize=8,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Visibility=Visibility.Hidden};content.Children.Add(check);button.Content=content;
                System.Windows.Automation.AutomationProperties.SetName(button,band.Band);
                button.Click+=(_,_)=>{if(button.IsEnabled)SelectDeviation(band);};
                SetRow(button,grade);SetColumn(button,col+1);grid.Children.Add(button);DeviationCells.Add(new(band,button,check));
            }
        }
        return grid;
    }
    private bool MatchesDeviation(BandChoice b)=>singleKind==FeatureKind.Hole?Input.HoleZone==b.Zone&&Input.HoleGrade==b.Grade:Input.ShaftZone==b.Zone&&Input.ShaftGrade==b.Grade;
    private void SelectDeviation(BandChoice band)
    {
        if(singleKind==FeatureKind.Hole){Input.HoleZone=band.Zone;Input.HoleGrade=band.Grade;}
        else {Input.ShaftZone=band.Zone;Input.ShaftGrade=band.Grade;}
        Refresh();shell.SaveInputs();
    }
    private void RefreshDeviationMatrix(decimal size,bool valid)
    {
        foreach(var cell in DeviationCells)
        {
            bool selected=MatchesDeviation(cell.Band);
            cell.Button.IsEnabled=valid&&BandSelection.RecommendedAt(singleKind,size,cell.Band)&&catalog.Available(singleKind,size,cell.Band.Zone,cell.Band.Grade);
            var rank=BandSelection.RankAt(size,cell.Band);
            Appearance.MatrixState(cell.Button,cell.Check,rank,selected);
        }
        RefreshMatrixHeaders(DeviationCells.FirstOrDefault(c=>MatchesDeviation(c.Band))?.Button);
        SetMatrixNotice(!valid?"请输入有效尺寸，当前公差带选择保留。":MatrixMode&&size>500?"500 < 尺寸 ≤ 3150 mm：使用大尺寸常用公差带范围；优先/其次/最后的三级顺序适用于尺寸 ≤ 500 mm。":"");
        var r=Hole??Shaft;
        if(r is null)
        {
            PresentColumns([Enumerable.Repeat("—",5).ToArray(),Enumerable.Repeat("—",5).ToArray()]);
            intervalText!.Text="结果、图解及复制已清空；所选公差带仍保留。";
        }
        else
        {
            string[][] values=[[S(r.UpperUm/1000m),S(r.LowerUm/1000m),F(r.ToleranceUm/1000m),F(r.MinimumMm),F(r.MaximumMm)],[S(r.UpperUm),S(r.LowerUm),F(r.ToleranceUm),"",""]];
            PresentColumns(values);
            intervalText!.Text=$"命中尺寸区间：{r.Interval}";
        }
        int grade=singleKind==FeatureKind.Hole?Input.HoleGrade:Input.ShaftGrade;
        advice.Text=HasResult?$"IT{grade}：{SizeToleranceCatalog.GradeAdvice(grade)}":Error;advice.ToolTip=advice.Text;
        RefreshResultHeading();
        UpdateMode();
    }
}
