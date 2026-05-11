using SynarSSES.Core.Models;

namespace SynarSSES.Core.Services;

// Port of the SSES Sample Size Calculator (vba_source/SSCalculator.cls.Calculate).
// For a single-stratum SRS design the chain is:
//   strTerm1 = strSize * sqrt(rvr*(100-rvr)) / (100 * n * sqrt(strCost))
//   strTerm2 = strSize * sqrt(rvr*(100-rvr) * strCost) / (100 * n)
//   strTerm3 = strSize * rvr*(100-rvr) / (10000 * n)
// Summed across strata (just one for KY), then:
//   EffectiveSampleSize = sumTerm1 * sumTerm2
//                       / ((precision / 1.645)^2 + (1/n) * sumTerm3)
//   TargetSampleSize    = ceil(Effective * DesignEffect)
//   OriginalSampleSize  = ceil((1 + SafetyMargin/100) * Target
//                              / (AccuracyRate * CompletionRate / 10000))
// Each step rounds UP if not already integer (VBA: `Fix(x) + 1`).
public sealed class SampleSizeCalculator
{
    public SampleSizeResult Compute(SampleSizeInput input)
    {
        if (input.FrameSize < 1)
            throw new ArgumentException("Frame size must be positive.", nameof(input));
        if (input.AccuracyRatePercent <= 0 || input.CompletionRatePercent <= 0)
            throw new ArgumentException("Accuracy and completion rates must be positive.", nameof(input));

        double n = input.FrameSize;
        double rvr = input.ExpectedRvrPercent;
        double strCost = 1.0;  // single-stratum: cost factor cancels out

        // Single-stratum case: stratum size == frame size, so the sums reduce.
        double term1 = n * Math.Sqrt(rvr * (100 - rvr)) / (100 * n * Math.Sqrt(strCost));
        double term2 = n * Math.Sqrt(rvr * (100 - rvr) * strCost) / (100 * n);
        double term3 = n * rvr * (100 - rvr) / (10000 * n);

        double z = input.UseOneSidedCi ? 1.645 : 1.96;
        double denom = Math.Pow(input.PrecisionTarget / z, 2) + (1.0 / n) * term3;
        double effective = term1 * term2 / denom;
        int effectiveInt = CeilingInt(effective);

        double target = effectiveInt * input.DesignEffect;
        int targetInt = CeilingInt(target);

        double orss = (1 + input.SafetyMarginPercent / 100.0)
                      * targetInt
                      / (input.AccuracyRatePercent * input.CompletionRatePercent / 10000.0);
        int orssInt = CeilingInt(orss);

        // Per VBA: if computed sample size exceeds the frame, cap at frame size.
        if (orssInt > input.FrameSize) orssInt = input.FrameSize;

        return new SampleSizeResult
        {
            Input = input,
            EffectiveSampleSize = effectiveInt,
            TargetSampleSize = targetInt,
            OriginalSampleSize = orssInt,
        };
    }

    // VBA `Fix(x) + 1 if x <> Fix(x)` -- round up unless already an integer.
    private static int CeilingInt(double x)
    {
        var floor = (int)Math.Floor(x);
        return x > floor ? floor + 1 : floor;
    }
}
