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
    private readonly Dictionary<TextBlock,ClickCopy> copyTargets=[];
    private ClickCopy? titleCopy;
    internal IEnumerable<ClickCopy> NumericCopies=>copyTargets.Values;
    internal ClickCopy TitleCopy=>titleCopy!;
    private ClickCopy Target(TextBlock text)
    {
        if(!copyTargets.TryGetValue(text,out var target)){target=new(text,v=>shell.Copy(v.Text,v.SuccessMessage));copyTargets.Add(text,target);}
        return target;
    }
    private void PresentColumns(string[][] values)
    {
        string[] fields=["上偏差","下偏差","公差","最小尺寸","最大尺寸"];
        for(int col=0;col<5;col++)
        {
            var aligned=CopyValue.Align(new[]{values[0][col],values[1][col]});
            for(int row=0;row<2;row++)
            {
                string unit=fit?(col==2?"μm":"mm"):row==0||col>=3?"mm":"μm";
                string feature=fit?(row==0?"孔":"轴"):singleKind==FeatureKind.Hole?"孔":"轴";
                Target(fitValues[row,col]).Update(new(aligned[row],feature+fields[col],unit,HasResult));
            }
        }
    }
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
    private void AddCopy(Panel parent,string label,Func<string> value)
    {
        var b=new Button{Content="复制"+label,Padding=new Thickness(10,7,10,7),Margin=new Thickness(0,0,6,6),FontSize=12};
        b.Click+=(_,_)=>{if(HasResult)shell.Copy(value(),"已复制完整结果。");};if(label=="完整结果")b.Style=(Style)FindResource("PrimaryButton");parent.Children.Add(b);copyButtons.Add(b);
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
            c.Button.SetResourceReference(Control.BorderBrushProperty,selected?"Accent":"Hairline");c.Button.BorderThickness=new Thickness(selected?2:1);
            c.Button.SetResourceReference(Control.ForegroundProperty,selected?"Accent":"Ink");
        }
        try
        {
            if(!valid)throw new ArgumentException("请输入大于 0 且不超过 3150 的十进制尺寸（mm）。");
            if(fit){Fit=catalog.CalculateFit(basis,d,Input.HoleZone,Input.HoleGrade,Input.ShaftZone,Input.ShaftGrade);Hole=Fit.Hole;Shaft=Fit.Shaft;}
            else if(singleKind==FeatureKind.Hole)Hole=catalog.LookupDeviation(singleKind,d,Input.HoleZone,Input.HoleGrade);
            else Shaft=catalog.LookupDeviation(singleKind,d,Input.ShaftZone,Input.ShaftGrade);
            resultText=SizeResultText.Create(Hole,Shaft,Fit);
            heading.Text=resultText.Heading;summary.Text=resultText.Summary;notice.Text=resultText.Notice;
        }
        catch(ArgumentException ex){Error=ex.Message;heading.Text="等待有效查询条件";summary.Text=Error;notice.Text="";}
        titleCopy!.Update(new(resultText?.Heading??"等待有效查询条件","尺寸标注","",HasResult));
        if(!HasResult)shell.CancelCopy(PageId);
        foreach(var b in copyButtons)b.IsEnabled=HasResult;
        Plot.Hole=Hole;Plot.Shaft=Shaft;Plot.InvalidateVisual();
        if(fit)RefreshMatrix(d,valid);else RefreshDeviationMatrix(d,valid);
        if(notice.Text==CopyText.WithTitle)notice.Text="";
    }
    internal void Restore(PageInput input){Input=input;nominal.Text=input.Nominal;Refresh();}
    internal void RefreshAppearance()=>Plot.InvalidateVisual();
}

