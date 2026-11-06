using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tolerance.Core;
namespace Tolerance.App;

internal sealed partial class SizePage : Grid
{
    private readonly ShellWindow shell;
    private readonly SizeToleranceCatalog catalog;
    internal PageInput Input { get; private set; }
    internal string PageId { get; }
    private readonly bool fit;
    private readonly FeatureKind singleKind;
    private readonly BasisSystem basis;
    private readonly TextBox nominal;
    private readonly TextBlock heading,summary,notice,advice;
    internal readonly BandPlot Plot=new();
    private readonly List<(Button Button,FeatureKind Kind,string? Zone,int Grade)> choices=[];
    private readonly List<Button> copyButtons=[];
    internal DeviationResult? Hole { get; private set; }
    internal DeviationResult? Shaft { get; private set; }
    internal FitResult? Fit { get; private set; }
    internal string Error { get; private set; }="";
    internal bool HasResult=>Hole is not null||Shaft is not null;
    private SizeResultText? resultText;
    private (string? Size,string Hole,int HoleGrade,string Shaft,int ShaftGrade,string Unit,int Places)? copyContext;
    internal string FullText=>resultText?.FullText??"";
    private string Heading=>resultText?.Heading??"";
    private static string F(decimal d)=>NumberDisplay.Format(d);
    private static string S(decimal d)=>NumberDisplay.Signed(d);
    internal SizePage(ShellWindow shell,string id,PageInput input,SizeToleranceCatalog catalog)
    {
        this.shell=shell;PageId=id;Input=input;this.catalog=catalog;
        fit=id.EndsWith("-fit");singleKind=id=="hole"?FeatureKind.Hole:FeatureKind.Shaft;
        basis=id=="shaft-fit"?BasisSystem.Shaft:BasisSystem.Hole;
        Width=1180;Height=700;Margin=new Thickness(0);
        (nominal,heading,summary,notice,advice)=BuildQueryLayout();
        Refresh();
    }
    private string CopyNumber(decimal um)=>F(shell.Settings.CopyUnit=="mm"?um/1000m:um);
    private string DeviationCopy(bool upper)
    {
        var r=Hole??Shaft;return CopyNumber(upper?r!.UpperUm:r!.LowerUm);
    }
    private void AddCopy(Panel parent,string label,Func<string> value)
    {
        var b=new Button{Content="复制"+label,Padding=new Thickness(10,7,10,7),Margin=new Thickness(0,0,6,6),FontSize=12};
        b.Click+=(_,_)=>{if(HasResult)shell.Copy(value());};if(label=="尺寸标注")b.Style=(Style)FindResource("PrimaryButton");parent.Children.Add(b);copyButtons.Add(b);
    }
    private void AddChoices(Panel parent,FeatureKind kind,bool fixedZone)
    {
        string name=kind==FeatureKind.Hole?"孔":"轴";
        parent.Children.Add(UI.Text(fixedZone?$"基准{name}：{(kind==FeatureKind.Hole?"H":"h")} · IT 等级":$"{name}基本偏差代号",13,FontWeights.Medium,new Thickness(0,12,0,6)));
        if(!fixedZone)
        {
            var grid=new System.Windows.Controls.Primitives.UniformGrid{Columns=7};parent.Children.Add(grid);
            foreach(string zone in catalog.Zones(kind))
            {
                var b=Choice(zone);grid.Children.Add(b);choices.Add((b,kind,zone,0));
                b.Click+=(_,_)=>{if(kind==FeatureKind.Hole)Input.HoleZone=zone;else Input.ShaftZone=zone;Refresh();shell.SaveInputs();};
            }
            parent.Children.Add(UI.Text(name+" IT 等级",13,FontWeights.Medium,new Thickness(0,10,0,6)));
        }
        var grades=new System.Windows.Controls.Primitives.UniformGrid{Columns=9};parent.Children.Add(grades);
        foreach(int grade in Enumerable.Range(1,18))
        {
            var b=Choice(grade.ToString());grades.Children.Add(b);choices.Add((b,kind,null,grade));
            b.Click+=(_,_)=>{if(kind==FeatureKind.Hole)Input.HoleGrade=grade;else Input.ShaftGrade=grade;advice.Text=$"IT{grade}："+SizeToleranceCatalog.GradeAdvice(grade);Refresh();shell.SaveInputs();};
        }
    }
    private static Button Choice(string text)=>new(){Content=text,Padding=new Thickness(2),Height=29,Margin=new Thickness(2),FontSize=14};
    private bool TrySize(out decimal d)
    {
        return ExactDecimal.TryParse(Input.Nominal,false,out d,out _) && d>0 && d<=3150;
    }
    internal void Refresh()
    {
        var context=(Input.Nominal,Input.HoleZone,Input.HoleGrade,Input.ShaftZone,Input.ShaftGrade,shell.Settings.CopyUnit,shell.Settings.DecimalPlaces);
        if(copyContext!=context)shell.CancelCopy(PageId);
        copyContext=context;
        Hole=null;Shaft=null;Fit=null;resultText=null;Error="";
        bool valid=TrySize(out decimal d);
        foreach(var c in choices)
        {
            string zone=c.Kind==FeatureKind.Hole?Input.HoleZone:Input.ShaftZone;int grade=c.Kind==FeatureKind.Hole?Input.HoleGrade:Input.ShaftGrade;
            bool selected=c.Zone is null?c.Grade==grade:c.Zone==zone;
            c.Button.IsEnabled=valid&&(c.Zone is null?catalog.Available(c.Kind,d,zone,c.Grade):Enumerable.Range(1,18).Any(g=>catalog.Available(c.Kind,d,c.Zone,g)));
            c.Button.BorderBrush=UI.Brush(selected?"Accent":"Hairline");c.Button.BorderThickness=new Thickness(selected?2:1);
            c.Button.Foreground=UI.Brush(selected?"Accent":"Ink");
        }
        try
        {
            if(!valid)throw new ArgumentException("请输入大于 0 且不超过 3150 的十进制尺寸（mm）。");
            if(fit){Fit=catalog.CalculateFit(basis,d,Input.HoleZone,Input.HoleGrade,Input.ShaftZone,Input.ShaftGrade);Hole=Fit.Hole;Shaft=Fit.Shaft;}
            else if(singleKind==FeatureKind.Hole)Hole=catalog.LookupDeviation(singleKind,d,Input.HoleZone,Input.HoleGrade);
            else Shaft=catalog.LookupDeviation(singleKind,d,Input.ShaftZone,Input.ShaftGrade);
            resultText=SizeResultText.Create(Hole,Shaft,Fit,shell.Settings.CopyUnit);
            heading.Text=resultText.Heading;summary.Text=resultText.Summary;notice.Text=resultText.Notice;
        }
        catch(ArgumentException ex){Error=ex.Message;heading.Text="等待有效查询条件";summary.Text=Error;notice.Text="";}
        if(!HasResult)shell.CancelCopy(PageId);
        foreach(var b in copyButtons)b.IsEnabled=HasResult;
        Plot.Hole=Hole;Plot.Shaft=Shaft;Plot.InvalidateVisual();
        if(fit)RefreshMatrix(d,valid);else RefreshDeviationMatrix(d,valid);
    }
    internal void Restore(PageInput input){Input=input;nominal.Text=input.Nominal;Refresh();}
}

