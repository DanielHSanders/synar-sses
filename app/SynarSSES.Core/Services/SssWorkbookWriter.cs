using ClosedXML.Excel;
using SynarSSES.Core.Models;

namespace SynarSSES.Core.Services;

// Writes an SssReport to the SAMHSA-formatted xlsx workbook (Tables 1-8).
// Cell positions match the layout of Synar2025_SSES_Final.xlsx so the
// resulting file is a drop-in replacement.
public sealed class SssWorkbookWriter
{
    public byte[] Write(SssReport report)
    {
        using var wb = new XLWorkbook();
        WriteTable1(wb.AddWorksheet("Table1"),   report);
        WriteTable2(wb.AddWorksheet("Table2"),   report);
        WriteTable3(wb.AddWorksheet("Table3"),   report);
        WriteTable4(wb.AddWorksheet("Table4"),   report);
        WriteTable5(wb.AddWorksheet("Table 5"),  report);
        WriteTable6(wb.AddWorksheet("Table6"),   report);
        WriteTable7(wb.AddWorksheet("Table7"),   report);
        WriteTable8(wb.AddWorksheet("Table8"),   report);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // Table 1: cover sheet with headline numbers. Matches the row layout of
    // Synar2025_SSES_Final.xlsx (R3=CSAP-SYNAR REPORT, R4=State, etc.).
    private static void WriteTable1(IXLWorksheet ws, SssReport report)
    {
        ws.Cell("A1").Value = "SSES Table 1 (Synar Survey Estimates and Sample Sizes)";
        ws.Cell("B3").Value = "CSAP-SYNAR REPORT";
        ws.Cell("B4").Value = "State";                                ws.Cell("C4").Value = report.StateCode;
        ws.Cell("B5").Value = "Federal Fiscal Year (FFY)";            ws.Cell("C5").Value = report.FederalFiscalYear;
        ws.Cell("B6").Value = "Date";                                 ws.Cell("C6").Value = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss");
        ws.Cell("B7").Value = "Data";                                 ws.Cell("C7").Value = "synarcheck";
        ws.Cell("B8").Value = "Program Version";                      ws.Cell("C8").Value = "SynarSSES 0.1";
        ws.Cell("B9").Value = "Analysis Option";                      ws.Cell("C9").Value = "Stratified SRS with FPC";

        ws.Cell("B11").Value = "Estimates";
        ws.Cell("B12").Value = "Unweighted Retailer Violation Rate";  ws.Cell("C12").Value = report.Overall.UnweightedRvr;
        ws.Cell("B13").Value = "Weighted Retailer Violation Rate";    ws.Cell("C13").Value = report.Overall.WeightedRvr;
        ws.Cell("B14").Value = "Standard Error";                      ws.Cell("C14").Value = report.Overall.StandardError;
        ws.Cell("B15").Value = "Is SAMHSA Precision Requirement Met"; ws.Cell("C15").Value = report.Overall.SamhsaPrecisionMet ? "YES" : "NO";
        ws.Cell("B16").Value = "Right-sided 95% Confidence Interval"; ws.Cell("C16").Value = FormatOneSidedCi(report.Overall);
        ws.Cell("B17").Value = "Two-sided 95% Confidence Interval";   ws.Cell("C17").Value = FormatTwoSidedCi(report.Overall);
        ws.Cell("B18").Value = "Design Effect";                       ws.Cell("C18").Value = report.Overall.DesignEffect3;
        ws.Cell("B19").Value = "Accuracy Rate (unweighted)";          ws.Cell("C19").Value = report.Overall.UnweightedAccuracyRate;
        ws.Cell("B20").Value = "Accuracy Rate (weighted)";            ws.Cell("C20").Value = report.Overall.WeightedAccuracyRate;
        ws.Cell("B21").Value = "Completion Rate (unweighted)";        ws.Cell("C21").Value = report.Overall.CompletionRate;
    }

    private static string FormatOneSidedCi(OverallStats o)
        => $"[0.0%, {o.CiUpperOneSided95 * 100:0.0}%]";

    private static string FormatTwoSidedCi(OverallStats o)
        => $"[{o.CiLower95 * 100:0.0}%, {o.CiUpper95 * 100:0.0}%]";

    // Table 2: per-stratum results, split into All Outlets / OTC / VM
    // sections. The "Total" row at the bottom of each section is the only
    // place where the standard error appears.
    private static void WriteTable2(IXLWorksheet ws, SssReport report)
    {
        ws.Cell("A1").Value = "SSES Table 2 (Synar Survey Results by Stratum)";
        ws.Cell("J1").Value = $"STATE: {report.StateCode}";
        ws.Cell("J2").Value = $"FFY: {report.FederalFiscalYear}";

        // Header row 4
        var headers = new[]
        {
            "Samp. Stratum", "Var. Stratum", "Outlet Frame Size",
            "Estimated Outlet Population Size",
            "Number of PSU Clusters Created", "Number of PSU Clusters in Sample",
            "Outlet Sample Size", "Number of Eligible Outlets in Sample",
            "Number of Sample Outlets Inspected", "Number of Sample Outlets in Violation",
            "Retailer Violation Rate(%)", "Standard Error(%)",
        };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(4, i + 1).Value = headers[i];

        // For KY: only one stratum, and all outlets are treated as OTC. The
        // VM section is emitted but zeroed. Sections start at rows 5, 8, 11.
        WriteTable2Section(ws, 5,  "All Outlets",              report);
        WriteTable2Section(ws, 8,  "Over the Counter Outlets", report);
        WriteTable2Section(ws, 11, "Vending Machines",         report, zeroed: true);
    }

    private static void WriteTable2Section(IXLWorksheet ws, int headerRow, string label, SssReport report, bool zeroed = false)
    {
        ws.Cell(headerRow, 1).Value = label;
        var dataRow = headerRow + 1;
        foreach (var s in report.StratumResults)
        {
            ws.Cell(dataRow, 1).Value = s.SamplingStratumId;
            ws.Cell(dataRow, 2).Value = s.VarianceStratumId;
            ws.Cell(dataRow, 3).Value = zeroed ? 0 : s.OutletFrameSize;
            ws.Cell(dataRow, 4).Value = zeroed ? 0 : s.EstimatedPopulationSize;
            ws.Cell(dataRow, 5).Value = "N/A";
            ws.Cell(dataRow, 6).Value = "N/A";
            ws.Cell(dataRow, 7).Value = zeroed ? 0 : s.OutletSampleSize;
            ws.Cell(dataRow, 8).Value = zeroed ? 0 : s.EligibleOutletsInSample;
            ws.Cell(dataRow, 9).Value = zeroed ? 0 : s.InspectedCount;
            ws.Cell(dataRow, 10).Value = zeroed ? 0 : s.ViolationCount;
            ws.Cell(dataRow, 11).Value = zeroed ? 0 : s.ViolationRate;
            dataRow++;
        }
        ws.Cell(dataRow, 1).Value = "Total";
        ws.Cell(dataRow, 3).Value = zeroed ? 0 : report.StratumResults.Sum(s => s.OutletFrameSize);
        ws.Cell(dataRow, 4).Value = zeroed ? 0 : report.StratumResults.Sum(s => s.EstimatedPopulationSize);
        ws.Cell(dataRow, 7).Value = zeroed ? 0 : report.Overall.SampleSize;
        ws.Cell(dataRow, 8).Value = zeroed ? 0 : report.Overall.EligibleSampleSize;
        ws.Cell(dataRow, 9).Value = zeroed ? 0 : report.Overall.InspectedCount;
        ws.Cell(dataRow, 10).Value = zeroed ? 0 : report.Overall.ViolationCount;
        ws.Cell(dataRow, 11).Value = zeroed ? 0 : report.Overall.WeightedRvr;
        ws.Cell(dataRow, 12).Value = zeroed ? 0 : report.Overall.StandardError;
    }

    private static readonly (string Code, string Description, char Section)[] DispositionRows =
    {
        ("EC", "Eligible and inspection complete outlet",                'E'),
        ("N1", "In operation but closed at time of visit",                'N'),
        ("N2", "Unsafe to access",                                         'N'),
        ("N3", "Presence of police",                                       'N'),
        ("N4", "Youth inspector knows salesperson",                        'N'),
        ("N5", "Moved to new location but not inspected",                  'N'),
        ("N6", "Drive thru only/youth inspector has no drivers license",   'N'),
        ("N7", "Tobacco out of stock",                                     'N'),
        ("N8", "Run out of time",                                          'N'),
        ("N9", "Other noncompletion",                                      'N'),
        ("I1", "Out of Business",                                          'I'),
        ("I2", "Does not sell tobacco products",                           'I'),
        ("I3", "Inaccessible by youth",                                    'I'),
        ("I4", "Private club or private residence",                        'I'),
        ("I5", "Temporary closure",                                        'I'),
        ("I6", "Can't be located",                                         'I'),
        ("I7", "Wholesale only/Carton sale only",                          'I'),
        ("I8", "Vending machine broken",                                   'I'),
        ("I9", "Duplicate",                                                'I'),
        ("I10","Other ineligibility",                                      'I'),
    };

    // Table 3: sample tally by disposition code, with subtotals for the three
    // sections (Eligible Completes, Eligible Noncompletes, Ineligible).
    private static void WriteTable3(IXLWorksheet ws, SssReport report)
    {
        ws.Cell("A1").Value = "SSES Table 3 (Synar Survey Sample Tally by Disposition Code)";
        ws.Cell("D1").Value = $"STATE: {report.StateCode}";
        ws.Cell("D2").Value = $"FFY: {report.FederalFiscalYear}";

        ws.Cell("B4").Value = "Disposition Code";
        ws.Cell("C4").Value = "Description";
        ws.Cell("D4").Value = "Count";
        ws.Cell("E4").Value = "Subtotal";

        var row = 5;
        int ecSubtotal = 0, nSubtotal = 0, iSubtotal = 0;
        char prevSection = ' ';
        foreach (var (code, description, section) in DispositionRows)
        {
            if (prevSection != ' ' && prevSection != section)
            {
                WriteSubtotalRow(ws, row++, prevSection, ecSubtotal, nSubtotal, iSubtotal);
            }
            var count = report.Tally.CountsByCode.TryGetValue(code, out var c) ? c : 0;
            ws.Cell(row, 2).Value = code;
            ws.Cell(row, 3).Value = description;
            ws.Cell(row, 4).Value = count;
            row++;
            if (section == 'E') ecSubtotal += count;
            if (section == 'N') nSubtotal += count;
            if (section == 'I') iSubtotal += count;
            prevSection = section;
        }
        WriteSubtotalRow(ws, row++, 'I', ecSubtotal, nSubtotal, iSubtotal);
        ws.Cell(row, 2).Value = "Grand Total";
        ws.Cell(row, 5).Value = ecSubtotal + nSubtotal + iSubtotal;
    }

    private static void WriteSubtotalRow(IXLWorksheet ws, int row, char justClosedSection, int ec, int n, int i)
    {
        var (label, value) = justClosedSection switch
        {
            'E' => ("Total (Eligible Completes)",    ec),
            'N' => ("Total (Eligible Noncompletes)", n),
            'I' => ("Total (Ineligible)",            i),
            _   => ("",                              0),
        };
        ws.Cell(row, 2).Value = label;
        ws.Cell(row, 5).Value = value;
    }

    // Table 4: inspector demographics (gender x age 14..20). For each gender
    // block, list ages 14-20 then a Subtotal row. End with a Grand Total.
    private static void WriteTable4(IXLWorksheet ws, SssReport report)
    {
        ws.Cell("A1").Value = "SSES Table 4 (Synar Survey Inspection Results by Inspector Demographics)";
        ws.Cell("H3").Value = $"STATE: {report.StateCode}";
        ws.Cell("H4").Value = $"FFY: {report.FederalFiscalYear}";
        ws.Cell("C5").Value = "Frequency Distribution";
        ws.Cell("C6").Value = "Gender";
        ws.Cell("D6").Value = "Age";
        ws.Cell("E6").Value = "Number of Inspectors";
        ws.Cell("F6").Value = "Attempted Buys";
        ws.Cell("G6").Value = "Successful Buys";

        var row = 7;
        foreach (var gender in new[] { ("M", "Male"), ("F", "Female") })
        {
            ws.Cell(row, 3).Value = gender.Item2;
            var subtotalInspectors = 0;
            var subtotalAttempts = 0;
            var subtotalSuccesses = 0;
            for (var age = 14; age <= 20; age++)
            {
                var cell = report.Inspectors.Cells.FirstOrDefault(c => c.Gender == gender.Item1 && c.Age == age);
                ws.Cell(row, 4).Value = age;
                ws.Cell(row, 5).Value = cell?.InspectorCount ?? 0;
                ws.Cell(row, 6).Value = cell?.AttemptedBuys  ?? 0;
                ws.Cell(row, 7).Value = cell?.SuccessfulBuys ?? 0;
                subtotalInspectors += cell?.InspectorCount  ?? 0;
                subtotalAttempts   += cell?.AttemptedBuys   ?? 0;
                subtotalSuccesses  += cell?.SuccessfulBuys  ?? 0;
                row++;
            }
            ws.Cell(row, 4).Value = "Subtotal";
            ws.Cell(row, 5).Value = subtotalInspectors;
            ws.Cell(row, 6).Value = subtotalAttempts;
            ws.Cell(row, 7).Value = subtotalSuccesses;
            row++;
        }
        ws.Cell(row, 3).Value = "Total";
        ws.Cell(row, 5).Value = report.Inspectors.Cells.Sum(c => c.InspectorCount);
        ws.Cell(row, 6).Value = report.Inspectors.Cells.Sum(c => c.AttemptedBuys);
        ws.Cell(row, 7).Value = report.Inspectors.Cells.Sum(c => c.SuccessfulBuys);
    }

    // Table 5: raw microdata passthrough. Headers match the SSES manual
    // section 5 layout. Two columns (J Gender, K Age) have empty headers in
    // the golden file -- we preserve that.
    private static void WriteTable5(IXLWorksheet ws, SssReport report)
    {
        var headers = new[]
        {
            "SynarFullID", "Stratum", "PopS", "Vstratum", "PopV",
            "Code", "Viol", "Type", "IA#", "", "",
            "VMsize", "Product", "Outlet", "Asked",
        };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var row = 2;
        foreach (var m in report.Microdata)
        {
            ws.Cell(row, 1).Value  = m.SynarFullId;
            ws.Cell(row, 2).Value  = m.SamplingStratum;
            ws.Cell(row, 3).Value  = m.SamplingStratumPopulation;
            ws.Cell(row, 4).Value  = m.VarianceStratum;
            ws.Cell(row, 5).Value  = m.VarianceStratumPopulation;
            ws.Cell(row, 6).Value  = m.DispositionCode;
            if (m.Violation == true) ws.Cell(row, 7).Value = 1;
            ws.Cell(row, 8).Value  = m.OutletType ?? "";
            ws.Cell(row, 9).Value  = m.InspectorId ?? "";
            ws.Cell(row, 10).Value = m.InspectorGender ?? "";
            if (m.InspectorAge.HasValue)     ws.Cell(row, 11).Value = m.InspectorAge.Value;
            if (m.VmFrameSize.HasValue)      ws.Cell(row, 12).Value = m.VmFrameSize.Value;
            if (m.ProductType.HasValue)      ws.Cell(row, 13).Value = m.ProductType.Value;
            if (m.RetailOutletType.HasValue) ws.Cell(row, 14).Value = m.RetailOutletType.Value;
            ws.Cell(row, 15).Value = m.AskedForId ?? "";
            row++;
        }
    }

    // Tables 6/7/8: cross-tabs. Left side is the frequency table (category x
    // attempted/successful/rate); right side pivots violation rate by
    // inspector age x gender. For v1 we emit the left side only -- the right
    // side is an audit aid, not part of the headline submission, and the
    // 2025 golden has all-zero pivots for KY (no asked-for-id captured).
    private static void WriteTable6(IXLWorksheet ws, SssReport report)
        => WriteCrossTabFrequency(ws, "SSES Table 6 (Synar Survey Inspection Results by Product Type)", "Product Type", report.ProductCrossTab, report);

    private static void WriteTable7(IXLWorksheet ws, SssReport report)
        => WriteCrossTabFrequency(ws, "SSES Table 7 (Synar Survey Inspection Results by Retail Outlet Type)", "Retail Outlet", report.OutletCrossTab, report);

    private static void WriteTable8(IXLWorksheet ws, SssReport report)
        => WriteCrossTabFrequency(ws, "SSES Table 8 (Synar Survey Inspection Results by Clerk Asked for ID)", "Clerk Asked for ID", report.AskedForIdCrossTab, report);

    private static void WriteCrossTabFrequency(IXLWorksheet ws, string title, string categoryLabel, CrossTab tab, SssReport report)
    {
        ws.Cell("A1").Value = title;
        ws.Cell("D3").Value = $"STATE: {report.StateCode}";
        ws.Cell("D4").Value = $"FFY: {report.FederalFiscalYear}";
        ws.Cell("A7").Value = "Frequency Distribution and Buy Rate";
        ws.Cell("A8").Value = categoryLabel;
        ws.Cell("B8").Value = "Attempted Buys";
        ws.Cell("C8").Value = "Successful Buys";
        ws.Cell("D8").Value = "Violation Rate (%)";

        var row = 9;
        int totalAttempts = 0, totalSuccesses = 0;
        foreach (var r in tab.Rows)
        {
            ws.Cell(row, 1).Value = r.Category;
            ws.Cell(row, 2).Value = r.AttemptedBuys;
            ws.Cell(row, 3).Value = r.SuccessfulBuys;
            ws.Cell(row, 4).Value = r.ViolationRate;
            totalAttempts  += r.AttemptedBuys;
            totalSuccesses += r.SuccessfulBuys;
            row++;
        }
        ws.Cell(row, 1).Value = "Grand Total";
        ws.Cell(row, 2).Value = totalAttempts;
        ws.Cell(row, 3).Value = totalSuccesses;
        ws.Cell(row, 4).Value = totalAttempts > 0 ? (double)totalSuccesses / totalAttempts : 0;
    }
}
