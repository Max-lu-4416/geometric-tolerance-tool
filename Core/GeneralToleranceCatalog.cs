namespace Tolerance.Core;

public sealed record GeneralQueryResult(ToleranceType Type, string Grade, decimal? Parameter,
    decimal? SizeTolerance, decimal? Lower, decimal? Upper, decimal Millimeters, string Rule)
{
    public decimal Micrometers => Millimeters * 1000m;
    public string Interval => Upper.HasValue
        ? $"{QueryResult.Format(Lower ?? 0)} < 基本长度 ≤ {QueryResult.Format(Upper.Value)} mm"
        : "不按基本长度分段";
    public string FullText => $"{Type.Name}（{Type.Symbol}）\r\n未注公差等级：{Grade}"
        + (Parameter is decimal p ? $"\r\n基本长度：{QueryResult.Format(p)} mm\r\n尺寸区间：{Interval}" : "")
        + (SizeTolerance is decimal s ? $"\r\n尺寸公差：{QueryResult.Format(s)} mm" : "")
        + $"\r\n公差：{QueryResult.Format(Millimeters)} mm = {QueryResult.Format(Micrometers)} μm\r\n规则：{Rule}";
}

public sealed class GeneralToleranceCatalog(ToleranceCatalog numeric)
{
    private static readonly decimal[] LengthBounds = [10, 30, 100, 300, 1000, 3000];
    private static readonly decimal[][] FormValues =
    [
        [0.02m, 0.05m, 0.1m, 0.2m, 0.3m, 0.4m],
        [0.05m, 0.1m, 0.2m, 0.4m, 0.6m, 0.8m],
        [0.1m, 0.2m, 0.4m, 0.8m, 1.2m, 1.6m]
    ];
    private static readonly decimal[] OtherBounds = [100, 300, 1000, 3000];
    private static readonly decimal[][] PerpendicularValues =
    [
        [0.2m, 0.3m, 0.4m, 0.5m], [0.4m, 0.6m, 0.8m, 1m], [0.6m, 1m, 1.5m, 2m]
    ];
    private static readonly decimal[][] SymmetryValues =
    [
        [0.5m, 0.5m, 0.5m, 0.5m], [0.6m, 0.6m, 0.8m, 1m], [0.6m, 1m, 1.5m, 2m]
    ];
    private static readonly decimal[] RunoutValues = [0.1m, 0.2m, 0.5m];
    public static bool RequiresLength(string id) => id is "straightness" or "flatness" or "parallelism" or "perpendicularity" or "symmetry";
    public static bool RequiresSizeTolerance(string id) => id is "roundness" or "parallelism";
    public static string UnsupportedReason(string id) => id switch
    {
        "cylindricity" => "圆柱度未规定通用的未注公差值；由圆度、直线度及相对素线平行度控制，有功能要求时应单独标注",
        "coaxiality" => "同轴度未规定通用的未注公差值；极限状况下可参考径向圆跳动值，须结合具体条件确定",
        "angularity" => "倾斜度未规定独立的 H/K/L 数值，应由相关形位、线性尺寸或角度公差控制",
        "total-runout" => "全跳动未规定独立的 H/K/L 数值，应由相关形位、线性尺寸或角度公差控制",
        _ => ""
    };
    public bool TryQuery(string? typeId, string? grade, string? lengthText, string? sizeToleranceText,
        out GeneralQueryResult? result, out string error)
    {
        result = null;
        var type = numeric.Types.FirstOrDefault(t => t.Id == typeId);
        if (type is null) { error = "请选择几何公差类型"; return false; }
        int row = grade switch { "H" => 0, "K" => 1, "L" => 2, _ => -1 };
        if (row < 0) { error = "请选择 H、K 或 L 未注公差等级"; return false; }
        error = UnsupportedReason(type.Id);
        if (error.Length != 0) return false;
        decimal? length = null, size = null, lower = null, upper = null;
        decimal limit;
        if (RequiresLength(type.Id))
        {
            if (!TryPositive(lengthText, out var parsedLength)) { error = "请输入有效的基本长度（mm），必须大于 0"; return false; }
            if (parsedLength > 3000) { error = "H/K/L 基本长度必须大于 0 且不超过 3000 mm"; return false; }
            length = parsedLength;
            var bounds = type.Id is "perpendicularity" or "symmetry" ? OtherBounds : LengthBounds;
            int column = Array.FindIndex(bounds, b => parsedLength <= b);
            lower = column == 0 ? 0 : bounds[column - 1]; upper = bounds[column];
            limit = type.Id switch
            {
                "perpendicularity" => PerpendicularValues[row][column],
                "symmetry" => SymmetryValues[row][column],
                _ => FormValues[row][column]
            };
        }
        else limit = RunoutValues[row];
        string rule = type.Id switch
        {
            "roundness" => "取直径尺寸公差与径向圆跳动限值中的较小值",
            "parallelism" => "取尺寸公差与相应直线度或平面度限值中的较大值",
            "circular-runout" => "圆跳动限值固定，与基本长度无关",
            "symmetry" => "适用于至少一个要素为中心平面，或两要素轴线相互垂直的情形",
            _ => "按基本长度范围选取未注公差值"
        };
        if (RequiresSizeTolerance(type.Id))
        {
            if (!TryPositive(sizeToleranceText, out var parsedSize))
            { error = type.Id == "roundness" ? "请输入直径尺寸公差（mm），必须大于 0" : "请输入尺寸公差（mm），必须大于 0"; return false; }
            // A millimeter value must also be exactly representable in micrometers.
            if (parsedSize > decimal.MaxValue / 1000m) { error = "尺寸公差超出可计算范围"; return false; }
            size = parsedSize;
            rule += $"（{QueryResult.Format(parsedSize)} 与 {QueryResult.Format(limit)} mm）";
            limit = type.Id == "roundness" ? Math.Min(limit, parsedSize) : Math.Max(limit, parsedSize);
        }
        result = new(type, grade!, length, size, lower, upper, limit, rule);
        error = ""; return true;
    }
    private static bool TryPositive(string? text, out decimal value)
        => ExactDecimal.TryParse(text, true, out value, out _) && value > 0;
}