internal sealed class BandPlot : FrameworkElement
{
    internal DeviationResult? Hole,Shaft;
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);double w=ActualWidth,h=ActualHeight;dc.DrawRectangle(Brushes.White,new Pen(Brushes.LightGray,1),new Rect(0,0,w,h));
        void Text(string s,double x,double y,Brush? brush=null,double size=12)=>dc.DrawText(new FormattedText(s,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Consolas"),size,brush??Brushes.DimGray,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(x,y));
        Text("公差带 · 端点偏差 / μm",16,12);
        var values=new[]{Hole,Shaft}.Where(r=>r is not null).SelectMany(r=>new[]{r!.LowerUm,r.UpperUm}).Append(0m).ToArray();
        if(values.Length==1){Text("有效查询后显示公差带",20,h/2);return;}
        decimal min=values.Min(),max=values.Max(),span=max-min;
        double Y(decimal v)=>48+(double)((max-v)/span)*(h-100);
        double zero=Y(0);var zeroPen=new Pen(Brushes.DarkGray,1){DashStyle=DashStyles.Dash};dc.DrawLine(zeroPen,new Point(24,zero),new Point(w-24,zero));Text("0",30,zero-20);
        double barWidth=Math.Min(82,w*.16);double labelSize=w<450?11:12;
        void Band(DeviationResult? r,double x,Color color,string label)
        {
            if(r is null)return;double top=Y(r.UpperUm),bottom=Y(r.LowerUm);
            var fill=new SolidColorBrush(Color.FromArgb(65,color.R,color.G,color.B));var stroke=new SolidColorBrush(color);
            dc.DrawRectangle(fill,new Pen(stroke,1.6),new Rect(x,top,barWidth,Math.Max(.1,bottom-top)));
            Text(NumberDisplay.Signed(r.UpperUm),x+barWidth+10,top-9,stroke,labelSize);Text(NumberDisplay.Signed(r.LowerUm),x+barWidth+10,Math.Max(bottom-9,top+9),stroke,labelSize);
            Text(label+" "+r.Band,x-2,h-30,stroke,13);
        }
        Band(Hole,w*(w<450?.12:.22),((SolidColorBrush)UI.Brush("PlotHole")).Color,"孔");Band(Shaft,w*(w<450?.55:.60),((SolidColorBrush)UI.Brush("PlotShaft")).Color,"轴");
    }
}

internal static class UI
{
    internal static Brush Brush(string key)=>(Brush)Application.Current.Resources[key];
    internal static TextBlock Text(string text,double size=14,FontWeight? weight=null,Thickness? margin=null)
    {var t=new TextBlock{Text=text,FontSize=size,FontWeight=weight??FontWeights.Normal,Margin=margin??new Thickness(0),TextWrapping=TextWrapping.Wrap};t.SetResourceReference(TextBlock.ForegroundProperty,"Ink");return t;}
    internal static Border Card(){var b=new Border{Padding=new Thickness(20),CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(1)};b.SetResourceReference(Border.BackgroundProperty,"Surface");b.SetResourceReference(Border.BorderBrushProperty,"Hairline");return b;}
}
