using Tolerance.Core;

namespace Tolerance.App;

/// <summary>Copy output depends on query results, never on visual controls.</summary>
internal sealed record SizeResultText(string Heading, string Summary, string Details, string Notice)
{
    internal string FullText => $"{Heading}\r\n{Summary}\r\n{Details}\r\n{Notice}";

    internal static SizeResultText Create(DeviationResult? hole, DeviationResult? shaft, FitResult? fit, string copyUnit)
    {
        var single = hole ?? shaft ?? throw new ArgumentException("需要有效查询结果。");
        static string F(decimal d) => NumberDisplay.Format(d);
        static string S(decimal d) => NumberDisplay.Signed(d);
        var heading = $"⌀{F(single.NominalMm)} " + (fit is null ? single.Band : $"{hole!.Band}/{shaft!.Band}");
        var summary = fit is null ? single.Kind == FeatureKind.Hole ? "孔：上偏差 ES，下偏差 EI" : "轴：上偏差 es，下偏差 ei" : $"{fit.Nature} · {fit.Range(F)}";
        var details = string.Join("\r\n", new[] { hole, shaft }.OfType<DeviationResult>().Select(r =>
            $"{(r.Kind == FeatureKind.Hole ? "孔 ES / EI" : "轴 es / ei")}   {S(r.UpperUm / 1000m)} / {S(r.LowerUm / 1000m)} mm   ({S(r.UpperUm)} / {S(r.LowerUm)} μm)\r\n极限尺寸 {F(r.MinimumMm)} ～ {F(r.MaximumMm)} mm    公差 {F(r.ToleranceUm)} μm\r\n尺寸区间 {r.Interval}"));
        var notice = string.Join("；", new[] { hole?.Notice, shaft?.Notice }.Where(s => !string.IsNullOrEmpty(s)).Distinct());
        if (notice.Length == 0) notice = $"偏差复制单位：{copyUnit}；尺寸始终为 mm。";
        return new(heading, summary, details, notice);
    }
}
