namespace SynarSSES.Core.Models;

// A recorded draw. The frame size is the important part: it is the population
// the sample was actually taken from, which the SSES report needs and which
// cannot be recovered by counting map_location later on.
public sealed class SampleRecord
{
    public int SampleId { get; init; }
    public int CheckYear { get; init; }
    public string StateCode { get; init; } = "KY";
    public DateTime DrawnAt { get; init; }
    public int FrameSize { get; init; }
    public string? FrameFilter { get; init; }
    public int? EffectiveSampleSize { get; init; }
    public int? TargetSampleSize { get; init; }
    public int? OriginalSampleSize { get; init; }
    public bool IsFinal { get; init; }
    public string? Note { get; init; }
}
