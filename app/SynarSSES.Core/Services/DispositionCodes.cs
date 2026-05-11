namespace SynarSSES.Core.Services;

// Mapping from SAMHSA disposition codes to the internal r-code used by the
// SSES algorithm. Source: CSPSSES.cls Init_DCodeList (vba_source line 2415).
//   rcode=1 = eligible & inspection complete   (EC)
//   rcode=2 = eligible but inspection not complete  (N1..N9)
//   rcode=3 = ineligible                       (I1..I10)
internal static class DispositionCodes
{
    public const int Complete = 1;       // d=1, Resp=1
    public const int Noncomplete = 2;    // d=1, Resp=0
    public const int Ineligible = 3;     // d=0, Resp=0

    public static int ToRCode(string code) => code?.Trim().ToUpperInvariant() switch
    {
        "EC"                                              => Complete,
        "N1" or "N2" or "N3" or "N4" or "N5" or
        "N6" or "N7" or "N8" or "N9"                      => Noncomplete,
        "I1" or "I2" or "I3" or "I4" or "I5" or
        "I6" or "I7" or "I8" or "I9" or "I10"             => Ineligible,
        _ => throw new ArgumentOutOfRangeException(
                nameof(code), code,
                "Unknown disposition code. Valid: EC, N1-N9, I1-I10.")
    };
}
