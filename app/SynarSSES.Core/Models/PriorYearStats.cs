namespace SynarSSES.Core.Models;

// Summary stats from a past year of synarcheck, used as defaults when
// planning the next year's sample.
public sealed record PriorYearStats(
    int CheckYear,
    int TotalInspections,
    int EligibleComplete,
    int ViolationCount,
    double ViolationRatePercent,
    double AccuracyRatePercent,
    double CompletionRatePercent);
