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

// One row of the drawn frame sample, ready for inspector handoff. CheckType
// is set by CheckTypeAssigner after the draw -- null on raw repository reads.
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
    public string? CheckType { get; set; }  // Cigarette | Smokeless | Electronic | Menthol
}

public sealed class CheckTypeAssignmentSummary
{
    public required int CigaretteCount { get; init; }
    public required int SmokelessCount { get; init; }
    public required int ElectronicCount { get; init; }
    public required int MentholCount { get; init; }
    public required int CigaretteTarget { get; init; }
    public required int SmokelessTarget { get; init; }
    public required int ElectronicTarget { get; init; }
    public required int MentholTarget { get; init; }
    // True if the sample has fewer Vape+Tobacco-Shop+Gas-Station outlets than
    // the Electronic target requires -- the assigner fills as many as it can
    // and surfaces the warning so the user can choose to redraw with a larger
    // size or accept the under-coverage.
    public string? ElectronicWarning { get; init; }
}
