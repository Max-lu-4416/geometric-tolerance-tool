using System.ComponentModel;
using Tolerance.Core;

namespace Tolerance.App;

public sealed class QueryViewModel : INotifyPropertyChanged
{
    public ToleranceCatalog Catalog { get; } = new();
    public GeneralToleranceCatalog GeneralCatalog { get; }
    public IReadOnlyList<ToleranceType> Types => Catalog.Types;
    public IReadOnlyList<TypeGroup> TypeGroups { get; }
    private ToleranceType selectedType;
    private int selectedGrade = 6;
    private string selectedCode = "6", parameterText = "100", sizeToleranceText = "";
    private QueryResult? result;
    private GeneralQueryResult? generalResult;
    private string error = "";
    private bool adviceRequested;
    private GradeChoice[] gradeChoices = [];
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? QueryChanged;
    public QueryViewModel()
    {
        GeneralCatalog = new(Catalog);
        TypeChoice Choice(string id) { var t = Types.Single(t => t.Id == id); return new(t, t.Name, t.Geometry,
            id == "circular-runout" ? "M 10,5 L 20,3 L 19,13 Z" : id == "total-runout" ? "M 8,5 L 17,3 L 16,12 Z M 15,5 L 24,3 L 23,12 Z" : null); }
        TypeGroups = new[]
        {
            new TypeGroup("线与面", new[] { Choice("straightness"), Choice("flatness") }),
            new TypeGroup("回转表面", new[] { Choice("roundness"), Choice("cylindricity") }),
            new TypeGroup("方向", new[] { Choice("parallelism"), Choice("perpendicularity"), Choice("angularity") }),
            new TypeGroup("位置与跳动", new[] { Choice("coaxiality"), Choice("symmetry"),
                new TypeChoice(null, "位置度", "M 19,12 A 7,7 0 1 1 5,12 A 7,7 0 1 1 19,12 M 0,12 L 24,12 M 12,0 L 12,24"), Choice("circular-runout"), Choice("total-runout") })
        };
        selectedType = Types[1]; RefreshGrades(); Update();
    }
    public ToleranceType SelectedType
    {
        get => selectedType;
        set
        {
            if (value is null || selectedType == value) return;
            selectedType = value;
            var table = Catalog.TableFor(value);
            selectedGrade = Math.Clamp(selectedGrade, table.FirstGrade, table.LastGrade);
            if (!IsGeneral) selectedCode = selectedGrade.ToString();
            RefreshGrades(); Update();
        }
    }
    public string SelectedGradeCode
    {
        get => selectedCode;
        set
        {
            if (value == selectedCode) return;
            if (!(value is "H" or "K" or "L") && !int.TryParse(value, out _)) return;
            selectedCode = value;
            if (int.TryParse(value, out int g)) selectedGrade = g;
            Update();
        }
    }
    public int SelectedGrade { get => selectedGrade; set => SelectedGradeCode = value.ToString(); }
    public string ParameterText { get => parameterText; set { parameterText = value; Update(); } }
    public string SizeToleranceText { get => sizeToleranceText; set { sizeToleranceText = value; Update(); } }
    public bool IsGeneral => selectedCode is "H" or "K" or "L";
    public bool LengthRequired => !IsGeneral || GeneralToleranceCatalog.RequiresLength(selectedType.Id);
    public bool SizeToleranceRequired => IsGeneral && GeneralToleranceCatalog.RequiresSizeTolerance(selectedType.Id);
    public string SizeToleranceLabel => selectedType.Id == "roundness" ? "直径尺寸公差总宽 / mm" : "尺寸公差总宽 / mm";
    public int[] Grades { get { var t = Catalog.TableFor(selectedType); return Enumerable.Range(t.FirstGrade, t.Rows.Length).ToArray(); } }
    public GradeChoice[] GradeChoices => gradeChoices;
    public GradeChoice[] GeneralGradeChoices { get; } = [new("H", true), new("K", true), new("L", true)];
    public GradeChoice[] AllGradeChoices { get; private set; } = [];
    public string NumericGradeHint => $"标注公差 · {Catalog.TableFor(selectedType).FirstGrade}～{Catalog.TableFor(selectedType).LastGrade} 级";
    public string ResultCategory => HasResult ? IsGeneral ? "未注公差" : "标注公差" : "等待有效条件";
    public string ResultContext => HasResult && Interval.Length>0 ? $"{ResultDescription} · {Interval}" : ResultDescription;
    public string GradeHint => IsGeneral ? "当前为未注公差 · 点击等级查看选用说明" : "0～12：标注公差 · H/K/L：未注公差";
    public string ParameterLabel => !IsGeneral ? selectedType.ParameterLabel : selectedType.Id == "flatness" ? "L / D" : "L";
    public string ParameterHint => !IsGeneral ? selectedType.Hint : selectedType.Id switch
    {
        "straightness" => "按被测线的长度选值",
        "flatness" => "按表面较长一侧或圆表面的直径选值",
        "perpendicularity" => "按直角两边中较短边的长度选值，较长边作为基准",
        "symmetry" => "按被测要素基本长度选值，较长要素作为基准",
        "parallelism" => "基本长度按相应直线度或平面度项目取值；较长要素作为基准",
        "roundness" => "由直径尺寸公差与径向圆跳动限值共同确定",
        "circular-runout" => "该项限值固定，不需要基本长度",
        _ => "该类型未规定可直接查询的 H/K/L 数值"
    };
    public string AllowedRange => !LengthRequired ? "当前项目无需输入主参数" : IsGeneral ? "0 < 基本长度 ≤ 3000 mm" : $"0 < 主参数 ≤ {QueryResult.Format(Catalog.TableFor(selectedType).Bounds[^1])} mm";
    public bool HasResult => result is not null || generalResult is not null;
    public string ResultState => HasResult ? (IsGeneral ? "未注公差" : "查询完成") : "等待有效条件";
    public string Millimeters => result is not null ? QueryResult.Format(result.Millimeters) : generalResult is not null ? QueryResult.Format(generalResult.Millimeters) : "—";
    public string MicrometerValue => result is not null ? QueryResult.Format(result.Micrometers) : generalResult is not null ? QueryResult.Format(generalResult.Micrometers) : "";
    public string Micrometers => HasResult ? $"= {MicrometerValue} μm" : "— μm";
    public string ResultDescription => HasResult ? $"{selectedType.Name} · {selectedCode} 级" : "请检查查询条件";
    public string Interval => result?.Interval ?? generalResult?.Interval ?? "";
    public string RuleText => generalResult?.Rule ?? "";
    public string Error => error;
    public QueryResult? Result => result;
    public GeneralQueryResult? GeneralResult => generalResult;
    public string FullResult => result?.FullText ?? generalResult?.FullText ?? "";
    public string DiagramPath => $"pack://application:,,,/Assets/diagram-{selectedType.Id}.png";
    public string DiagramCaption => $"{selectedType.Name} · {(LengthRequired ? "主参数图例" : "要素示意")}";
    public bool AdviceRequested => adviceRequested;
    private GradeAdvice Advice => GradeGuidance.For(selectedType, selectedCode);
    public string AdviceTitle => adviceRequested ? Advice.Title : "等级选用说明";
    public string AdviceApplication => adviceRequested ? Advice.Application : "点击数字等级或 H/K/L，查看适用场景";
    public string AdviceExamples => adviceRequested ? Advice.Examples : "";
    public string AdviceNote => adviceRequested ? Advice.Note : "";
    public void ShowGradeAdvice() { adviceRequested = true; PropertyChanged?.Invoke(this, new(null)); }
    public void RefreshFormatting() => PropertyChanged?.Invoke(this, new(null));
    private void RefreshGrades()
    {
        gradeChoices = Enumerable.Range(0, 13).Select(g => new GradeChoice(g.ToString(), g >= Catalog.TableFor(selectedType).FirstGrade)).ToArray();
        AllGradeChoices = gradeChoices.Concat(GeneralGradeChoices).ToArray();
    }
    private void Update()
    {
        result = null; generalResult = null;
        if (IsGeneral) GeneralCatalog.TryQuery(selectedType.Id, selectedCode, parameterText, sizeToleranceText, out generalResult, out error);
        else Catalog.TryQuery(selectedType.Id, selectedGrade, parameterText, out result, out error);
        PropertyChanged?.Invoke(this, new(null));
        QueryChanged?.Invoke(this, EventArgs.Empty);
    }
}
