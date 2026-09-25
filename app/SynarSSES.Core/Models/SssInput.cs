namespace SynarSSES.Core.Models;

// All inputs the calculator needs for a single SSES run. The shape mirrors
// what the SAMHSA SSES program takes as input (the Table 5 microdata sheet
// plus the cover-sheet metadata it prompts for).
public sealed class SssInput
{
    public required string StateCode { get; init; }          // "KY"
    public required int FederalFiscalYear { get; init; }     // e.g. 2027
    public required IReadOnlyList<MicrodataRow> Microdata { get; init; }

    // SSES asks the operator for these two at run time rather than deriving
    // them: they are the sample-size calculator's results for the year and
    // appear on Table 1 under "Sample Size for Current Year".
    public int? EffectiveSampleSize { get; init; }
    public int? TargetSampleSize { get; init; }

    // Table 1 run date. SSES prints local time; the caller supplies it so the
    // server clock (UTC) does not leak into the report.
    public DateTime? GeneratedAt { get; init; }

    // Table 1 "Data" line. SSES writes the input file name here.
    public string DataSource { get; init; } = "synarcheck";
}
