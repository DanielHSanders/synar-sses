namespace SynarSSES.Core.Models;

// Computed output, structured by SAMHSA output table. Each property corresponds
// to a tab in the final xlsx workbook; SssWorkbookWriter lays them out.
public sealed class SssReport
{
    public required string StateCode { get; init; }
    public required int FederalFiscalYear { get; init; }
    public required DateTime GeneratedAt { get; init; }
    public required string DataSource { get; init; }
    public required string AnalysisOption { get; init; }
    public int? EffectiveSampleSize { get; init; }
    public int? TargetSampleSize { get; init; }

    public required OverallStats Overall { get; init; }                   // Table 1
    public required IReadOnlyList<Table2Section> Table2 { get; init; }    // Table 2
    // Some outlets were typed UNK: SSES then folds them into All Outlets and
    // adds a note, because All Outlets no longer equals OTC + VM.
    public required bool HasUnknownOutletType { get; init; }
    public required SampleTally Tally { get; init; }                      // Table 3
    public required InspectorDemographics Inspectors { get; init; }       // Table 4
    public required IReadOnlyList<MicrodataRow> Microdata { get; init; }  // Table 5
    public required CrossTab ProductCrossTab { get; init; }               // Table 6
    public required CrossTab OutletCrossTab { get; init; }                // Table 7
    public required CrossTab AskedForIdCrossTab { get; init; }            // Table 8
}

public sealed class OverallStats
{
    public required int FrameSize { get; init; }                   // sum of stratum populations
    public required double EstimatedPopulationSize { get; init; }  // N_HAT, unrounded
    public required int SampleSize { get; init; }                  // NSMP: outlets drawn
    public required int EligibleSampleSize { get; init; }          // ND: EC + noncomplete
    public required int InspectedCount { get; init; }              // NRESP: EC count
    public required int ViolationCount { get; init; }              // sum of yI on EC rows
    public required double WeightedRvr { get; init; }              // R
    public required double UnweightedRvr { get; init; }
    public required double StandardError { get; init; }
    public required double WeightedAccuracyRate { get; init; }
    public required double UnweightedAccuracyRate { get; init; }
    public required double CompletionRate { get; init; }           // NRESP/ND
    public required double CiLower95 { get; init; }                // R - 1.96*SE clamped >=0
    public required double CiUpper95 { get; init; }                // R + 1.96*SE clamped <=1
    public required double CiUpperOneSided95 { get; init; }        // R + 1.645*SE
    public required bool SamhsaPrecisionMet { get; init; }         // 1.645*SE <= 0.03
    public required double DesignEffect1 { get; init; }
    public required double DesignEffect2 { get; init; }
    public required double DesignEffect3 { get; init; }
    public required double OverallSamplingRate { get; init; }      // F = M / NN
}

// One block of Table 2: All Outlets, Over the Counter Outlets, or Vending
// Machines. Estimated population sizes are already rounded the way SSES rounds
// them, so the rows add up to the total.
public sealed class Table2Section
{
    public required string Label { get; init; }
    public required IReadOnlyList<Table2Row> Strata { get; init; }
    public required Table2Row Total { get; init; }
    public required double StandardError { get; init; }
}

public sealed class Table2Row
{
    public required string SamplingStratumId { get; init; }
    public required string VarianceStratumId { get; init; }
    public required int OutletFrameSize { get; init; }
    public required long EstimatedPopulationSize { get; init; }
    public required int OutletSampleSize { get; init; }
    public required int EligibleOutletsInSample { get; init; }
    public required int InspectedCount { get; init; }
    public required int ViolationCount { get; init; }
    public required double ViolationRate { get; init; }
}

public sealed class SampleTally
{
    // Disposition-code rollup that becomes Table 3. The SSES manual fixes
    // the order: EC, then N1-N9, then I1-I10, with subtotals and a grand total.
    public required IReadOnlyDictionary<string, int> CountsByCode { get; init; }
}

public sealed class InspectorDemographics
{
    // Table 4 counts inspectors, not inspections: each youth inspector is
    // placed once, by their gender and age, with their attempted and
    // successful buys. Only inspectors with at least one completed inspection
    // are counted.
    public required IReadOnlyList<InspectorAgeCell> Cells { get; init; }
    // Inspectors whose age falls outside 14-20 or whose gender is missing.
    public required int OtherInspectorCount { get; init; }
    public required int OtherAttemptedBuys { get; init; }
    public required int OtherSuccessfulBuys { get; init; }
    public required int TotalInspectorCount { get; init; }
}

public sealed class InspectorAgeCell
{
    public required string Gender { get; init; }      // "M" or "F"
    public required int Age { get; init; }            // 14..20
    public required int InspectorCount { get; init; }
    public required int AttemptedBuys { get; init; }
    public required int SuccessfulBuys { get; init; }
}

// Tables 6 (product), 7 (retail outlet), 8 (clerk asked for ID). The left side
// is attempts and buys per category; the right side breaks the buy rate down
// by inspector gender and age.
public sealed class CrossTab
{
    public required string CategoryLabel { get; init; }
    public required IReadOnlyList<CrossTabRow> Rows { get; init; }
    // Completed inspections by category, gender and age. A null category
    // means every category; a null age means every age, including ages
    // outside 14-20. Gender is always "M" or "F" -- inspectors with any other
    // gender are left out of the pivot, as SSES does.
    public required IReadOnlyDictionary<PivotKey, BuyCount> Pivot { get; init; }
}

public sealed class CrossTabRow
{
    public required string Category { get; init; }
    public required int AttemptedBuys { get; init; }
    public required int SuccessfulBuys { get; init; }
    public required double ViolationRate { get; init; }
}

public readonly record struct PivotKey(string? Category, string Gender, int? Age);

public readonly record struct BuyCount(int Attempts, int Sales)
{
    public BuyCount Add(BuyCount other) => new(Attempts + other.Attempts, Sales + other.Sales);
}