internal sealed class BandPlot : FrameworkElement
{
    internal DeviationResult? Hole,Shaft;
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);double w=ActualWidth,h=ActualHeight;
        if(w<=0||h<=0)return;
        var ink=new SolidColorBrush(Color.FromRgb(65,80,94));var rule=new Pen(new SolidColorBrush(Color.FromRgb(222,229,235)),1);
        dc.DrawRectangle(Brushes.White,null,new Rect(0,0,w,h));
        void Text(string s,double x,double y,Brush? brush=null,double size=12)=>dc.DrawText(new FormattedText(s,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Consolas, Microsoft YaHei UI"),size,brush??ink,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(x,y));
        Text("公差带",4,2,ink,14);Text("端点偏差 / μm",w-112,4,ink,11);
        dc.DrawLine(rule,new Point(4,28),new Point(w-4,28));
        var values=new[]{Hole,Shaft}.Where(r=>r is not null).SelectMany(r=>new[]{r!.LowerUm,r.UpperUm}).Append(0m).ToArray();
        if(values.Length==1){Text("有效查询后显示公差带",20,h/2);return;}
        decimal min=values.Min(),max=values.Max(),span=max-min;
        double Y(decimal v)=>50+(double)((max-v)/span)*(h-100);
        // A shared linear scale for both bands, with the zero datum always in range.
        for(int i=0;i<=4;i++){double y=50+i*(h-100)/4;dc.DrawLine(rule,new Point(28,y),new Point(w-8,y));}
        dc.DrawLine(rule,new Point(28,42),new Point(28,h-42));
        double zero=Y(0);var zeroPen=new Pen(new SolidColorBrush(Color.FromRgb(108,122,135)),1){DashStyle=DashStyles.Dash};dc.DrawLine(zeroPen,new Point(28,zero),new Point(w-8,zero));Text("0",8,zero-8);
        double barWidth=Math.Min(58,w*.145);double labelSize=11;
        void Band(DeviationResult? r,double x,Color color,string label)
        {
            if(r is null)return;double top=Y(r.UpperUm),bottom=Y(r.LowerUm);
            var fill=new SolidColorBrush(Color.FromArgb(38,color.R,color.G,color.B));var stroke=new SolidColorBrush(color);var outline=new Pen(stroke,1.4);
            dc.DrawRectangle(fill,outline,new Rect(x,top,barWidth,Math.Max(.1,bottom-top)));
            dc.DrawLine(outline,new Point(x+barWidth,top),new Point(x+barWidth+6,top));dc.DrawLine(outline,new Point(x+barWidth,bottom),new Point(x+barWidth+6,bottom));
            Text(NumberDisplay.Signed(r.UpperUm),x+barWidth+9,top-9,stroke,labelSize);Text(NumberDisplay.Signed(r.LowerUm),x+barWidth+9,Math.Max(bottom-9,top+9),stroke,labelSize);
            Text(label+" "+r.Band,x-2,h-28,stroke,13);
        }
        bool pair=Hole is not null&&Shaft is not null;
        Band(Hole,w*(pair?.16:.36),((SolidColorBrush)UI.Brush("PlotHole")).Color,"孔");Band(Shaft,w*(pair?.58:.36),((SolidColorBrush)UI.Brush("PlotShaft")).Color,"轴");
    }
}

internal static class UI
{
    internal static Brush Brush(string key)=>(Brush)Application.Current.Resources[key];
    internal static TextBlock Text(string text,double size=14,FontWeight? weight=null,Thickness? margin=null)
    {var t=new TextBlock{Text=text,FontSize=size,FontWeight=weight??FontWeights.Normal,Margin=margin??new Thickness(0),TextWrapping=TextWrapping.Wrap};t.SetResourceReference(TextBlock.ForegroundProperty,"Ink");return t;}
    internal static FrameworkElement SectionHeading(string title,string? detail=null,Thickness? margin=null)
    {
        var grid=new Grid{Margin=margin??new Thickness(0)};
        grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});grid.ColumnDefinitions.Add(new());
        var text=Text(title,14,FontWeights.Medium);grid.Children.Add(text);
        var line=new Border{Height=1,Margin=new Thickness(12,0,0,0),VerticalAlignment=VerticalAlignment.Center};line.SetResourceReference(Border.BackgroundProperty,"Hairline");Grid.SetColumn(line,1);grid.Children.Add(line);
        if(detail is not null){grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=GridLength.Auto});var caption=Text(detail,12,margin:new Thickness(0,6,0,0));caption.SetResourceReference(TextBlock.ForegroundProperty,"Secondary");Grid.SetRow(caption,1);Grid.SetColumnSpan(caption,2);grid.Children.Add(caption);}
        return grid;
    }
    internal static Border Card(){var b=new Border{Padding=new Thickness(16),CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(1)};b.SetResourceReference(Border.CornerRadiusProperty,"PanelCornerRadius");b.SetResourceReference(Border.BackgroundProperty,"Surface");b.SetResourceReference(Border.BorderBrushProperty,"Hairline");return b;}
}
