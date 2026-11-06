using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tolerance.Core;
namespace Tolerance.App;

internal sealed class SizePage : Grid
{
    private readonly ShellWindow shell;
    private readonly SizeToleranceCatalog catalog;
    internal PageInput Input { get; private set; }
    internal string PageId { get; }
    private readonly bool fit;
    private readonly FeatureKind singleKind;
    private readonly BasisSystem basis;
    private readonly TextBox nominal;
    private readonly TextBlock heading,summary,details,notice,advice;
    internal readonly BandPlot Plot=new();
    private readonly List<(Button Button,FeatureKind Kind,string? Zone,int Grade)> choices=[];
    private readonly List<Button> copyButtons=[];
    private ComboBox? presets;
    private sealed record PresetOption(string Label,FitPreset? Preset,bool Enabled)
    { public override string ToString()=>Label; }
    private bool updating;
    internal DeviationResult? Hole { get; private set; }
    internal DeviationResult? Shaft { get; private set; }
    internal FitResult? Fit { get; private set; }
    internal string Error { get; private set; }="";
    internal bool HasResult=>Hole is not null||Shaft is not null;
    internal string FullText=>!HasResult?"":$"{Heading}\r\n{summary.Text}\r\n{details.Text}\r\n{notice.Text}";
    private string Heading=>fit?$"⌀{F(Size)} {Input.HoleZone}{Input.HoleGrade}/{Input.ShaftZone}{Input.ShaftGrade}":$"⌀{F(Size)} {(singleKind==FeatureKind.Hole?Input.HoleZone+Input.HoleGrade:Input.ShaftZone+Input.ShaftGrade)}";
    private decimal Size=>TrySize(out var d)?d:0;
    private static string F(decimal d)=>NumberDisplay.Format(d);
    private static string S(decimal d)=>NumberDisplay.Signed(d);
    internal SizePage(ShellWindow shell,string id,PageInput input,SizeToleranceCatalog catalog)
    {
        this.shell=shell;PageId=id;Input=input;this.catalog=catalog;
        fit=id.EndsWith("-fit");singleKind=id=="hole"?FeatureKind.Hole:FeatureKind.Shaft;
        basis=id=="shaft-fit"?BasisSystem.Shaft:BasisSystem.Hole;
        Width=1180;Height=700;Margin=new Thickness(0);
        ColumnDefinitions.Add(new(){Width=new GridLength(420)});ColumnDefinitions.Add(new(){Width=new GridLength(16)});ColumnDefinitions.Add(new());
        var left=UI.Card();Children.Add(left);var inputPanel=new StackPanel();left.Child=inputPanel;
        inputPanel.Children.Add(UI.Text(ShellWindow.PageTitle(id),24,FontWeights.Medium));
        inputPanel.Children.Add(UI.Text("公称尺寸 / mm  ·  0 < d/D ≤ 3150",13,margin: new Thickness(0,12,0,6)));
        nominal=new(){Text=Input.Nominal,Height=44,VerticalContentAlignment=VerticalAlignment.Center};inputPanel.Children.Add(nominal);
        nominal.TextChanged+=(_,_)=>{Input.Nominal=nominal.Text;Refresh();shell.SaveInputs();};
        if(fit)
        {
            inputPanel.Children.Add(UI.Text("常用组合（≤ 500 mm）＋自由选择",13,margin:new Thickness(0,12,0,6)));
            presets=new(){Height=32,FontSize=12,DisplayMemberPath="Label"};
            var itemStyle=new Style(typeof(ComboBoxItem), (Style)FindResource(typeof(ComboBoxItem)));
            itemStyle.Setters.Add(new Setter(IsEnabledProperty,new System.Windows.Data.Binding("Enabled")));presets.ItemContainerStyle=itemStyle;inputPanel.Children.Add(presets);
            presets.SelectionChanged+=(_,_)=>
            {
                if(updating||presets.SelectedItem is not PresetOption{Preset: FitPreset p})return;
                Input.HoleZone=p.HoleZone;Input.HoleGrade=p.HoleGrade;Input.ShaftZone=p.ShaftZone;Input.ShaftGrade=p.ShaftGrade;
                advice!.Text=p.Use+"。实际性质随尺寸与等级变化，以计算结果为准。";Refresh();shell.SaveInputs();
            };
            AddChoices(inputPanel,basis==BasisSystem.Hole?FeatureKind.Shaft:FeatureKind.Hole,false);
            AddChoices(inputPanel,basis==BasisSystem.Hole?FeatureKind.Hole:FeatureKind.Shaft,true);
        }
        else AddChoices(inputPanel,singleKind,false);
        inputPanel.Children.Add(UI.Text("等级选用说明",14,FontWeights.Medium,new Thickness(0,16,0,6)));
        advice=UI.Text("点击 IT 等级查看用途。尺寸或组合无效时保留当前选择。",13);inputPanel.Children.Add(advice);
        var right=UI.Card();SetColumn(right,2);Children.Add(right);
        var result=new Grid();right.Child=result;
        foreach(var h in new[]{52d,62d,148d,280d,72d,40d})result.RowDefinitions.Add(new(){Height=new GridLength(h)});
        heading=UI.Text("",28,FontWeights.Medium);result.Children.Add(heading);
        summary=UI.Text("",20,FontWeights.Medium);SetRow(summary,1);result.Children.Add(summary);
        details=UI.Text("",14);details.LineHeight=25;SetRow(details,2);result.Children.Add(details);
        SetRow(Plot,3);result.Children.Add(Plot);
        var actions=new WrapPanel{Margin=new Thickness(0,12,0,0)};SetRow(actions,4);result.Children.Add(actions);
        if(fit){AddCopy(actions,"孔 ES",()=>CopyNumber(Hole!.UpperUm));AddCopy(actions,"孔 EI",()=>CopyNumber(Hole!.LowerUm));AddCopy(actions,"轴 es",()=>CopyNumber(Shaft!.UpperUm));AddCopy(actions,"轴 ei",()=>CopyNumber(Shaft!.LowerUm));}
        else {AddCopy(actions,"上偏差",()=>DeviationCopy(true));AddCopy(actions,"下偏差",()=>DeviationCopy(false));}
        AddCopy(actions,"尺寸标注",()=>Heading);AddCopy(actions,"完整结果",()=>FullText);
        if(!fit){AddCopy(actions,"最大尺寸",()=>F((Hole??Shaft)!.MaximumMm));AddCopy(actions,"最小尺寸",()=>F((Hole??Shaft)!.MinimumMm));}
        notice=UI.Text("",12);SetRow(notice,5);result.Children.Add(notice);
        Refresh();
    }
    private string Pair(DeviationResult r)=>$"{CopyNumber(r.UpperUm)} / {CopyNumber(r.LowerUm)}";
    private string CopyNumber(decimal um)=>F(shell.Settings.CopyUnit=="mm"?um/1000m:um);
    private string DeviationCopy(bool upper)
    {
        if(fit)return upper?$"{CopyNumber(Hole!.UpperUm)} / {CopyNumber(Shaft!.UpperUm)}":$"{CopyNumber(Hole!.LowerUm)} / {CopyNumber(Shaft!.LowerUm)}";
        var r=Hole??Shaft;return CopyNumber(upper?r!.UpperUm:r!.LowerUm);
    }
    private void AddCopy(Panel parent,string label,Func<string> value)
    {
        var b=new Button{Content="复制"+label,Padding=new Thickness(10,7,10,7),Margin=new Thickness(0,0,6,6),FontSize=12};
        b.Click+=(_,_)=>{if(HasResult)shell.Copy(value());};parent.Children.Add(b);copyButtons.Add(b);
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
                b.Click+=(_,_)=>{if(kind==FeatureKind.Hole)Input.HoleZone=zone;else Input.ShaftZone=zone;ClearPreset();Refresh();shell.SaveInputs();};
            }
            parent.Children.Add(UI.Text(name+" IT 等级",13,FontWeights.Medium,new Thickness(0,10,0,6)));
        }
        var grades=new System.Windows.Controls.Primitives.UniformGrid{Columns=9};parent.Children.Add(grades);
        foreach(int grade in Enumerable.Range(1,18))
        {
            var b=Choice(grade.ToString());grades.Children.Add(b);choices.Add((b,kind,null,grade));
            b.Click+=(_,_)=>{if(kind==FeatureKind.Hole)Input.HoleGrade=grade;else Input.ShaftGrade=grade;advice.Text=$"IT{grade}："+SizeToleranceCatalog.GradeAdvice(grade);ClearPreset();Refresh();shell.SaveInputs();};
        }
    }
    private static Button Choice(string text)=>new(){Content=text,Padding=new Thickness(2),Height=29,Margin=new Thickness(2),FontSize=13};
    private void ClearPreset(){if(presets is null)return;updating=true;presets.SelectedIndex=-1;updating=false;}
    private bool TrySize(out decimal d)
    {
        var t=(Input.Nominal??"").Trim();d=0;
        if(!Regex.IsMatch(t,@"^(?:\d+(?:\.\d*)?|\.\d+)$")||!decimal.TryParse(t,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out d)||d<=0||d>3150)return false;
        var parts=t.Split('.');var whole=parts[0].TrimStart('0');var fraction=parts.Length>1?parts[1].TrimEnd('0'):"";
        var canonical=(whole.Length==0?"0":whole)+(fraction.Length==0?"":"."+fraction);
        return canonical==NumberDisplay.Format(d,0);
    }
    internal void Refresh()
    {
        Hole=null;Shaft=null;Fit=null;Error="";
        bool valid=TrySize(out decimal d);
        foreach(var c in choices)
        {
            string zone=c.Kind==FeatureKind.Hole?Input.HoleZone:Input.ShaftZone;int grade=c.Kind==FeatureKind.Hole?Input.HoleGrade:Input.ShaftGrade;
            bool selected=c.Zone is null?c.Grade==grade:c.Zone==zone;
            c.Button.IsEnabled=valid&&(c.Zone is null?catalog.Available(c.Kind,d,zone,c.Grade):Enumerable.Range(1,18).Any(g=>catalog.Available(c.Kind,d,c.Zone,g)));
            c.Button.BorderBrush=UI.Brush(selected?"Accent":"Hairline");c.Button.BorderThickness=new Thickness(selected?2:1);
            c.Button.Foreground=UI.Brush(selected?"Accent":"Ink");
        }
        if(presets is not null)
        {
            presets.IsEnabled=valid&&d<=500;
            var options=new List<PresetOption>{new("自由选择（展开常用组合）",null,true)};
            options.AddRange(CommonFits.For(basis).Select(p=>new PresetOption(p.Label,p,valid&&d<=500&&catalog.Available(FeatureKind.Hole,d,p.HoleZone,p.HoleGrade)&&catalog.Available(FeatureKind.Shaft,d,p.ShaftZone,p.ShaftGrade))));
            updating=true;presets.ItemsSource=options;
            presets.SelectedItem=options.FirstOrDefault(o=>o.Preset is {} p&&p.HoleZone==Input.HoleZone&&p.HoleGrade==Input.HoleGrade&&p.ShaftZone==Input.ShaftZone&&p.ShaftGrade==Input.ShaftGrade)??options[0];updating=false;
        }
        try
        {
            if(!valid)throw new ArgumentException("请输入大于 0 且不超过 3150 的十进制尺寸（mm）。");
            if(fit){Fit=catalog.CalculateFit(basis,d,Input.HoleZone,Input.HoleGrade,Input.ShaftZone,Input.ShaftGrade);Hole=Fit.Hole;Shaft=Fit.Shaft;}
            else if(singleKind==FeatureKind.Hole)Hole=catalog.LookupDeviation(singleKind,d,Input.HoleZone,Input.HoleGrade);
            else Shaft=catalog.LookupDeviation(singleKind,d,Input.ShaftZone,Input.ShaftGrade);
            heading.Text=Heading;summary.Text=Fit is null?$"公差 {F((Hole??Shaft)!.ToleranceUm/1000m)} mm  =  {F((Hole??Shaft)!.ToleranceUm)} μm":$"{Fit.Nature} · {Fit.Range(F)}";
            details.Text=string.Join("\r\n",new[]{Hole,Shaft}.Where(r=>r is not null).Select(r=>$"{(r!.Kind==FeatureKind.Hole?"孔 ES / EI":"轴 es / ei")}   {S(r.UpperUm/1000m)} / {S(r.LowerUm/1000m)} mm   ({S(r.UpperUm)} / {S(r.LowerUm)} μm)\r\n极限尺寸 {F(r.MinimumMm)} ～ {F(r.MaximumMm)} mm    公差 {F(r.ToleranceUm)} μm\r\n尺寸区间 {r.Interval}"));
            details.FontSize=fit?13:15;details.LineHeight=fit?22:28;
            notice.Text=string.Join("；",new[]{Hole?.Notice,Shaft?.Notice}.Where(s=>!string.IsNullOrEmpty(s)).Distinct());
            if(notice.Text.Length==0)notice.Text=$"偏差复制单位：{shell.Settings.CopyUnit}；尺寸始终为 mm。";
        }
        catch(ArgumentException ex){Error=ex.Message;heading.Text="等待有效查询条件";summary.Text=Error;details.Text="当前选择已保留，请调整尺寸或公差带。";notice.Text="";}
        foreach(var b in copyButtons)b.IsEnabled=HasResult;
        Plot.Hole=Hole;Plot.Shaft=Shaft;Plot.InvalidateVisual();
    }
    internal void Restore(PageInput input){Input=input;nominal.Text=input.Nominal;Refresh();}
}

