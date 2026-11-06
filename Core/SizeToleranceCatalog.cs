using System.Text.Json;
namespace Tolerance.Core;

public sealed record SizeGradeAdvice(string Summary, string Detail);
public enum FeatureKind { Hole, Shaft }
public enum BasisSystem { Hole, Shaft }
public sealed record SizeCell(string Kind, string Zone, string Grade, decimal LowerMm, decimal UpperMm,
    decimal UpperUm, decimal LowerUm, bool Trial, decimal MinMm, bool MinInclusive, bool WidthWarning);
public sealed record DeviationResult(FeatureKind Kind, decimal NominalMm, SizeCell Cell)
{
    public decimal UpperUm => Cell.UpperUm;
    public decimal LowerUm => Cell.LowerUm;
    public decimal ToleranceUm => UpperUm - LowerUm;
    public decimal MaximumMm => NominalMm + UpperUm / 1000m;
    public decimal MinimumMm => NominalMm + LowerUm / 1000m;
    public string Band => Cell.Zone + Cell.Grade;
    public string Interval => $"{QueryResult.Format(Math.Max(Cell.LowerMm,Cell.MinMm))} {(Cell.MinInclusive&&Cell.MinMm>Cell.LowerMm?"≤":"<")} d/D ≤ {QueryResult.Format(Cell.UpperMm)} mm";
    public string Notice => string.Join("；", new[] { Cell.Trial ? "试用值" : "", Cell.WidthWarning ? "收录偏差宽度与同级标准公差不同，请确认后使用" : "" }.Where(s => s.Length > 0));
}
public sealed record FitResult(BasisSystem Basis, DeviationResult Hole, DeviationResult Shaft)
{
    public decimal MinimumDifferenceUm => Hole.LowerUm - Shaft.UpperUm;
    public decimal MaximumDifferenceUm => Hole.UpperUm - Shaft.LowerUm;
    public string Nature => MinimumDifferenceUm >= 0 ? "间隙配合" : MaximumDifferenceUm <= 0 ? "过盈配合" : "过渡配合";
    public string Range(Func<decimal,string> format) => MinimumDifferenceUm >= 0
        ? $"间隙 {format(MinimumDifferenceUm / 1000m)} ～ {format(MaximumDifferenceUm / 1000m)} mm"
        : MaximumDifferenceUm <= 0 ? $"过盈 {format(-MaximumDifferenceUm / 1000m)} ～ {format(-MinimumDifferenceUm / 1000m)} mm"
        : $"最大间隙 {format(MaximumDifferenceUm / 1000m)} mm；最大过盈 {format(-MinimumDifferenceUm / 1000m)} mm";
}
public sealed class SizeToleranceCatalog
{
    public IReadOnlyList<SizeCell> Cells { get; }
    public SizeToleranceCatalog()
    {
        using var stream=typeof(SizeToleranceCatalog).Assembly.GetManifestResourceStream("Tolerance.Core.SizeCatalog.json")!;
        Cells=JsonSerializer.Deserialize<SizeCell[]>(stream,new JsonSerializerOptions { PropertyNameCaseInsensitive=true })!;
    }
    public IEnumerable<string> Zones(FeatureKind kind) => Cells.Where(c=>c.Kind==(kind==FeatureKind.Hole?"hole":"shaft")).Select(c=>c.Zone).Distinct();
    public bool Available(FeatureKind kind,decimal size,string zone,int grade) => Cells.Any(c=>Matches(c,kind,size,zone,grade));
    private static bool Matches(SizeCell c,FeatureKind kind,decimal size,string zone,int grade) => c.Kind==(kind==FeatureKind.Hole?"hole":"shaft") && c.Zone==zone && c.Grade==grade.ToString() && size>c.LowerMm && size<=c.UpperMm && (c.MinInclusive?size>=c.MinMm:size>c.MinMm);
    public DeviationResult LookupDeviation(FeatureKind kind,decimal nominalMm,string zone,int grade)
    {
        if(nominalMm<=0 || nominalMm>3150) throw new ArgumentException("公称尺寸须大于 0 且不超过 3150 mm。");
        var cell=Cells.SingleOrDefault(c=>Matches(c,kind,nominalMm,zone,grade));
        if(cell is null) throw new ArgumentException($"{zone}{grade} 在当前尺寸未收录有效偏差；请保留或重新选择公差带。");
        return new(kind,nominalMm,cell);
    }
    public FitResult CalculateFit(BasisSystem basis,decimal nominalMm,string holeZone,int holeGrade,string shaftZone,int shaftGrade)
    {
        if(basis==BasisSystem.Hole && holeZone!="H" || basis==BasisSystem.Shaft && shaftZone!="h") throw new ArgumentException("基孔制固定 H，基轴制固定 h。");
        return new(basis,LookupDeviation(FeatureKind.Hole,nominalMm,holeZone,holeGrade),LookupDeviation(FeatureKind.Shaft,nominalMm,shaftZone,shaftGrade));
    }
    public static string GradeAdvice(int grade) => GuidanceFor(grade).Detail;
    public static SizeGradeAdvice GuidanceFor(int grade)
    {
        (string summary,string detail) = grade switch
        {
            <=4 => ("精密量规、仪器及高精度配合", "精密量规、精密仪器及高精度配合。需具备稳定的精加工与检测能力。"),
            5 or 6 => ("精密配合、主轴与轴承连接", "精密配合、机床主轴和轴承配合等。常配合磨削、精镗等工艺，最终按功能与工艺能力确定。"),
            7 or 8 => ("一般机械定位、滑动与装配", "一般机械中较常用的配合精度，如定位、滑动与装配连接。需结合偏差代号和实际间隙或过盈选用。"),
            9 or 10 => ("较宽松的配合与间隙连接", "要求较宽松的配合、一般间隙连接。适用于对定位精度要求不高的部位。"),
            11 or 12 => ("低精度连接或非精密尺寸", "粗配合和低精度连接，常用于较宽松的加工要求。"),
            _ => ("低精度连接或非精密尺寸", "尺寸精度要求较低的非配合尺寸或粗加工。粗等级通常不适用于精密定位。")
        };
        return new(summary, detail + " 等级仅决定公差宽度，不能单独决定配合性质。");
    }
}
