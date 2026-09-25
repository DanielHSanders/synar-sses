using System.Globalization;
using ClosedXML.Excel;
using SynarSSES.Core.Models;
using SynarSSES.Core.Services;
using Xunit;

namespace SynarSSES.Tests;

// Parity against the legacy SSES v7.0 Excel program. Each reference workbook
// below is output that SSES itself produced; its Table 5 sheet reproduces the
// microdata SSES was given, so feeding that back through this app must
// reproduce the rest of the workbook.
//
// The reference files live outside the repo, in the Synar/SSES folder. Point
// SSES_GOLDEN_DIR at it, or run from a checkout that sits beneath it.
public sealed class SsesParityTests
{
    [Fact]
    public void ReproducesKy2025TotalRow()
    {
        var report = Compute(Golden("Synar2025_SSES_Final.xlsx"), 2026, 303, 303);

        Assert.Equal(4452, report.Overall.FrameSize);
        Assert.Equal(376,  report.Overall.SampleSize);
        Assert.Equal(348,  report.Overall.EligibleSampleSize);
        Assert.Equal(348,  report.Overall.InspectedCount);
        Assert.Equal(35,   report.Overall.ViolationCount);
        Assert.Equal(4120.0, report.Overall.EstimatedPopulationSize, precision: 0);
        Assert.Equal(0.10057471264367722, report.Overall.WeightedRvr, precision: 10);
        Assert.Equal(0.015447390432283435, report.Overall.StandardError, precision: 10);
    }

    // Every cell of every sheet, against what SSES produced from the same
    // microdata. The effective and target sample sizes are the values the
    // operator typed in when SSES ran.
    [Theory]
    [InlineData("Synar2025_SSES_Final.xlsx", 2026, 303, 303)]   // FFY 2026 submission
    [InlineData("Synar2026_SSES_Final.xlsx", 2027, 256, 256)]   // FFY 2027, run by Tim
    public void WorkbookMatchesLegacySses(string file, int ffy, int effective, int target)
    {
        var goldenPath = Golden(file);
        var bytes = new SssWorkbookWriter().Write(Compute(goldenPath, ffy, effective, target));

        using var produced = new XLWorkbook(new MemoryStream(bytes));
        using var golden = new XLWorkbook(goldenPath);

        Assert.Equal(golden.Worksheets.Select(w => w.Name), produced.Worksheets.Select(w => w.Name));

        var diffs = new List<string>();
        foreach (var gws in golden.Worksheets)
        {
            var pws = produced.Worksheet(gws.Name);
            var addresses = gws.CellsUsed(XLCellsUsedOptions.Contents)
                .Concat(pws.CellsUsed(XLCellsUsedOptions.Contents))
                .Select(c => c.Address.ToString()!)
                .Distinct();
            foreach (var a in addresses)
            {
                if (Expected.Contains((gws.Name, a))) continue;
                var g = gws.Cell(a).Value;
                var p = pws.Cell(a).Value;
                if (!Same(g, p)) diffs.Add($"{gws.Name}!{a}: SSES={Show(g)} ours={Show(p)}");
            }
        }

        Assert.True(diffs.Count == 0,
            $"{diffs.Count} cells differ from {file}:\n" + string.Join("\n", diffs.Take(60)));
    }

    // Cells that legitimately differ from a run of the legacy program.
    private static readonly HashSet<(string Sheet, string Cell)> Expected = new()
    {
        ("Table1", "C6"),   // run date
        ("Table1", "C7"),   // SSES prints the input file name; this app names its source
        ("Table1", "C8"),   // program version -- deliberately not "Version 7.0"
        ("Table 5", "J1"),  // Table 5 headers echo the input file, which used
        ("Table 5", "K1"),  //   "(No column name)" for these in 2025
    };

    private static SssReport Compute(string goldenPath, int ffy, int effective, int target) =>
        new SssCalculator().Compute(new SssInput
        {
            StateCode = "KY",
            FederalFiscalYear = ffy,
            Microdata = LoadTable5(goldenPath),
            EffectiveSampleSize = effective,
            TargetSampleSize = target,
        }, withFpc: true);

    private static bool Same(XLCellValue a, XLCellValue b)
    {
        object? x = Normalise(a), y = Normalise(b);
        if (x is double dx && y is double dy)
            return Math.Abs(dx - dy) <= 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(dx), Math.Abs(dy)));
        return Equals(x, y);
    }

    // Numbers stored as text and blank strings compare as their plain values,
    // so formatting choices do not show up as differences.
    private static object? Normalise(XLCellValue v)
    {
        if (v.IsBlank) return null;
        if (v.IsNumber) return v.GetNumber();
        if (v.IsBoolean) return v.GetBoolean();
        if (v.IsDateTime) return v.GetDateTime();
        var s = v.ToString().Trim();
        if (s.Length == 0) return null;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : s;
    }

    private static string Show(XLCellValue v) => v.IsBlank ? "(blank)" : "'" + v + "'";

    private static string Golden(string file)
    {
        var env = Environment.GetEnvironmentVariable("SSES_GOLDEN_DIR");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var p = Path.Combine(env, file);
            if (File.Exists(p)) return p;
        }
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var p = Path.Combine(dir.FullName, "SSES", file);
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException(
            $"{file} not found. Set SSES_GOLDEN_DIR to the folder holding the SSES reference workbooks.");
    }

    // Reads the "Table 5" sheet: the SSES microdata grid from section 5 of the
    // manual. Columns:
    //   A SynarFullID   B Stratum    C PopS       D Vstratum   E PopV
    //   F Code          G Viol       H Type       I IA#        J Gender
    //   K Age           L VMsize     M Product    N Outlet     O Asked
    private static List<MicrodataRow> LoadTable5(string path)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet("Table 5");
        var rows = new List<MicrodataRow>();
        for (var r = 2; !string.IsNullOrWhiteSpace(ws.Cell(r, 1).GetString()); r++)
        {
            rows.Add(new MicrodataRow
            {
                SynarFullId               = ws.Cell(r, 1).GetString(),
                SamplingStratum           = ws.Cell(r, 2).GetString(),
                SamplingStratumPopulation = (int)ws.Cell(r, 3).GetDouble(),
                VarianceStratum           = ws.Cell(r, 4).GetString(),
                VarianceStratumPopulation = (int)ws.Cell(r, 5).GetDouble(),
                DispositionCode           = ws.Cell(r, 6).GetString(),
                Violation                 = ReadOptionalBit(ws.Cell(r, 7)),
                OutletType                = NullIfBlank(ws.Cell(r, 8).GetString()),
                InspectorId               = NullIfBlank(ws.Cell(r, 9).GetString()),
                InspectorGender           = NullIfBlank(ws.Cell(r, 10).GetString()),
                InspectorAge              = ReadOptionalInt(ws.Cell(r, 11)),
                VmFrameSize               = ReadOptionalInt(ws.Cell(r, 12)),
                ProductType               = ReadOptionalInt(ws.Cell(r, 13)),
                RetailOutletType          = ReadOptionalInt(ws.Cell(r, 14)),
                AskedForId                = NullIfBlank(ws.Cell(r, 15).GetString()),
            });
        }
        return rows;
    }

    private static bool? ReadOptionalBit(IXLCell cell)
    {
        var s = cell.GetString().Trim();
        return s == "" ? null : s == "1";
    }

    private static int? ReadOptionalInt(IXLCell cell)
    {
        var s = cell.GetString().Trim();
        return s == "" ? null : (int)cell.GetDouble();
    }

    private static string? NullIfBlank(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
