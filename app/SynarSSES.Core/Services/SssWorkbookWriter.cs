using System.Globalization;
using ClosedXML.Excel;
using SynarSSES.Core.Models;

namespace SynarSSES.Core.Services;

// Writes an SssReport to the SAMHSA workbook (Tables 1-8). Cell positions,
// labels and number formats follow what SSES v7.0 itself produces -- checked
// cell by cell against Synar2025_SSES_Final.xlsx and the FFY 2027 run -- so
// the file reads the same to a reviewer who knows the legacy output.
//
// The one deliberate difference is the Program Version line on Table 1. SSES
// stamps "Version 7.0" there, and SAMHSA reads that line to confirm SSES v7.0
// was used; this app says what it actually is.
public sealed class SssWorkbookWriter
{
    public const string ProgramVersion = "SynarSSES (port of SSES Version 7.0)";

    private const string Pct1 = "#,##0.0%";
    private const string Int = "#,##0";
    private const string Pct1Short = "0.0%";
    private const string Pct1Rate = "##0.0%";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public byte[] Write(SssReport report)
    {
        using var wb = new XLWorkbook();
        WriteTable1(wb.AddWorksheet("Table1"), report);
        WriteTable2(wb.AddWorksheet("Table2"), report);
        WriteTable3(wb.AddWorksheet("Table3"), report);
        WriteTable4(wb.AddWorksheet("Table4"), report);
        WriteTable5(wb.AddWorksheet("Table 5"), report);
        WriteCrossTab(wb.AddWorksheet("Table6"), report, report.ProductCrossTab,
            "SSES Table (Synar Survey Inspection Results by Type of Product)",
            "Buy Rate by Type of Product, Age, and Gender", ProductCategories);
        WriteCrossTab(wb.AddWorksheet("Table7"), report, report.OutletCrossTab,
            "SSES Table (Synar Survey Inspection Results by Type of Retail Outlet)",
            "Buy Rate by Type of Retail Outlet, Age, and Gender", RetailOutletCategories);
        WriteCrossTab(wb.AddWorksheet("Table8"), report, report.AskedForIdCrossTab,
            "SSES Table (Synar Survey Inspection Results by Clerk Asked for ID)",
            "Buy Rate by Clerk Asked for ID, Age, and Gender", AskedForIdCategories);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void Set(IXLWorksheet ws, int row, int col, XLCellValue value, string? format = null)
    {
        var c = ws.Cell(row, col);
        c.Value = value;
        if (format is not null) c.Style.NumberFormat.Format = format;
    }

    // Stratum ids that are whole numbers are written as numbers, as SSES does.
    private static XLCellValue Id(string id) =>
        int.TryParse(id, NumberStyles.Integer, Inv, out var n) ? n : id;

    // ---------------------------------------------------------------- Table 1

    private static void WriteTable1(IXLWorksheet ws, SssReport r)
    {
        var o = r.Overall;
        Set(ws, 1, 1, "SSES Table 1 (Synar Survey Estimates and Sample Sizes)");
        Set(ws, 3, 2, "CSAP-SYNAR REPORT");
        Set(ws, 4, 2, "State");                     Set(ws, 4, 3, r.StateCode);
        Set(ws, 5, 2, "Federal Fiscal Year (FFY)"); Set(ws, 5, 3, r.FederalFiscalYear);
        Set(ws, 6, 2, "Date");                      Set(ws, 6, 3, r.GeneratedAt, "m/d/yy h:mm");
        Set(ws, 7, 2, "Data");                      Set(ws, 7, 3, r.DataSource);
        Set(ws, 8, 2, "Program Version");           Set(ws, 8, 3, ProgramVersion);
        Set(ws, 9, 2, "Analysis Option");           Set(ws, 9, 3, r.AnalysisOption);

        Set(ws, 11, 2, "Estimates");
        Set(ws, 12, 2, "Unweighted Retailer Violation Rate");   Set(ws, 12, 3, o.UnweightedRvr, Pct1);
        Set(ws, 13, 2, "Weighted Retailer Violation Rate");     Set(ws, 13, 3, o.WeightedRvr, Pct1);
        Set(ws, 14, 2, "Standard Error");                       Set(ws, 14, 3, o.StandardError, Pct1);
        Set(ws, 15, 2, "Is SAMHSA Precision Requirement met?"); Set(ws, 15, 3, o.SamhsaPrecisionMet ? "YES" : "NO");
        Set(ws, 16, 2, "Right-sided 95% Confidence Interval");
        Set(ws, 16, 3, "[0.0%, " + Percent(o.CiUpperOneSided95) + "]");
        Set(ws, 17, 2, "Two-sided 95% Confidence Interval");
        Set(ws, 17, 3, "[" + Percent(o.CiLower95) + ", " + Percent(o.CiUpper95) + "]");
        Set(ws, 18, 2, "Design Effect");                  Set(ws, 18, 3, o.DesignEffect3, "#,##0.0");
        Set(ws, 19, 2, "Accuracy Rate (unweighted)");     Set(ws, 19, 3, o.UnweightedAccuracyRate, Pct1);
        Set(ws, 20, 2, "Accuracy Rate (weighted)");       Set(ws, 20, 3, o.WeightedAccuracyRate, Pct1);
        Set(ws, 21, 2, "Completion Rate (unweighted)");   Set(ws, 21, 3, o.CompletionRate, Pct1);

        // Effective and target sizes are what the operator types in when SSES
        // runs; the remaining four are derived from the data.
        Set(ws, 23, 2, "Sample Size for Current Year");
        Set(ws, 24, 2, "Effective Sample Size");
        if (r.EffectiveSampleSize is int eff) Set(ws, 24, 3, eff, Int);
        Set(ws, 25, 2, "Target (Minimum) Sample Size");
        if (r.TargetSampleSize is int tss) Set(ws, 25, 3, tss, Int);
        Set(ws, 26, 2, "Original Sample Size");  Set(ws, 26, 3, o.SampleSize, Int);
        Set(ws, 27, 2, "Eligible Sample Size "); Set(ws, 27, 3, o.EligibleSampleSize, Int);
        Set(ws, 28, 2, "Final Sample Size");     Set(ws, 28, 3, o.InspectedCount);
        Set(ws, 29, 2, "Overall Sampling Rate"); Set(ws, 29, 3, o.OverallSamplingRate, Pct1);
    }

    // VBA Format(x, "0.0%").
    private static string Percent(double x) =>
        (x * 100).ToString("0.0", Inv) + "%";

    // ---------------------------------------------------------------- Table 2

    private static void WriteTable2(IXLWorksheet ws, SssReport r)
    {
        Set(ws, 1, 1, "SSES Table 2 (Synar Survey Results by Stratum and by OTC/VM)");
        Set(ws, 1, 10, "STATE: " + r.StateCode);
        Set(ws, 2, 10, "FFY: " + r.FederalFiscalYear);

        var headers = new[]
        {
            "Samp. Stratum", "Var. Stratum", "Outlet Frame Size", "Estimated Outlet Population Size",
            "Number of PSU Clusters Created", "Number of PSU Clusters in Sample", "Outlet Sample Size",
            "Number of Eligible Outlets in Sample", "Number of Sample Outlets Inspected",
            "Number of Sample Outlets in Violation", "Retailer Violation Rate(%)", "Standard Error(%)",
        };
        for (var i = 0; i < headers.Length; i++) Set(ws, 4, i + 1, headers[i], "@");

        var row = 5;
        foreach (var section in r.Table2)
        {
            Set(ws, row++, 1, section.Label);
            foreach (var s in section.Strata)
            {
                Set(ws, row, 1, Id(s.SamplingStratumId));
                Set(ws, row, 2, Id(s.VarianceStratumId));
                WriteTable2Counts(ws, row, s);
                Set(ws, row, 5, "N/A");
                Set(ws, row, 6, "N/A");
                row++;
            }
            Set(ws, row, 1, "Total");
            WriteTable2Counts(ws, row, section.Total);
            Set(ws, row, 12, section.StandardError, Pct1);
            row++;
        }

        if (r.HasUnknownOutletType)
        {
            Set(ws, row + 1, 1, "Note:");
            Set(ws, row + 1, 2, "There are some records with unknown outlet type. Therefore the overall counts may not equal the sum of OTC and VM counts.");
        }
    }

    private static void WriteTable2Counts(IXLWorksheet ws, int row, Table2Row s)
    {
        Set(ws, row, 3, s.OutletFrameSize, Int);
        Set(ws, row, 4, s.EstimatedPopulationSize, Int);
        Set(ws, row, 7, s.OutletSampleSize, Int);
        Set(ws, row, 8, s.EligibleOutletsInSample, Int);
        Set(ws, row, 9, s.InspectedCount, Int);
        Set(ws, row, 10, s.ViolationCount, Int);
        Set(ws, row, 11, s.ViolationRate, Pct1);
    }

    // ---------------------------------------------------------------- Table 3

    private static readonly (string Code, string Description, char Section)[] DispositionRows =
    {
        ("EC", "Eligible and inspection complete outlet",              'E'),
        ("N1", "In operation but closed at time of visit",              'N'),
        ("N2", "Unsafe to access",                                       'N'),
        ("N3", "Presence of police",                                     'N'),
        ("N4", "Youth inspector knows salesperson",                      'N'),
        ("N5", "Moved to new location but not inspected",                'N'),
        ("N6", "Drive thru only/youth inspector has no drivers license", 'N'),
        ("N7", "Tobacco out of stock",                                   'N'),
        ("N8", "Run out of time",                                        'N'),
        ("N9", "Other noncompletion",                                    'N'),
        ("I1", "Out of Business",                                        'I'),
        ("I2", "Does not sell tobacco products",                         'I'),
        ("I3", "Inaccessible by youth",                                  'I'),
        ("I4", "Private club or private residence",                      'I'),
        ("I5", "Temporary closure",                                      'I'),
        ("I6", "Can't be located",                                       'I'),
        ("I7", "Wholesale only/Carton sale only",                        'I'),
        ("I8", "Vending machine broken",                                 'I'),
        ("I9", "Duplicate",                                              'I'),
        ("I10", "Other ineligibility",                                   'I'),
    };

    private static readonly Dictionary<char, string> SubtotalLabels = new()
    {
        ['E'] = "Total (Eligible Completes)",
        ['N'] = "Total (Eligible Noncompletes)",
        ['I'] = "Total (Ineligibles)",
    };

    private static void WriteTable3(IXLWorksheet ws, SssReport r)
    {
        Set(ws, 1, 1, "SSES Table 3 (Synar Survey Sample Tally Summary)");
        Set(ws, 1, 4, "STATE: " + r.StateCode);
        Set(ws, 2, 4, "FFY: " + r.FederalFiscalYear);
        Set(ws, 4, 2, "Disposition Code");
        Set(ws, 4, 3, "Description");
        Set(ws, 4, 4, "Count");
        Set(ws, 4, 5, "Subtotal");

        int Count(string code) => r.Tally.CountsByCode.TryGetValue(code, out var c) ? c : 0;
        var otherNoncomplete = Count("N9") > 0;
        var otherIneligible = Count("I10") > 0;

        var row = 5;
        var grand = 0;
        foreach (var section in new[] { 'E', 'N', 'I' })
        {
            var subtotal = 0;
            foreach (var (code, description, sec) in DispositionRows.Where(d => d.Section == section))
            {
                var n = Count(code);
                var seeBelow = (code == "N9" && otherNoncomplete) || (code == "I10" && otherIneligible);
                Set(ws, row, 2, code);
                Set(ws, row, 3, seeBelow ? description + " (see below)" : description);
                Set(ws, row, 4, n);
                subtotal += n;
                row++;
            }
            Set(ws, row, 2, SubtotalLabels[section]);
            Set(ws, row, 5, subtotal);
            grand += subtotal;
            row++;
        }
        Set(ws, row, 2, "Grand Total");
        Set(ws, row, 5, grand);

        // SSES leaves a grid for the reasons behind N9 / I10 for the state to fill in.
        row += 3;
        if (otherNoncomplete)
        {
            Set(ws, row, 3, "Give reasons and counts for other noncompletion:");
            Set(ws, row + 1, 3, "Reason");
            Set(ws, row + 1, 4, "Count");
            row += 7;
        }
        if (otherIneligible)
        {
            Set(ws, row, 3, "Give reasons and counts for other ineligibility:");
            Set(ws, row + 1, 3, "Reason");
            Set(ws, row + 1, 4, "Count");
        }
    }

    // ---------------------------------------------------------------- Table 4

    private static void WriteTable4(IXLWorksheet ws, SssReport r)
    {
        var insp = r.Inspectors;
        Set(ws, 1, 1, "SSES Table 4 (Synar Survey Inspection Results by Youth Inspector Characteristics)");
        Set(ws, 3, 8, "STATE: " + r.StateCode);
        Set(ws, 4, 8, "FFY: " + r.FederalFiscalYear);
        Set(ws, 5, 3, "Frequency Distribution");
        Set(ws, 6, 3, "Gender", "@");
        Set(ws, 6, 4, "Age", "@");
        Set(ws, 6, 5, "Number of Inspectors", "@");
        Set(ws, 6, 6, "Attempted Buys", "@");
        Set(ws, 6, 7, "Successful Buys", "@");

        var row = 7;
        var totals = new Dictionary<string, (int Attempts, int Sales)>();
        foreach (var (g, label) in new[] { ("M", "Male"), ("F", "Female") })
        {
            Set(ws, row, 3, label);
            int n = 0, att = 0, sales = 0;
            foreach (var cell in insp.Cells.Where(c => c.Gender == g).OrderBy(c => c.Age))
            {
                Set(ws, row, 4, cell.Age);
                Set(ws, row, 5, cell.InspectorCount);
                Set(ws, row, 6, cell.AttemptedBuys);
                Set(ws, row, 7, cell.SuccessfulBuys, Int);
                n += cell.InspectorCount;
                att += cell.AttemptedBuys;
                sales += cell.SuccessfulBuys;
                row++;
            }
            Set(ws, row, 4, "Subtotal");
            Set(ws, row, 5, n);
            Set(ws, row, 6, att);
            Set(ws, row, 7, sales, Int);
            totals[g] = (att, sales);
            row++;
        }

        var hasOther = insp.OtherInspectorCount > 0;
        Set(ws, row, 3, hasOther ? "Other (Explain below)" : "Other");
        Set(ws, row, 5, insp.OtherInspectorCount);
        Set(ws, row, 6, insp.OtherAttemptedBuys);
        Set(ws, row, 7, insp.OtherSuccessfulBuys);
        row++;
        Set(ws, row, 3, "Grand Total");
        Set(ws, row, 5, insp.TotalInspectorCount);
        Set(ws, row, 6, totals["M"].Attempts + totals["F"].Attempts + insp.OtherAttemptedBuys);
        Set(ws, row, 7, totals["M"].Sales + totals["F"].Sales + insp.OtherSuccessfulBuys);

        // Buy rate by age and gender (CSPInspectors.Output_RatebyAge).
        row += 2;
        Set(ws, row++, 3, "Buy Rate in Percent by Age and Gender");
        Set(ws, row, 3, "Age");
        Set(ws, row, 5, "Male");
        Set(ws, row, 6, "Female");
        Set(ws, row, 7, "Total");
        row++;
        for (var age = 14; age <= 20; age++)
        {
            var m = insp.Cells.Single(c => c.Gender == "M" && c.Age == age);
            var f = insp.Cells.Single(c => c.Gender == "F" && c.Age == age);
            Set(ws, row, 3, age);
            Set(ws, row, 5, Rate(m.SuccessfulBuys, m.AttemptedBuys), Pct1Rate);
            Set(ws, row, 6, Rate(f.SuccessfulBuys, f.AttemptedBuys), Pct1Rate);
            Set(ws, row, 7, Rate(m.SuccessfulBuys + f.SuccessfulBuys, m.AttemptedBuys + f.AttemptedBuys), Pct1Rate);
            row++;
        }
        Set(ws, row, 3, "Other");
        Set(ws, row, 7, Rate(insp.OtherSuccessfulBuys, insp.OtherAttemptedBuys), Pct1Rate);
        row++;
        // The total excludes "Other", as it does in SSES.
        Set(ws, row, 3, "Total");
        Set(ws, row, 5, Rate(totals["M"].Sales, totals["M"].Attempts), Pct1Rate);
        Set(ws, row, 6, Rate(totals["F"].Sales, totals["F"].Attempts), Pct1Rate);
        Set(ws, row, 7, Rate(totals["M"].Sales + totals["F"].Sales,
                             totals["M"].Attempts + totals["F"].Attempts), Pct1Rate);

        // SSES writes this note over the rate table's own "Total" label, which
        // hides it; here it goes one row clear of the table instead.
        if (hasOther) Set(ws, row + 2, 3, "*Explain values in Other category.");
    }

    private static double Rate(int sales, int attempts) =>
        attempts > 0 ? (double)sales / attempts : 0;

    // ---------------------------------------------------------------- Table 5

    private static void WriteTable5(IXLWorksheet ws, SssReport r)
    {
        var headers = new[]
        {
            "SynarFullID", "Stratum", "PopS", "Vstratum", "PopV", "Code", "Viol", "Type", "IA#",
            "Gender", "Age", "VMsize", "Product", "Outlet", "Asked",
        };
        for (var i = 0; i < headers.Length; i++) Set(ws, 1, i + 1, headers[i]);

        var row = 2;
        foreach (var m in r.Microdata)
        {
            Set(ws, row, 1, m.SynarFullId);
            Set(ws, row, 2, Id(m.SamplingStratum));
            Set(ws, row, 3, m.SamplingStratumPopulation);
            Set(ws, row, 4, Id(m.VarianceStratum));
            Set(ws, row, 5, m.VarianceStratumPopulation);
            Set(ws, row, 6, m.DispositionCode);
            if (m.Violation == true) Set(ws, row, 7, 1);
            if (!string.IsNullOrEmpty(m.OutletType)) Set(ws, row, 8, m.OutletType);
            if (!string.IsNullOrEmpty(m.InspectorId)) Set(ws, row, 9, m.InspectorId);
            if (!string.IsNullOrEmpty(m.InspectorGender)) Set(ws, row, 10, m.InspectorGender);
            if (m.InspectorAge is int age) Set(ws, row, 11, age);
            if (m.VmFrameSize is int vm) Set(ws, row, 12, vm);
            if (m.ProductType is int product) Set(ws, row, 13, product);
            if (m.RetailOutletType is int outlet) Set(ws, row, 14, outlet);
            if (!string.IsNullOrEmpty(m.AskedForId)) Set(ws, row, 15, m.AskedForId);
            row++;
        }
    }

    // ------------------------------------------------------------ Tables 6-8

    // SAMHSA prints every category whether or not the state has data for it,
    // in this order, so a state with no ENDS checks still shows an ENDS row
    // of zeros and row positions stay stable from year to year.
    private static readonly string[] ProductCategories =
    {
        "Cigarettes", "Small cigars/Cigarillos", "Smokeless tobacco", "ENDS",
        "Other", "Missing", "Invalid",
    };

    private static readonly string[] RetailOutletCategories =
    {
        "Gas Station", "Tobacco Store", "Restaurant", "Hotel", "Grocery Store",
        "Drug Store", "Other", "Missing", "Invalid",
    };

    private static readonly string[] AskedForIdCategories =
    {
        "Yes", "No", "Missing", "Invalid",
    };

    private static void WriteCrossTab(IXLWorksheet ws, SssReport r, CrossTab tab,
                                      string title, string pivotTitle, string[] categories)
    {
        Set(ws, 1, 1, title);
        Set(ws, 1, 6, title);
        Set(ws, 3, 4, "STATE: " + r.StateCode);
        Set(ws, 4, 4, "FFY: " + r.FederalFiscalYear);
        Set(ws, 3, 14, "STATE: " + r.StateCode);
        Set(ws, 4, 14, "FFY: " + r.FederalFiscalYear);

        // Left side: attempts and buys per category.
        Set(ws, 7, 1, "Frequency Distribution and Buy Rate");
        Set(ws, 8, 1, tab.CategoryLabel);
        Set(ws, 8, 2, "Attempted Buys");
        Set(ws, 8, 3, "Successful Buys");
        Set(ws, 8, 4, "Violation Rate (%)");

        var byCategory = tab.Rows.ToDictionary(x => x.Category);
        var row = 9;
        int totalAttempts = 0, totalSales = 0;
        foreach (var category in categories)
        {
            byCategory.TryGetValue(category, out var x);
            var att = x?.AttemptedBuys ?? 0;
            var sales = x?.SuccessfulBuys ?? 0;
            Set(ws, row, 1, category);
            Set(ws, row, 2, att);
            Set(ws, row, 3, sales);
            Set(ws, row, 4, Rate(sales, att), Pct1Short);
            totalAttempts += att;
            totalSales += sales;
            row++;
        }
        // A category outside SAMHSA's list would otherwise vanish from the
        // workbook and drop its buys out of the Grand Total.
        foreach (var x in tab.Rows.Where(x => !categories.Contains(x.Category)))
        {
            Set(ws, row, 1, x.Category + " (unmapped)");
            Set(ws, row, 2, x.AttemptedBuys);
            Set(ws, row, 3, x.SuccessfulBuys);
            Set(ws, row, 4, x.ViolationRate, Pct1Short);
            totalAttempts += x.AttemptedBuys;
            totalSales += x.SuccessfulBuys;
            row++;
        }
        Set(ws, row, 1, "Grand Total");
        Set(ws, row, 2, totalAttempts);
        Set(ws, row, 3, totalSales);
        Set(ws, row, 4, Rate(totalSales, totalAttempts), Pct1Short);

        // Right side: buy rate by category, age and gender, in three blocks.
        Set(ws, 6, 6, pivotTitle);
        var stride = categories.Length + 6;
        var blocks = new[]
        {
            (Label: "Male", Total: "Total Male", Genders: new[] { "M" }),
            (Label: "Female", Total: "Total Female", Genders: new[] { "F" }),
            (Label: "All", Total: "Grand Total", Genders: new[] { "M", "F" }),
        };
        for (var b = 0; b < blocks.Length; b++)
        {
            var (label, totalLabel, genders) = blocks[b];
            var top = 7 + b * stride;
            Set(ws, top, 6, label);
            Set(ws, top + 1, 6, tab.CategoryLabel);
            Set(ws, top + 1, 7, "Age");
            Set(ws, top + 1, 14, "Total");
            for (var a = 14; a <= 20; a++) Set(ws, top + 2, a - 7, a);

            var line = top + 3;
            foreach (var category in categories.Append<string?>(null))
            {
                Set(ws, line, 6, category ?? totalLabel);
                for (var a = 14; a <= 20; a++)
                    Set(ws, line, a - 7, PivotRate(tab, category, genders, a), Pct1Short);
                Set(ws, line, 14, PivotRate(tab, category, genders, null), Pct1Short);
                line++;
            }
        }
    }

    // CSPProducts.Output_BuyRate: rates on the right-hand pivot are stored
    // rounded to three places (VBA Round, which rounds half to even).
    private static double PivotRate(CrossTab tab, string? category, string[] genders, int? age)
    {
        var total = new BuyCount(0, 0);
        foreach (var g in genders)
        {
            if (tab.Pivot.TryGetValue(new PivotKey(category, g, age), out var c))
                total = total.Add(c);
        }
        return total.Attempts > 0
            ? Math.Round((double)total.Sales / total.Attempts, 3, MidpointRounding.ToEven)
            : 0;
    }
}
