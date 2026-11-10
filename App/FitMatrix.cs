using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tolerance.Core;
namespace Tolerance.App;

internal sealed partial class SizePage
{
    internal sealed record MatrixCell(FitPreset Preset,Button Button,TextBlock Check);
    internal readonly List<MatrixCell> MatrixCells=[];
    private Grid? matrixPanel,freePanel;
    private Button? matrixModeButton,freeModeButton;
    private TextBlock? matrixLegend,intervalText;
    private readonly TextBlock[,] fitValues=new TextBlock[2,5];
    internal IEnumerable<TextBlock> ResultNumbers=>fitValues.Cast<TextBlock>();
    internal bool MatrixMode { get; private set; }=true;

    private (TextBox,TextBlock,TextBlock,TextBlock,TextBlock) BuildQueryLayout()
    {
        foreach(double height in new[]{52d,32d,0d,312d,8d,276d})RowDefinitions.Add(new(){Height=new GridLength(height)});
        var toolbar=new Grid();Children.Add(toolbar);
        foreach(double width in new[]{98d,124d,42d,916d})toolbar.ColumnDefinitions.Add(new(){Width=new GridLength(width)});
        var label=UI.Text("公称尺寸",14);label.VerticalAlignment=VerticalAlignment.Center;toolbar.Children.Add(label);
        var input=new TextBox{Text=Input.Nominal,Height=44,VerticalAlignment=VerticalAlignment.Top};SetColumn(input,1);toolbar.Children.Add(input);
        System.Windows.Automation.AutomationProperties.SetName(input,"公称尺寸 / mm");
        System.Windows.Automation.AutomationProperties.SetHelpText(input,"输入大于 0 且不超过 3150 的十进制尺寸；有效范围依所选公差带而定。");
        var unit=UI.Text("mm",12,margin:new Thickness(8,0,0,0));unit.VerticalAlignment=VerticalAlignment.Center;SetColumn(unit,2);toolbar.Children.Add(unit);
        var title=UI.Text("",24,FontWeights.Medium,new Thickness(20,0,0,0));title.VerticalAlignment=VerticalAlignment.Center;title.TextWrapping=TextWrapping.NoWrap;title.TextTrimming=TextTrimming.CharacterEllipsis;title.Margin=new Thickness(0);titleCopy=new(title,v=>shell.Copy(v.Text,v.SuccessMessage));titleCopy.Button.Margin=new Thickness(20,0,0,0);SetColumn(titleCopy.Button,3);toolbar.Children.Add(titleCopy.Button);
        input.TextChanged+=(_,_)=>{Input.Nominal=input.Text;Refresh();shell.SaveInputs();};
        var modes=new StackPanel{Orientation=Orientation.Horizontal};SetRow(modes,1);Children.Add(modes);
        matrixModeButton=new Button{Content=fit?"常用配合矩阵":"公差带矩阵",Padding=new Thickness(14,3,14,3),Margin=new Thickness(0,0,8,2)};
        freeModeButton=new Button{Content="自由选择",Padding=new Thickness(14,3,14,3),Margin=new Thickness(0,0,16,2)};
        modes.Children.Add(matrixModeButton);modes.Children.Add(freeModeButton);
        ContextHelp.Attach(matrixModeButton,fit
            ? "一格选择完整孔轴组合，同时选定孔、轴等级；空格为未收录常用组合。"+(basis==BasisSystem.Hole?"H8 子行用于区分轴等级。":"")+"常用配合矩阵适用尺寸不超过 500 mm，更大尺寸可使用自由选择。"
            : "一格选定代号与 IT 等级；空白为选用矩阵未列出的组合，可切换自由选择查询完整收录值。优先、其次、最后表示选用顺序；蓝框与勾号表示当前选择。");
        ContextHelp.Attach(freeModeButton,"自由选择包含全部有效收录代号和 IT1～IT18，最高至 3150 mm；不代表所有组合都有效。切换模式保留当前组合，尺寸变化时也保留选择；未收录时清空结果、图解并禁用复制。"+(fit?(basis==BasisSystem.Hole?"基孔制固定孔代号 H，孔轴等级分别选择。":"基轴制固定轴代号 h，孔轴等级分别选择。"):""));
        modes.Children.Add(Appearance.Legend("MatrixPreferred","优先"));
        if(!fit)modes.Children.Add(Appearance.Legend("MatrixSecondary","其次"));
        modes.Children.Add(Appearance.Legend("MatrixSurface",fit?"常用":"最后"));
        modes.Children.Add(Appearance.Legend("Accent","选中 ✓",true));
        modes.Children.Add(Appearance.Legend("MatrixDisabled","不可用"));
        matrixModeButton.Click+=(_,_)=>SetMatrixMode(true);freeModeButton.Click+=(_,_)=>SetMatrixMode(false);
        matrixLegend=UI.Text("",12);matrixLegend.Visibility=Visibility.Collapsed;matrixLegend.SetResourceReference(TextBlock.ForegroundProperty,"Muted");SetRow(matrixLegend,2);Children.Add(matrixLegend);
        matrixPanel=fit?BuildMatrix():BuildDeviationMatrix();SetRow(matrixPanel,3);Children.Add(matrixPanel);
        freePanel=new Grid();freePanel.ColumnDefinitions.Add(new(){Width=new GridLength(680)});freePanel.ColumnDefinitions.Add(new(){Width=new GridLength(24)});freePanel.ColumnDefinitions.Add(new());
        SetRow(freePanel,3);Children.Add(freePanel);
        var variable=new StackPanel();freePanel.Children.Add(variable);
        AddChoices(variable,fit?(basis==BasisSystem.Hole?FeatureKind.Shaft:FeatureKind.Hole):singleKind,false);
        var datum=new StackPanel();SetColumn(datum,2);freePanel.Children.Add(datum);
        if(fit)
        {
        AddChoices(datum,basis==BasisSystem.Hole?FeatureKind.Hole:FeatureKind.Shaft,true);
        }
        var lower=new Grid();lower.ColumnDefinitions.Add(new(){Width=new GridLength(780)});lower.ColumnDefinitions.Add(new(){Width=new GridLength(16)});lower.ColumnDefinitions.Add(new());SetRow(lower,5);Children.Add(lower);
        var presentation=BuildResultPanel(lower);
        MatrixMode=fit?CommonFits.For(basis).Any(MatchesInput):BandSelection.For(singleKind).Any(MatchesDeviation);UpdateMode();
        return(input,title,presentation.Summary,presentation.Notice,presentation.Advice);
    }
    private Grid BuildMatrix()
    {
        var grid=new Grid();grid.ColumnDefinitions.Add(new(){Width=new GridLength(76)});
        string[] lowerZones=["a","b","c","d","e","f","g","h","js","k","m","n","p","r","s","t","u","v","x","y","z"];
        string[] zones=lowerZones.Select(z=>basis==BasisSystem.Shaft?z.ToUpperInvariant():z).ToArray();
        foreach(var z in zones)grid.ColumnDefinitions.Add(new());
        grid.RowDefinitions.Add(new(){Height=new GridLength(24)});
        // H8 has two distinct shaft grades. Separate subrows retain full-size targets.
        (int Datum,int Secondary)[] rows=basis==BasisSystem.Shaft
            ? Enumerable.Range(5,8).Select(g=>(Datum:g,Secondary:0)).ToArray()
            : new (int Datum,int Secondary)[]{(6,5),(7,6),(8,7),(8,8),(9,9),(10,10),(11,11),(12,12)};
        foreach(var row in rows)grid.RowDefinitions.Add(new(){Height=new GridLength(36)});
        void Header(string value,int row,int col)
        {
            AddMatrixHeader(grid,value,row,col);
        }
        Header(basis==BasisSystem.Shaft?"基准轴":"基准孔",0,0);
        for(int col=0;col<zones.Length;col++)Header(zones[col],0,col+1);
        var presets=CommonFits.For(basis);
        for(int row=0;row<rows.Length;row++)
        {
            var definition=rows[row];Header(basis==BasisSystem.Shaft?$"h{definition.Datum}":definition.Datum==8?$"H8 · 轴{definition.Secondary}":$"H{definition.Datum}",row+1,0);
            for(int col=0;col<zones.Length;col++)
            {
                var p=presets.SingleOrDefault(p=>basis==BasisSystem.Shaft?p.ShaftGrade==definition.Datum&&p.HoleZone==zones[col]:p.HoleGrade==definition.Datum&&p.ShaftGrade==definition.Secondary&&p.ShaftZone==zones[col]);
                if(p is null)
                {
                    var empty=new Border{Margin=new Thickness(1),BorderThickness=new Thickness(.5)};empty.SetResourceReference(Border.BorderBrushProperty,"Hairline");SetRow(empty,row+1);SetColumn(empty,col+1);grid.Children.Add(empty);continue;
                }
                var button=new Button{Style=(Style)FindResource("MatrixButton"),Padding=new Thickness(0),Margin=new Thickness(1),Tag=p,ToolTip=$"{p.Label}\n{(p.IsPreferred?"优先配合":"常用配合")} · 适用尺寸须核对收录范围"};
                var label=new Grid{Width=42};
                var bands=new TextBlock{Text=$"{p.HoleZone}{p.HoleGrade}\n{p.ShaftZone}{p.ShaftGrade}",FontFamily=new FontFamily("Segoe UI"),FontSize=12,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.NoWrap,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};label.Children.Add(bands);
                var check=new TextBlock{Text="✓",FontSize=9,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Visibility=Visibility.Hidden};label.Children.Add(check);button.Content=label;
                System.Windows.Automation.AutomationProperties.SetName(button,$"{p.HoleZone}{p.HoleGrade}/{p.ShaftZone}{p.ShaftGrade}");
                button.Click+=(_,_)=>{if(button.IsEnabled)SelectPreset(p);};SetRow(button,row+1);SetColumn(button,col+1);grid.Children.Add(button);MatrixCells.Add(new(p,button,check));
            }
        }
        return grid;
    }
    private bool MatchesInput(FitPreset p)=>p.HoleZone==Input.HoleZone&&p.HoleGrade==Input.HoleGrade&&p.ShaftZone==Input.ShaftZone&&p.ShaftGrade==Input.ShaftGrade;
    internal void SelectPreset(FitPreset p)
    {
        Input.HoleZone=p.HoleZone;Input.HoleGrade=p.HoleGrade;Input.ShaftZone=p.ShaftZone;Input.ShaftGrade=p.ShaftGrade;
        Refresh();shell.SaveInputs();
    }
    internal void SetMatrixMode(bool matrix)
    {MatrixMode=matrix;Refresh();shell.UpdateLayout();shell.ApplyFonts(this);}
    private void UpdateMode()
    {
        matrixPanel!.Visibility=MatrixMode?Visibility.Visible:Visibility.Collapsed;freePanel!.Visibility=MatrixMode?Visibility.Collapsed:Visibility.Visible;
        matrixModeButton!.BorderBrush=UI.Brush(MatrixMode?"Accent":"Hairline");freeModeButton!.BorderBrush=UI.Brush(MatrixMode?"Hairline":"Accent");
        matrixModeButton.BorderThickness=new Thickness(1);freeModeButton.BorderThickness=new Thickness(1);
        matrixModeButton.SetResourceReference(Control.BackgroundProperty,MatrixMode?"SelectionSurface":"ControlSurface");freeModeButton.SetResourceReference(Control.BackgroundProperty,MatrixMode?"ControlSurface":"SelectionSurface");
    }
    private void SetMatrixNotice(string text)
    {
        matrixLegend!.Text=text;
        matrixLegend.Visibility=text.Length==0?Visibility.Collapsed:Visibility.Visible;
        RowDefinitions[2].Height=new GridLength(text.Length==0?0:20);
    }
    private void RefreshMatrix(decimal size,bool valid)
    {
        foreach(var cell in MatrixCells)
        {
            var p=cell.Preset;bool selected=MatchesInput(p);
            cell.Button.IsEnabled=valid&&size<=500&&catalog.Available(FeatureKind.Hole,size,p.HoleZone,p.HoleGrade)&&catalog.Available(FeatureKind.Shaft,size,p.ShaftZone,p.ShaftGrade);
            Appearance.MatrixState(cell.Button,cell.Check,p.IsPreferred?SelectionRank.Preferred:SelectionRank.Other,selected);
        }
        RefreshMatrixHeaders(MatrixCells.FirstOrDefault(c=>MatchesInput(c.Preset))?.Button);
        SetMatrixNotice(!valid?"请输入有效公称尺寸；当前选择保留。":MatrixMode&&size>500?"常用矩阵适用尺寸 ≤ 500 mm；更大尺寸请使用自由选择。":"");
        var elements=new[]{Hole,Shaft};
        var display=elements.Select(r=>r is null?Enumerable.Repeat("—",5).ToArray():new[]{S(r.UpperUm/1000m),S(r.LowerUm/1000m),F(r.ToleranceUm),F(r.MinimumMm),F(r.MaximumMm)}).ToArray();
        PresentColumns(display);
        intervalText!.Text=HasResult?(Hole!.Interval==Shaft!.Interval?$"命中尺寸段：{Hole.Interval}":$"孔：{Hole.Interval}    轴：{Shaft.Interval}"):"所选组合已保留；结果、图解及复制已清空";
        var preset=CommonFits.For(basis).FirstOrDefault(MatchesInput);
        var holeAdvice=SizeToleranceCatalog.GuidanceFor(Input.HoleGrade).Summary;var shaftAdvice=SizeToleranceCatalog.GuidanceFor(Input.ShaftGrade).Summary;
        advice.Text=!HasResult?Error:holeAdvice==shaftAdvice?$"{preset?.Use??"自由组合"} · 孔 IT{Input.HoleGrade} / 轴 IT{Input.ShaftGrade}：{holeAdvice}":$"{preset?.Use??"自由组合"} · 孔 IT{Input.HoleGrade}：{holeAdvice}；轴 IT{Input.ShaftGrade}：{shaftAdvice}";
        advice.ToolTip=$"用途：{preset?.Use??"自由组合"}\n孔 IT{Input.HoleGrade}：{SizeToleranceCatalog.GradeAdvice(Input.HoleGrade)}\n轴 IT{Input.ShaftGrade}：{SizeToleranceCatalog.GradeAdvice(Input.ShaftGrade)}\n实际配合性质以当前尺寸计算结果为准。";
        RefreshResultHeading();
        UpdateMode();
    }
}
