using System.Text.Json;

namespace Tolerance.Core;

public sealed record ToleranceType(string Id, string Name, string Symbol, string Geometry,
    string TableId, string ParameterLabel, string Hint)
{
    public string DisplayName => $"{Symbol}  {Name}";
}

public sealed class TableData
{
    public string Id { get; set; } = "";
    public int FirstGrade { get; set; }
    public string Page { get; set; } = "";
    public string ContinuationPage { get; set; } = "";
    public int ContinuationGrade { get; set; }
    public decimal[] Bounds { get; set; } = [];
    public decimal[][] Rows { get; set; } = [];
    public int LastGrade => FirstGrade + Rows.Length - 1;
}

public sealed record QueryResult(ToleranceType Type, int Grade, decimal Parameter,
    decimal Lower, decimal Upper, decimal Micrometers, string TableId, string Page)
{
    public decimal Millimeters => Micrometers / 1000m;
    public string Interval => Lower == 0 ? $"0 < 主参数 ≤ {Format(Upper)} mm" : $"{Format(Lower)} < 主参数 ≤ {Format(Upper)} mm";
    public string FullText => $"{Type.Name}（{Type.Symbol}）\r\n公差等级：{Grade} 级\r\n主参数：{Type.ParameterLabel} = {Format(Parameter)} mm\r\n尺寸区间：{Interval}\r\n公差：{Format(Millimeters)} mm = {Format(Micrometers)} μm";
    public static string Format(decimal value) => NumberDisplay.Format(value);
}

public sealed class ToleranceCatalog
{
    private sealed class Root { public TableData[] Tables { get; set; } = []; }
    private readonly Dictionary<string, TableData> tables;
    public IReadOnlyList<ToleranceType> Types { get; } = Array.AsReadOnly(new[]
    {
        new ToleranceType("straightness", "直线度", "⏤", "M 2,12 L 22,12", "2-3-22", "L", "L 为被测线的长度，取值方向可查看主参数图例"),
        new ToleranceType("flatness", "平面度", "⏥", "M 2,17 L 8,7 L 23,7 L 17,17 Z", "2-3-22", "L", "L 按被测平面的主参数图例取值，请勿填入公差值"),
        new ToleranceType("roundness", "圆度", "○", "M 22,12 A 10,10 0 1 1 2,12 A 10,10 0 1 1 22,12", "2-3-23", "d / D", "d 为被测外圆直径，D 为被测内孔直径"),
        new ToleranceType("cylindricity", "圆柱度", "⌭", "M 19,12 A 7,7 0 1 1 5,12 A 7,7 0 1 1 19,12 M 2,21 L 8,3 M 16,21 L 22,3", "2-3-23", "d / D", "主参数为被测圆柱面的外径 d 或内径 D"),
        new ToleranceType("coaxiality", "同轴度", "◎", "M 22,12 A 10,10 0 1 1 2,12 A 10,10 0 1 1 22,12 M 17,12 A 5,5 0 1 1 7,12 A 5,5 0 1 1 17,12", "2-3-24", "d / D", "输入被测要素的直径 d 或 D，注意区分基准直径与被测直径"),
        new ToleranceType("symmetry", "对称度", "≡", "M 2,5 L 22,5 M 2,12 L 22,12 M 2,19 L 22,19", "2-3-24", "B / L", "输入被测要素的宽度 B 或图示长度 L"),
        new ToleranceType("circular-runout", "圆跳动", "↗", "M 4,21 L 20,3 M 10,5 L 20,3 L 19,13", "2-3-24", "d / D", "按图示被测直径取值；圆锥面取 d = (d₁ + d₂) / 2"),
        new ToleranceType("total-runout", "全跳动", "↗↗", "M 1,21 L 17,3 M 8,5 L 17,3 L 16,12 M 8,21 L 24,3 M 15,5 L 24,3 L 23,12", "2-3-24", "d / D", "按被测直径 d 或 D 取值，具体方向可查看主参数图例"),
        new ToleranceType("parallelism", "平行度", "∥", "M 4,21 L 12,3 M 12,21 L 20,3", "2-3-25", "L / d / D", "按被测长度 L 或直径 d / D 取值，具体方向可查看主参数图例"),
        new ToleranceType("perpendicularity", "垂直度", "⊥", "M 2,21 L 22,21 M 12,21 L 12,2", "2-3-25", "L / d / D", "输入被测长度 L 或被测端面的直径 d / D"),
        new ToleranceType("angularity", "倾斜度", "∠", "M 22,21 L 2,21 L 20,3", "2-3-25", "L", "主参数为倾斜被测要素的长度 L，请勿输入倾斜角度 α")
    });

    public ToleranceCatalog()
    {
        using var stream = typeof(ToleranceCatalog).Assembly.GetManifestResourceStream("Tolerance.Core.Catalog.json")
            ?? throw new InvalidDataException("内嵌公差数据缺失。");
        var root = JsonSerializer.Deserialize<Root>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("公差数据无法读取。");
        tables = root.Tables.ToDictionary(t => t.Id);
        if (tables.Count != 4) throw new InvalidDataException("公差表数量错误。");
        foreach (var t in tables.Values)
        {
            if (t.Bounds.Length == 0 || t.Bounds[0] <= 0 || t.Rows.Length == 0 ||
                t.Bounds.Zip(t.Bounds.Skip(1)).Any(p => p.First >= p.Second) ||
                t.Rows.Any(row => row.Length != t.Bounds.Length || row.Any(v => v <= 0)))
                throw new InvalidDataException("公差数据不完整。");
        }
    }

    public TableData TableFor(ToleranceType type) => tables[type.TableId];

    public bool TryQuery(string? typeId, int grade, string? parameterText, out QueryResult? result, out string error)
    {
        result = null;
        var type = Types.FirstOrDefault(t => t.Id == typeId);
        if (type is null) { error = "请选择几何公差类型。"; return false; }
        var table = TableFor(type);
        if (grade < table.FirstGrade || grade > table.LastGrade)
        { error = $"{type.Name}支持 {table.FirstGrade}～{table.LastGrade} 级。"; return false; }
        if (string.IsNullOrWhiteSpace(parameterText))
        { error = "请输入主参数，单位为 mm。"; return false; }
        if (!ExactDecimal.TryParse(parameterText, true, out var parameter, out var parseError))
        {
            error = parseError == DecimalParseError.PrecisionExceeded
                ? "数字精度超出支持范围，请减少小数位数。"
                : "请输入有效数字，小数点使用英文句点，例如 25.5。";
            return false;
        }
        if (parameter <= 0 || parameter > table.Bounds[^1])
        { error = $"主参数必须大于 0 且不超过 {QueryResult.Format(table.Bounds[^1])} mm。"; return false; }
        int column = Array.FindIndex(table.Bounds, bound => parameter <= bound);
        result = new(type, grade, parameter, column == 0 ? 0 : table.Bounds[column - 1], table.Bounds[column],
            table.Rows[grade - table.FirstGrade][column], table.Id,
            grade >= table.ContinuationGrade ? table.ContinuationPage : table.Page);
        error = "";
        return true;
    }
}
