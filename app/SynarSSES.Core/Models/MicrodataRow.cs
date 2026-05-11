namespace SynarSSES.Core.Models;

// One inspection record -- the unit of input to the SSES calculator.
// Mirrors the columns of "Table 5" in the SAMHSA SSES xlsx output: the raw
// microdata that all the downstream tables (1, 2, 3, 4, 6, 7, 8) are derived
// from. Column names follow the SSES manual section 5 (Stratified SRS layout).
public sealed class MicrodataRow
{
    // Column A: outlet identifier within this year's sample (e.g. "SY25-728").
    public required string SynarFullId { get; init; }

    // Column B: sampling stratum id (KY uses a single stratum, id "1").
    public required string SamplingStratum { get; init; }

    // Column C: total outlets in the sampling stratum frame.
    public required int SamplingStratumPopulation { get; init; }

    // Column D: variance stratum id. When variance and sampling strata are the
    // same (the simple SRS case), this equals SamplingStratum.
    public required string VarianceStratum { get; init; }

    // Column E: total outlets in the variance stratum frame.
    public required int VarianceStratumPopulation { get; init; }

    // Column F: response disposition code. Drives the EC / Refusal /
    // Ineligible / etc. classification in Table 3 and the eligible-respondent
    // bookkeeping in the variance estimator. Valid values per the SSES manual:
    //   EC = Eligible and inspection complete
    //   N1 = In operation but closed at time of visit
    //   N2 = Unsafe to access
    //   N3 = Refusal
    //   N4 = No buying attempt
    //   N5 = Out of business
    //   N6 = Cannot locate
    //   N7 = Other ineligible
    //   N8 = Does not sell tobacco
    //   N9 = Inspector / outlet anomaly
    public required string DispositionCode { get; init; }

    // Column G: did the outlet sell tobacco to the youth inspector? 0/1.
    // Only meaningful when DispositionCode == "EC".
    public bool? Violation { get; init; }

    // Column H: outlet type. "OTC", "VM", or "UNK". Blank treated as OTC.
    public string? OutletType { get; init; }

    // Column I: youth inspector identifier (e.g. "118-26").
    public string? InspectorId { get; init; }

    // Column J: inspector gender, "M" or "F".
    public string? InspectorGender { get; init; }

    // Column K: inspector age, 14-20 inclusive.
    public int? InspectorAge { get; init; }

    // Column L: vending-machine outlet frame size in the sampling stratum.
    // Only meaningful for designs that sample VMs separately. KY: blank.
    public int? VmFrameSize { get; init; }

    // Column M: product type code (optional). 1 = Cigarettes, 3 = Smokeless,
    // others per the SSES manual.
    public int? ProductType { get; init; }

    // Column N: retail outlet sub-type (optional, separate from OTC/VM).
    public int? RetailOutletType { get; init; }

    // Column O: did the clerk ask for ID? "Y"/"N"/blank (Missing).
    public string? AskedForId { get; init; }
}
