using System.Globalization;
using Tolerance.Core;

static class GeneralTests
{
    public static void Run(ToleranceCatalog numeric, Action<bool, string> check)
    {
        var general = new GeneralToleranceCatalog(numeric);
        var fixture = File.ReadLines(Path.Combine(AppContext.BaseDirectory, "general-fixture.csv")).Skip(1).Select(l => l.Split(','))
            .Select(p => new { Kind = p[0], Grade = p[1], Upper = decimal.Parse(p[2], CultureInfo.InvariantCulture), Value = decimal.Parse(p[3], CultureInfo.InvariantCulture) }).ToArray();
        check(fixture.Length == 45, "All 45 independently transcribed H/K/L cells");
        foreach (var group in fixture.GroupBy(c => (c.Kind, c.Grade)))
        {
            decimal lower = 0;
            foreach (var c in group)
            {
                string[] types = c.Kind switch { "form" => ["straightness", "flatness", "parallelism"], "runout" => ["circular-runout"], _ => [c.Kind] };
                foreach (var id in types)
                {
                    foreach (var length in c.Kind == "runout" ? [0m] : new[] { c.Upper, c.Upper - 0.000001m, lower + 0.000001m })
                    {
                        check(general.TryQuery(id, c.Grade, QueryResult.Format(length), "0.0001", out var r, out _), "General query succeeds");
                        check(r!.Millimeters == c.Value && r.Micrometers == c.Value * 1000m, $"General source value {id}/{c.Grade}/{length}");
                        check(r.Grade == c.Grade && !r.FullText.Contains("手册"), "General output grade and clean copy");
                        if (c.Kind != "runout") check(r.Lower == lower && r.Upper == c.Upper, "General boundary identity");
                    }
                    if (c.Upper is > 0 and < 3000)
                    {
                        general.TryQuery(id, c.Grade, QueryResult.Format(c.Upper + 0.000001m), "0.0001", out var next, out _);
                        check(next?.Lower == c.Upper, "General boundary plus epsilon");
                    }
                }
                lower = c.Upper;
            }
        }
        foreach (var (grade, cap) in new[] { ("H", 0.1m), ("K", 0.2m), ("L", 0.5m) })
        {
            foreach (decimal size in new[] { 0.05m, cap, cap + 0.05m })
            {
                check(general.TryQuery("roundness", grade, "", QueryResult.Format(size), out var r, out _), "Roundness needs no length");
                check(r!.Millimeters == Math.Min(size, cap), "Roundness minimum rule");
            }
            foreach (var invalid in new[] { "", "0", "-0.1", "abc", "1,000", "1e2", "0.10000000000000000000000000001" })
            {
                check(!general.TryQuery("roundness", grade, "", invalid, out var r, out _) && r is null, "Roundness missing/invalid dimension tolerance");
                check(!general.TryQuery("parallelism", grade, "100", invalid, out _, out _), "Parallelism needs valid size tolerance");
            }
            foreach (var id in new[] { "cylindricity", "coaxiality", "angularity", "total-runout" })
                check(!general.TryQuery(id, grade, "100", "0.1", out var r, out var error) && r is null && error.Length > 0, "No fabricated general value");
            foreach (var id in new[] { "straightness", "flatness", "parallelism", "perpendicularity", "symmetry" })
                foreach (var bad in new[] { "", "0", "-1", "3000.000001", "NaN", "100.00000000000000000000000000001" })
                    check(!general.TryQuery(id, grade, bad, "0.1", out _, out _), "General invalid length");
        }
        check(general.TryQuery("parallelism", "H", "100", "0.25", out var p1, out _) && p1!.Millimeters == 0.25m, "Parallelism max takes size");
        check(general.TryQuery("parallelism", "H", "100", "0.05", out var p2, out _) && p2!.Millimeters == 0.1m, "Parallelism max takes form");
        check(!general.TryQuery("unknown", "H", "1", "1", out _, out _), "Unknown general type");
        check(!general.TryQuery("flatness", "M", "1", "1", out _, out _), "Unknown general grade");
        foreach (var type in numeric.Types)
            foreach (var grade in Enumerable.Range(numeric.TableFor(type).FirstGrade, numeric.TableFor(type).Rows.Length).Select(g => g.ToString()).Concat(new[] { "H", "K", "L" }))
            {
                var advice = GradeGuidance.For(type, grade);
                check(advice.Title.Length > 0 && advice.Application.Length > 0 && advice.Examples.Length > 0 && advice.Note.Length > 0, "Every selectable grade has guidance");
                check(!advice.ToString().Contains("手册"), "No source labels in guidance");
            }
    }
}
