namespace SynarSSES.Core.Models;

// Computed output, structured by SAMHSA output table. Each property corresponds
// to a tab in the final xlsx workbook. The SssWorkbookWriter turns this into
// the byte-equivalent xlsx that gets submitted to SAMHSA.
public sealed class SssReport
{
    public required string StateCode { get; init; }
    public required int FederalFiscalYear { get; init; }
    public required DateTime GeneratedAt { get; init; }

    public required IReadOnlyList<SamplingStratumResult> StratumResults { get; init; } // Table 2
    public required SampleTally Tally { get; init; }                                   // Table 3
    public required InspectorDemographics Inspectors { get; init; }                    // Table 4
    public required IReadOnlyList<MicrodataRow> Microdata { get; init; }               // Table 5
    public required CrossTab ProductCrossTab { get; init; }                            // Table 6
    public required CrossTab OutletCrossTab { get; init; }                             // Table 7
    public required CrossTab AskedForIdCrossTab { get; init; }                         // Table 8

    // Overall ("All Outlets") statistics that drive Table 1 (the cover/summary)
    // and the "Total" row of Table 2.
    public required OverallStats Overall { get; init; }
}

public sealed class OverallStats
{
    public required int FrameSize { get; init; }              // sum of stratum populations
    public required double EstimatedPopulationSize { get; init; }  // N_HAT
    public required int SampleSize { get; init; }              // outlets drawn
    public required int EligibleSampleSize { get; init; }      // eligible (EC + ineligible-respondents)
    public required int InspectedCount { get; init; }          // EC count
    public required int ViolationCount { get; init; }          // sum of yI on EC rows
    public required double WeightedRvr { get; init; }          // R
    public required double UnweightedRvr { get; init; }
    public required double StandardError { get; init; }
    public required double WeightedAccuracyRate { get; init; }
    public required double UnweightedAccuracyRate { get; init; }
    public required double CompletionRate { get; init; }       // NRESP/ND
    public required double CiLower95 { get; init; }            // R - 1.96*SE clamped >=0
    public required double CiUpper95 { get; init; }            // R + 1.96*SE clamped <=1
    public required double CiUpperOneSided95 { get; init; }    // R + 1.645*SE
    public required bool SamhsaPrecisionMet { get; init; }     // 1.645*SE <= 0.03
    public required double DesignEffect1 { get; init; }
    public required double DesignEffect2 { get; init; }
    public required double DesignEffect3 { get; init; }
}

public sealed class SamplingStratumResult
{
    public required string SamplingStratumId { get; init; }
    public required string VarianceStratumId { get; init; }
    public required int OutletFrameSize { get; init; }
    public required double EstimatedPopulationSize { get; init; }
    public required int OutletSampleSize { get; init; }
    public required int EligibleOutletsInSample { get; init; }
    public required int InspectedCount { get; init; }
    public required int ViolationCount { get; init; }
    public required double ViolationRate { get; init; }
    public required double? StandardError { get; init; }  // only meaningful on "Total" row
}

public sealed class SampleTally
{
    // Disposition-code rollup that becomes Table 3. The SSES manual fixes
    // the order: EC, then N1-N9, with subtotals for Eligible-Completes,
    // Eligible-Noncompletes, Ineligible, and a grand total.
    public required IReadOnlyDictionary<string, int> CountsByCode { get; init; }
}

public sealed class InspectorDemographics
{
    // Per-cell counts for Table 4: rows are gender x age (14-20), columns
    // are inspector count, attempted buys, successful buys.
    public required IReadOnlyList<InspectorAgeCell> Cells { get; init; }
}

public sealed class InspectorAgeCell
{
    public required string Gender { get; init; }      // "M" or "F"
    public required int Age { get; init; }            // 14..20
    public required int InspectorCount { get; init; }
    public required int AttemptedBuys { get; init; }
    public required int SuccessfulBuys { get; init; }
}

// Generic shape used by Tables 6 (product), 7 (outlet), 8 (asked-for-id).
// Each cross-tab pairs a category (e.g. "Cigarette", "OTC", "Yes/No/Missing")
// with violation rates broken down by inspector age x gender.
public sealed class CrossTab
{
    public required string CategoryLabel { get; init; }
    public required IReadOnlyList<CrossTabRow> Rows { get; init; }
}

public sealed class CrossTabRow
{
    public required string Category { get; init; }
    public required int AttemptedBuys { get; init; }
    public required int SuccessfulBuys { get; init; }
    public required double ViolationRate { get; init; }
    // Optional age-by-gender breakdown for the right-hand pivot in tables 6/7/8.
    public IReadOnlyDictionary<(string Gender, int Age), double>? RateByGenderAge { get; init; }
}
