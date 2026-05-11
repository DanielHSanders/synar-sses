namespace SynarSSES.Core.Models;

// Inputs to the SSES sample-size calculator. Mirrors what the legacy
// "Sample Size Calculator" module takes (see vba_source/SSCalculator.cls).
// For a single-stratum SRS design (KY's case), strata reduces to one entry
// with strRvr from the prior year and strCost=1.
public sealed class SampleSizeInput
{
    public required int FrameSize { get; init; }            // total Valid outlets
    public required double ExpectedRvrPercent { get; init; } // e.g. 10.06 for 10.06%
    public required double DesignEffect { get; init; }       // e.g. 1.0 for SRS
    public required double AccuracyRatePercent { get; init; } // e.g. 92.55
    public required double CompletionRatePercent { get; init; } // e.g. 100.0
    public required double SafetyMarginPercent { get; init; } // 0..50
    public required bool UseOneSidedCi { get; init; }        // true: 1.645, false: 1.96
    // Precision target in absolute units: e.g. 0.03 means 3%. SAMHSA default 0.03.
    public double PrecisionTarget { get; init; } = 0.03;
}