internal sealed class BandPlot : FrameworkElement
{
    internal DeviationResult? Hole,Shaft;
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);double w=ActualWidth,h=ActualHeight;dc.DrawRectangle(Brushes.White,new Pen(Brushes.LightGray,1),new Rect(0,0,w,h));
        void Text(string s,double x,double y,Brush? brush=null,double size=12)=>dc.DrawText(new FormattedText(s,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),size,brush??Brushes.DimGray,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(x,y));
        Text("公差带 · 端点偏差 / μm",16,12);
        var values=new[]{Hole,Shaft}.Where(r=>r is not null).SelectMany(r=>new[]{r!.LowerUm,r.UpperUm}).Append(0m).ToArray();
        if(values.Length==1){Text("有效查询后显示公差带",20,h/2);return;}
        decimal min=values.Min(),max=values.Max(),span=max-min;
        double Y(decimal v)=>48+(double)((max-v)/span)*(h-100);
        double zero=Y(0);var zeroPen=new Pen(Brushes.Gray,1){DashStyle=DashStyles.Dash};dc.DrawLine(zeroPen,new Point(24,zero),new Point(w-24,zero));Text("0",30,zero-20);
        void Band(DeviationResult? r,double x,Color color,string label)
        {
            if(r is null)return;double top=Y(r.UpperUm),bottom=Y(r.LowerUm);
            var fill=new SolidColorBrush(Color.FromArgb(65,color.R,color.G,color.B));var stroke=new SolidColorBrush(color);
            dc.DrawRectangle(fill,new Pen(stroke,1.6),new Rect(x,top,82,Math.Max(.1,bottom-top)));
            Text(NumberDisplay.Signed(r.UpperUm),x+94,top-9,stroke);Text(NumberDisplay.Signed(r.LowerUm),x+94,Math.Max(bottom-9,top+9),stroke);
            Text(label+" "+r.Band,x-2,h-30,stroke,13);
        }
        Band(Hole,w*.22,Color.FromRgb(36,93,219),"孔");Band(Shaft,w*.60,Color.FromRgb(181,103,25),"轴");
    }
}

internal static class UI
{
    internal static Brush Brush(string key)=>(Brush)Application.Current.Resources[key];
    internal static TextBlock Text(string text,double size=14,FontWeight? weight=null,Thickness? margin=null)
    {var t=new TextBlock{Text=text,FontSize=size,FontWeight=weight??FontWeights.Normal,Margin=margin??new Thickness(0),TextWrapping=TextWrapping.Wrap};t.SetResourceReference(TextBlock.ForegroundProperty,"Ink");return t;}
    internal static Border Card(){var b=new Border{Padding=new Thickness(20),CornerRadius=new CornerRadius(10),BorderThickness=new Thickness(1)};b.SetResourceReference(Border.BackgroundProperty,"Surface");b.SetResourceReference(Border.BorderBrushProperty,"Hairline");return b;}
}
