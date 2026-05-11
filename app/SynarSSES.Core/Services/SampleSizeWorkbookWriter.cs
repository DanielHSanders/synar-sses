using ClosedXML.Excel;
using SynarSSES.Core.Models;

namespace SynarSSES.Core.Services;

// Writes the sample-size calculator output to a two-sheet xlsx:
//   Sheet 1 "Calculation" -- inputs + Effective/Target/Original sample sizes
//   Sheet 2 "Sample"      -- the drawn outlets, one per row, with id/name/address
public sealed class SampleSizeWorkbookWriter
{
    public byte[] Write(SampleSizeResult result, IReadOnlyList<SampledOutlet> drawn,
                       CheckTypeAssignmentSummary assignment,
                       int checkYear, string stateCode)
    {
        using var wb = new XLWorkbook();
        WriteCalculation(wb.AddWorksheet("Calculation"), result, assignment, checkYear, stateCode);
        WriteSample(wb.AddWorksheet("Sample"), drawn);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteCalculation(IXLWorksheet ws, SampleSizeResult r,
        CheckTypeAssignmentSummary a, int year, string state)
    {
        ws.Cell("A1").Value = "SSES Sample Size Calculation";
        ws.Cell("A3").Value = "State";                       ws.Cell("B3").Value = state;
        ws.Cell("A4").Value = "Planning Year";               ws.Cell("B4").Value = year;
        ws.Cell("A5").Value = "Generated";                   ws.Cell("B5").Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        ws.Cell("A7").Value = "Inputs";
        ws.Cell("A8").Value  = "Frame size";                 ws.Cell("B8").Value  = r.Input.FrameSize;
        ws.Cell("A9").Value  = "Expected RVR (%)";           ws.Cell("B9").Value  = r.Input.ExpectedRvrPercent;
        ws.Cell("A10").Value = "Design Effect";              ws.Cell("B10").Value = r.Input.DesignEffect;
        ws.Cell("A11").Value = "Accuracy Rate (%)";          ws.Cell("B11").Value = r.Input.AccuracyRatePercent;
        ws.Cell("A12").Value = "Completion Rate (%)";        ws.Cell("B12").Value = r.Input.CompletionRatePercent;
        ws.Cell("A13").Value = "Safety Margin (%)";          ws.Cell("B13").Value = r.Input.SafetyMarginPercent;
        ws.Cell("A14").Value = "Precision Target";           ws.Cell("B14").Value = r.Input.PrecisionTarget;
        ws.Cell("A15").Value = "CI Type";                    ws.Cell("B15").Value = r.Input.UseOneSidedCi ? "One-sided 95% (z=1.645)" : "Two-sided 95% (z=1.96)";

        ws.Cell("A17").Value = "Results";
        ws.Cell("A18").Value = "Effective Sample Size";      ws.Cell("B18").Value = r.EffectiveSampleSize;
        ws.Cell("A19").Value = "Target Sample Size";         ws.Cell("B19").Value = r.TargetSampleSize;
        ws.Cell("A20").Value = "Original Sample Size";       ws.Cell("B20").Value = r.OriginalSampleSize;
        ws.Cell("A21").Value = "(Use the Original Sample Size on the Sample sheet.)";

        ws.Cell("A23").Value = "CheckType Assignment";
        ws.Cell("A24").Value = "Category";   ws.Cell("B24").Value = "Target"; ws.Cell("C24").Value = "Assigned";
        ws.Cell("A25").Value = "Cigarette";  ws.Cell("B25").Value = a.CigaretteTarget;  ws.Cell("C25").Value = a.CigaretteCount;
        ws.Cell("A26").Value = "Smokeless";  ws.Cell("B26").Value = a.SmokelessTarget;  ws.Cell("C26").Value = a.SmokelessCount;
        ws.Cell("A27").Value = "Electronic"; ws.Cell("B27").Value = a.ElectronicTarget; ws.Cell("C27").Value = a.ElectronicCount;
        ws.Cell("A28").Value = "Menthol";    ws.Cell("B28").Value = a.MentholTarget;    ws.Cell("C28").Value = a.MentholCount;
        if (!string.IsNullOrEmpty(a.ElectronicWarning))
        {
            ws.Cell("A30").Value = "Warning";
            ws.Cell("B30").Value = a.ElectronicWarning;
        }

        ws.Columns("A:C").AdjustToContents();
    }

    private static void WriteSample(IXLWorksheet ws, IReadOnlyList<SampledOutlet> drawn)
    {
        var headers = new[]
        {
            "Synar Maps ID", "Name", "Address", "City", "State", "Zip",
            "Latitude", "Longitude", "Business Type", "CheckType",
        };
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

        var row = 2;
        foreach (var o in drawn)
        {
            ws.Cell(row, 1).Value = o.SynarMapsId;
            ws.Cell(row, 2).Value = o.Name;
            ws.Cell(row, 3).Value = o.Address;
            ws.Cell(row, 4).Value = o.City;
            ws.Cell(row, 5).Value = o.State;
            ws.Cell(row, 6).Value = o.Zip;
            ws.Cell(row, 7).Value = o.Latitude;
            ws.Cell(row, 8).Value = o.Longitude;
            ws.Cell(row, 9).Value = o.BusinessType ?? "";
            ws.Cell(row, 10).Value = o.CheckType ?? "";
            row++;
        }
        ws.Columns("A:J").AdjustToContents();
    }
}
