using System.Globalization;
using Tolerance.Core;

var catalog = new ToleranceCatalog();
int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
var fixture = File.ReadLines(Path.Combine(AppContext.BaseDirectory, "source-fixture.csv")).Skip(1)
    .Select(l => l.Split(',')).Select(p => new { Table = p[0], Grade = int.Parse(p[1]), Column = int.Parse(p[2]),
        Upper = decimal.Parse(p[3], CultureInfo.InvariantCulture), Expected = decimal.Parse(p[4], CultureInfo.InvariantCulture) }).ToArray();
Check(fixture.Length == 757, "Expected all 757 source cells");
foreach (var type in catalog.Types)
{
    var table = catalog.TableFor(type);
    foreach (var cell in fixture.Where(c => c.Table == type.TableId))
    {
        decimal lower = cell.Column == 0 ? 0 : table.Bounds[cell.Column - 1];
        foreach (decimal input in new[] { cell.Upper, cell.Upper - 0.000001m, lower + 0.000001m, (lower + cell.Upper) / 2 })
        {
            Check(catalog.TryQuery(type.Id, cell.Grade, QueryResult.Format(input), out var r, out _), "Query failed");
            Check(r!.Micrometers == cell.Expected, $"Wrong cell: {type.Name}/{cell.Grade}/{input}");
            Check(r.Lower == lower && r.Upper == cell.Upper, "Wrong boundary");
            Check(r.Millimeters * 1000m == cell.Expected, "Unit conversion");
            Check(r.Page == (cell.Grade >= table.ContinuationGrade ? table.ContinuationPage : table.Page), "Wrong source page");
        }
        if (cell.Column < table.Bounds.Length - 1)
        {
            catalog.TryQuery(type.Id, cell.Grade, QueryResult.Format(cell.Upper + 0.000001m), out var next, out _);
            Check(next?.Lower == cell.Upper && next.Upper == table.Bounds[cell.Column + 1], "Upper + epsilon must enter next segment");
        }
    }
    foreach (var text in new[] { "", " ", "-1", "0", "-0.00001", "NaN", "Infinity", "1,000", "1e2", "100 mm", "1.2.3", "1\n2", "10.00000000000000000000000000001", "99999999999999999999999999999999", QueryResult.Format(table.Bounds[^1] + 0.000001m) })
        Check(!catalog.TryQuery(type.Id, 6, text, out var r, out var error) && r is null && error.Length > 0, $"Accepted invalid input {text}");
    foreach (var grade in new[] { -1, table.FirstGrade - 1, 13, int.MaxValue })
        Check(!catalog.TryQuery(type.Id, grade, "1", out _, out _), "Invalid grade");
}
void Sample(string id, int grade, string input, decimal expected)
{
    Check(catalog.TryQuery(id, grade, input, out var r, out _), "Sample query");
    Check(r!.Micrometers == expected, "Sample value");
}
Sample("flatness", 6, "100", 10m);
Sample("straightness", 6, "10", 3m);
Sample("straightness", 6, "10.1", 4m);
Sample("roundness", 0, "3", 0.1m);
Sample("roundness", 0, " 3.0 ", 0.1m);
Check(!catalog.TryQuery("unknown", 6, "100", out _, out _), "Unknown type");
Check(QueryResult.Format(0.0001m) == "0.0001", "Small decimal formatting");
Check(QueryResult.Format(0.0100m) == "0.01", "Trailing zeros");
GeneralTests.Run(catalog, Check);
SizeTests.Run(Check);
RefactorTests.Run(Check);
Console.WriteLine($"PASS: {checks} checks; numeric 757 cells; H/K/L 45 cells; 7283 hole/shaft cells, 56 zones; independent IT-width and fit-result crosschecks; boundaries, units, invalid inputs, guidance, settings and precision.");
