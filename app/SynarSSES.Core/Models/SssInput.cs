namespace SynarSSES.Core.Models;

// All inputs the calculator needs for a single SSES run. The shape mirrors
// what the SAMHSA SSES program takes as input (the Table 5 microdata sheet
// plus the cover-sheet metadata).
public sealed class SssInput
{
    public required string StateCode { get; init; }          // "KY"
    public required int FederalFiscalYear { get; init; }     // e.g. 2026
    public required IReadOnlyList<MicrodataRow> Microdata { get; init; }
}
