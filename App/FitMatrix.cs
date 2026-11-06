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
        foreach(double height in new[]{52d,32d,20d,344d,8d,244d})RowDefinitions.Add(new(){Height=new GridLength(height)});
        var toolbar=new Grid();Children.Add(toolbar);
        foreach(double width in new[]{174d,98d,124d,42d,742d})toolbar.ColumnDefinitions.Add(new(){Width=new GridLength(width)});
        toolbar.Children.Add(UI.Text(ShellWindow.PageTitle(PageId),24,FontWeights.Medium));
        var label=UI.Text("公称尺寸",14);label.VerticalAlignment=VerticalAlignment.Center;SetColumn(label,1);toolbar.Children.Add(label);
        var input=new TextBox{Text=Input.Nominal,Height=44,VerticalAlignment=VerticalAlignment.Top};SetColumn(input,2);toolbar.Children.Add(input);
        var unit=UI.Text("mm",12,margin:new Thickness(8,0,0,0));unit.VerticalAlignment=VerticalAlignment.Center;SetColumn(unit,3);toolbar.Children.Add(unit);
        var title=UI.Text("",24,FontWeights.Medium,new Thickness(20,0,0,0));title.VerticalAlignment=VerticalAlignment.Center;title.TextWrapping=TextWrapping.NoWrap;title.TextTrimming=TextTrimming.CharacterEllipsis;SetColumn(title,4);toolbar.Children.Add(title);
        input.TextChanged+=(_,_)=>{Input.Nominal=input.Text;Refresh();shell.SaveInputs();};
        var modes=new StackPanel{Orientation=Orientation.Horizontal};SetRow(modes,1);Children.Add(modes);
        matrixModeButton=new Button{Content=fit?"常用配合矩阵":"公差带矩阵",Padding=new Thickness(14,3,14,3),Margin=new Thickness(0,0,8,2)};
        freeModeButton=new Button{Content="自由选择",Padding=new Thickness(14,3,14,3),Margin=new Thickness(0,0,16,2)};
        modes.Children.Add(matrixModeButton);modes.Children.Add(freeModeButton);
        modes.Children.Add(Appearance.Legend("MatrixPreferred","优先"));
        if(!fit)modes.Children.Add(Appearance.Legend("MatrixSecondary","其次"));
        modes.Children.Add(Appearance.Legend("MatrixSurface",fit?"常用":"最后"));
        modes.Children.Add(Appearance.Legend("Accent","选中 ✓",true));
        modes.Children.Add(Appearance.Legend("MatrixDisabled","不可用"));
        matrixModeButton.Click+=(_,_)=>SetMatrixMode(true);freeModeButton.Click+=(_,_)=>SetMatrixMode(false);
        matrixLegend=UI.Text("",12);matrixLegend.SetResourceReference(TextBlock.ForegroundProperty,"Muted");SetRow(matrixLegend,2);Children.Add(matrixLegend);
        matrixPanel=fit?BuildMatrix():BuildDeviationMatrix();SetRow(matrixPanel,3);Children.Add(matrixPanel);
        freePanel=new Grid();freePanel.ColumnDefinitions.Add(new(){Width=new GridLength(680)});freePanel.ColumnDefinitions.Add(new(){Width=new GridLength(24)});freePanel.ColumnDefinitions.Add(new());
        SetRow(freePanel,3);Children.Add(freePanel);
        var variable=new StackPanel();freePanel.Children.Add(variable);
        AddChoices(variable,fit?(basis==BasisSystem.Hole?FeatureKind.Shaft:FeatureKind.Hole):singleKind,false);
        var datum=new StackPanel();SetColumn(datum,2);freePanel.Children.Add(datum);
        if(fit)
        {
        AddChoices(datum,basis==BasisSystem.Hole?FeatureKind.Hole:FeatureKind.Shaft,true);
        datum.Children.Add(UI.Text(basis==BasisSystem.Hole?"基孔制固定孔代号 H，孔轴等级分别选择。":"基轴制固定轴代号 h，孔轴等级分别选择。",14,margin:new Thickness(0,20,0,12)));
        datum.Children.Add(UI.Text("自由选择使用全部有效收录值，最高至 3150 mm。切换模式保留当前组合。",13));
        }
        else
        {
            datum.Children.Add(UI.Text("完整收录查询",16,FontWeights.Medium,new Thickness(0,16,0,12)));
            datum.Children.Add(UI.Text("自由选择包含全部有效收录的代号与 IT1～IT18。矩阵覆盖常用选型，额外代号和等级可在此选择。",14));
            datum.Children.Add(UI.Text("尺寸变化时保留原选择；未收录的组合会清空结果和图解，禁用复制。",13,margin:new Thickness(0,20,0,0)));
        }
        var lower=new Grid();lower.ColumnDefinitions.Add(new(){Width=new GridLength(780)});lower.ColumnDefinitions.Add(new(){Width=new GridLength(16)});lower.ColumnDefinitions.Add(new());SetRow(lower,5);Children.Add(lower);
        var resultCard=UI.Card();resultCard.Padding=new Thickness(12);lower.Children.Add(resultCard);
        var content=new Grid();resultCard.Child=content;
        foreach(double height in new[]{32d,64d,20d,44d,32d,28d})content.RowDefinitions.Add(new(){Height=new GridLength(height)});
        var resultSummary=UI.Text("",16,FontWeights.Medium);content.Children.Add(resultSummary);
        var resultTable=new Grid();SetRow(resultTable,1);content.Children.Add(resultTable);
        foreach(double width in new[]{50d,110d,110d,100d,160d,160d})resultTable.ColumnDefinitions.Add(new(){Width=new GridLength(width)});
        foreach(double height in new[]{20d,22d,22d})resultTable.RowDefinitions.Add(new(){Height=new GridLength(height)});
        string[] headers=fit?["要素","上偏差 / mm","下偏差 / mm","公差 / μm","最小尺寸 / mm","最大尺寸 / mm"]:["单位","上偏差","下偏差","公差","最小尺寸 / mm","最大尺寸 / mm"];
        for(int col=0;col<headers.Length;col++){var t=UI.Text(headers[col],12);t.SetResourceReference(TextBlock.ForegroundProperty,"Secondary");if(col>0){t.TextAlignment=TextAlignment.Right;t.Margin=new Thickness(0,0,12,0);}SetColumn(t,col);resultTable.Children.Add(t);}
        for(int row=0;row<2;row++)
        {
            var t=UI.Text(fit?(row==0?"孔":"轴"):(row==0?"mm":"μm"),12);SetRow(t,row+1);resultTable.Children.Add(t);
            for(int col=0;col<5;col++)
            {
                t=UI.Text("—",14);t.FontFamily=new FontFamily("Consolas");t.TextWrapping=TextWrapping.NoWrap;
                t.HorizontalAlignment=HorizontalAlignment.Stretch;t.TextAlignment=TextAlignment.Right;t.Margin=new Thickness(0,0,12,0);SetRow(t,row+1);SetColumn(t,col+1);resultTable.Children.Add(t);fitValues[row,col]=t;
            }
        }
        intervalText=UI.Text("",12);intervalText.SetResourceReference(TextBlock.ForegroundProperty,"Muted");SetRow(intervalText,2);content.Children.Add(intervalText);
        var gradeAdvice=UI.Text("",12);gradeAdvice.SetResourceReference(TextBlock.ForegroundProperty,"Secondary");SetRow(gradeAdvice,3);content.Children.Add(gradeAdvice);
        var actions=new WrapPanel();SetRow(actions,4);content.Children.Add(actions);
        if(fit)
        {
        AddCopy(actions,"孔 ES",()=>CopyNumber(Hole!.UpperUm));AddCopy(actions,"孔 EI",()=>CopyNumber(Hole!.LowerUm));
        AddCopy(actions,"轴 es",()=>CopyNumber(Shaft!.UpperUm));AddCopy(actions,"轴 ei",()=>CopyNumber(Shaft!.LowerUm));
        AddCopy(actions,"尺寸标注",()=>Heading);AddCopy(actions,"完整结果",()=>FullText);
        }
        else
        {
            AddCopy(actions,"上偏差",()=>DeviationCopy(true));AddCopy(actions,"下偏差",()=>DeviationCopy(false));
            AddCopy(actions,"最大尺寸",()=>F((Hole??Shaft)!.MaximumMm));AddCopy(actions,"最小尺寸",()=>F((Hole??Shaft)!.MinimumMm));
            AddCopy(actions,"尺寸标注",()=>Heading);AddCopy(actions,"完整结果",()=>FullText);
        }
        foreach(var button in actions.Children.OfType<Button>())
        {button.Height=28;button.Padding=new Thickness(8,3,8,3);button.Margin=new Thickness(0,0,6,0);}
        var resultNotice=UI.Text("",12);resultNotice.SetResourceReference(TextBlock.ForegroundProperty,"Muted");SetRow(resultNotice,5);content.Children.Add(resultNotice);
        var plotCard=UI.Card();plotCard.Padding=new Thickness(12);SetColumn(plotCard,2);lower.Children.Add(plotCard);plotCard.Child=Plot;
        MatrixMode=fit?CommonFits.For(basis).Any(MatchesInput):BandSelection.For(singleKind).Any(MatchesDeviation);UpdateMode();
        return(input,title,resultSummary,resultNotice,gradeAdvice);
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
        foreach(var row in rows)grid.RowDefinitions.Add(new(){Height=new GridLength(40)});
        void Header(string value,int row,int col)
        {
            var t=UI.Text(value,12,FontWeights.Medium);t.VerticalAlignment=VerticalAlignment.Center;t.HorizontalAlignment=HorizontalAlignment.Center;t.TextWrapping=TextWrapping.NoWrap;
            SetRow(t,row);SetColumn(t,col);grid.Children.Add(t);
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
    private void AlignResultColumns()
    {for(int col=0;col<5;col++)Appearance.AlignColumn(new[]{fitValues[0,col],fitValues[1,col]});}
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
    private void RefreshMatrix(decimal size,bool valid)
    {
        foreach(var cell in MatrixCells)
        {
            var p=cell.Preset;bool selected=MatchesInput(p);
            cell.Button.IsEnabled=valid&&size<=500&&catalog.Available(FeatureKind.Hole,size,p.HoleZone,p.HoleGrade)&&catalog.Available(FeatureKind.Shaft,size,p.ShaftZone,p.ShaftGrade);
            Appearance.MatrixState(cell.Button,cell.Check,p.IsPreferred?SelectionRank.Preferred:SelectionRank.Other,selected);
        }
        matrixLegend!.Text=!valid?"请输入有效公称尺寸；当前选择保留。":!MatrixMode?"自由选择：孔轴等级分别点选，按实际收录范围查询；切换模式保留当前组合。":size>500?"常用矩阵适用尺寸 ≤ 500 mm；更大尺寸请使用自由选择。":basis==BasisSystem.Hole?"一格选择完整孔轴组合；H8 子行用于区分轴等级。空格为未收录常用组合。":"一格选择完整孔轴组合；空格为未收录常用组合。";
        var elements=new[]{Hole,Shaft};
        for(int row=0;row<2;row++)
        {
            var r=elements[row];var values=r is null?null:new[]{S(r.UpperUm/1000m),S(r.LowerUm/1000m),F(r.ToleranceUm),F(r.MinimumMm),F(r.MaximumMm)};
            for(int col=0;col<5;col++)fitValues[row,col].Text=values?[col]??"—";
        }
        AlignResultColumns();
        intervalText!.Text=HasResult?$"孔：{Hole!.Interval}    轴：{Shaft!.Interval}":"结果、图解及复制已清空；所选组合仍保留。";
        var preset=CommonFits.For(basis).FirstOrDefault(MatchesInput);
        advice.Text=$"{preset?.Use??"自由组合"}。孔 IT{Input.HoleGrade}：{SizeToleranceCatalog.GuidanceFor(Input.HoleGrade).Summary}；轴 IT{Input.ShaftGrade}：{SizeToleranceCatalog.GuidanceFor(Input.ShaftGrade).Summary}。实际配合性质以当前尺寸计算结果为准。";
        advice.ToolTip=$"孔 IT{Input.HoleGrade}：{SizeToleranceCatalog.GradeAdvice(Input.HoleGrade)}\n轴 IT{Input.ShaftGrade}：{SizeToleranceCatalog.GradeAdvice(Input.ShaftGrade)}";
        UpdateMode();
    }
}
