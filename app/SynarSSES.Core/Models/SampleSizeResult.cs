namespace SynarSSES.Core.Models;

// Output of SampleSizeCalculator. Intermediate values are kept so the result
// xlsx can show the full math chain (Effective -> Target -> Original).
public sealed class SampleSizeResult
{
    public required SampleSizeInput Input { get; init; }
    public required int EffectiveSampleSize { get; init; } // SRS minimum to hit precision target
    public required int TargetSampleSize { get; init; }     // EffectiveSampleSize * DesignEffect
    public required int OriginalSampleSize { get; init; }   // ORSS: number of outlets to draw
}

// One row of the drawn frame sample, ready for inspector handoff.
public sealed class SampledOutlet
{
    public required int SynarMapsId { get; init; }
    public required string Name { get; init; }
    public required string Address { get; init; }
    public required string City { get; init; }
    public required string State { get; init; }
    public required string Zip { get; init; }
    public required decimal Latitude { get; init; }
    public required decimal Longitude { get; init; }
    public string? BusinessType { get; init; }
}
