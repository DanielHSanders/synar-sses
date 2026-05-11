using ClosedXML.Excel;
using SynarSSES.Core.Models;
using SynarSSES.Core.Services;
using Xunit;

namespace SynarSSES.Tests;

// Validation against the 2025 KY submission. The reference xlsx
// (Synar2025_SSES_Final.xlsx) lives in the sibling `Synar/SSES/` folder --
// it is the official output produced by the legacy SSES program from the
// same microdata we now expect to reproduce. If we can match the headline
// numbers on Table 2 ("Total" row) the algorithm port is on solid ground.
public sealed class Ky2025ValidationTests
{
    [Fact]
    public void ReproducesKy2025TotalRow()
    {
        var goldenPath = LocateGoldenFile()
            ?? throw new FileNotFoundException(
                "Synar2025_SSES_Final.xlsx not found. Set SSES_GOLDEN_XLSX or place it at ../SSES/Synar2025_SSES_Final.xlsx.");

        var microdata = LoadTable5(goldenPath);
        Assert.Equal(376, microdata.Count);

        var input = new SssInput
        {
            StateCode = "KY",
            FederalFiscalYear = 2026,
            Microdata = microdata,
        };

        var report = new SssCalculator().Compute(input, withFpc: true);

        // Numbers from Synar2025_SSES_Final.xlsx Table 2 "Total" row.
        Assert.Equal(4452, report.Overall.FrameSize);
        Assert.Equal(376,  report.Overall.SampleSize);
        Assert.Equal(348,  report.Overall.EligibleSampleSize);
        Assert.Equal(348,  report.Overall.InspectedCount);
        Assert.Equal(35,   report.Overall.ViolationCount);
        Assert.Equal(4120.0, report.Overall.EstimatedPopulationSize, precision: 0);
        Assert.Equal(0.10057471264367722, report.Overall.WeightedRvr, precision: 10);
        Assert.Equal(0.015447390432283435, report.Overall.StandardError, precision: 10);
    }

    private static string? LocateGoldenFile()
    {
        var env = Environment.GetEnvironmentVariable("SSES_GOLDEN_XLSX");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;

        // Walk up from the test binary looking for `SSES/Synar2025_SSES_Final.xlsx`.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "SSES", "Synar2025_SSES_Final.xlsx");
            if (File.Exists(candidate)) return candidate;
            candidate = Path.Combine(dir.FullName, "..", "SSES", "Synar2025_SSES_Final.xlsx");
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            dir = dir.Parent;
        }
        return null;
    }

    // Reads the "Table 5" sheet of Synar2025_SSES_Final.xlsx, whose layout is
    // the SSES microdata grid documented in the manual section 5. Columns:
    //   A SynarFullID   B Stratum    C PopS       D Vstratum   E PopV
    //   F Code          G Viol        H Type       I IA#         J Gender
    //   K Age           L VMsize      M Product    N Outlet      O Asked
    private static List<MicrodataRow> LoadTable5(string path)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet("Table 5");
        var rows = new List<MicrodataRow>();
        // Row 1 is the header. Data starts at row 2 and runs until column A
        // goes empty.
        var rowNum = 2;
        while (true)
        {
            var idCell = ws.Cell(rowNum, 1).GetString();
            if (string.IsNullOrWhiteSpace(idCell)) break;

            rows.Add(new MicrodataRow
            {
                SynarFullId               = idCell,
                SamplingStratum           = ws.Cell(rowNum, 2).GetString(),
                SamplingStratumPopulation = (int)ws.Cell(rowNum, 3).GetDouble(),
                VarianceStratum           = ws.Cell(rowNum, 4).GetString(),
                VarianceStratumPopulation = (int)ws.Cell(rowNum, 5).GetDouble(),
                DispositionCode           = ws.Cell(rowNum, 6).GetString(),
                Violation                 = ReadOptionalBit(ws.Cell(rowNum, 7)),
                OutletType                = ws.Cell(rowNum, 8).GetString(),
                InspectorId               = NullIfBlank(ws.Cell(rowNum, 9).GetString()),
                InspectorGender           = NullIfBlank(ws.Cell(rowNum, 10).GetString()),
                InspectorAge              = ReadOptionalInt(ws.Cell(rowNum, 11)),
                VmFrameSize               = ReadOptionalInt(ws.Cell(rowNum, 12)),
                ProductType               = ReadOptionalInt(ws.Cell(rowNum, 13)),
                RetailOutletType          = ReadOptionalInt(ws.Cell(rowNum, 14)),
                AskedForId                = NullIfBlank(ws.Cell(rowNum, 15).GetString()),
            });
            rowNum++;
        }
        return rows;
    }

    private static bool? ReadOptionalBit(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        var s = cell.GetString().Trim();
        if (s == "") return null;
        return s == "1";
    }

    private static int? ReadOptionalInt(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        var s = cell.GetString().Trim();
        if (s == "") return null;
        return (int)cell.GetDouble();
    }

    private static string? NullIfBlank(string s)
        => string.IsNullOrWhiteSpace(s) ? null : s;
}
