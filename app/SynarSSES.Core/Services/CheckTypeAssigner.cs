using SynarSSES.Core.Models;

namespace SynarSSES.Core.Services;

// Assigns each drawn outlet a CheckType (Cigarette / Smokeless / Electronic /
// Menthol) using the rules KY actually used in 2025:
//
//   1. Every Vape outlet      -> Electronic   (deterministic; 31/31 in 2025)
//   2. Every Tobacco Shop     -> Electronic   (deterministic; 37/38 in 2025)
//   3. If the Electronic quota is still unmet, fill from Gas Station --
//      empirically the only other type that ever gets Electronic checks.
//   4. For everything else (Dollar / Convenience / Grocery / Gas / Pharmacy /
//      Liquor / etc.) distribute Cigarette / Smokeless / Menthol by the
//      2025 within-type weights derived from synarcheck:
//        Pharmacy, Liquor Store  -> Cigarette dominant
//        Grocery Store           -> Cigarette = Smokeless
//        Other non-Electronic    -> Cigarette slightly > Smokeless > Menthol
public sealed class CheckTypeAssigner
{
    // 2025 within-business-type weights, normalized to sum to 1 after removing
    // Electronic. Source: synarcheck join map_location for checkyear=2025.
    private static readonly Dictionary<string, (double Cig, double Smk, double Men)> Weights = new()
    {
        ["Gas Station"]       = (0.468, 0.429, 0.103),   // 59 / 54 / 13 (after 7 Electronic)
        ["Dollar Store"]      = (0.472, 0.417, 0.111),   // 34 / 30 / 8
        ["Convenience Store"] = (0.462, 0.385, 0.154),   // 18 / 15 / 6
        ["Grocery Store"]     = (0.389, 0.389, 0.222),   // 14 / 14 / 8
        ["Pharmacy"]          = (0.643, 0.214, 0.143),   // 9 / 3 / 2
        ["Liquor Store"]      = (0.750, 0.250, 0.000),   // 3 / 1 / 0
        ["Department Store"]  = (0.200, 0.600, 0.200),   // 1 / 3 / 1
    };

    // Fallback for unrecognized / blank business types -- the same proportions
    // 2025 used overall after Electronic is removed.
    private static readonly (double Cig, double Smk, double Men) DefaultWeights = (0.467, 0.407, 0.127);

    public CheckTypeAssignmentSummary Assign(
        IReadOnlyList<SampledOutlet> drawn,
        double cigaretteTargetPercent,
        double smokelessTargetPercent,
        double electronicTargetPercent,
        double mentholTargetPercent,
        Random? rng = null)
    {
        rng ??= Random.Shared;
        var n = drawn.Count;

        // Largest-remainder method so the four targets sum exactly to n.
        var targets = AllocateLargestRemainder(n,
            new[] { cigaretteTargetPercent, smokelessTargetPercent,
                    electronicTargetPercent, mentholTargetPercent });
        var (cigTarget, smkTarget, elecTarget, menTarget) =
            (targets[0], targets[1], targets[2], targets[3]);

        // --- Electronic ---
        var vape    = drawn.Where(o => Eq(o.BusinessType, "Vape")).ToList();
        var tobacco = drawn.Where(o => Eq(o.BusinessType, "Tobacco Shop")).ToList();
        var gas     = drawn.Where(o => Eq(o.BusinessType, "Gas Station")).ToList();

        foreach (var o in vape)    o.CheckType = "Electronic";
        foreach (var o in tobacco) o.CheckType = "Electronic";

        var electronicAssigned = vape.Count + tobacco.Count;
        string? warning = null;
        if (electronicAssigned < elecTarget)
        {
            var need = elecTarget - electronicAssigned;
            if (need <= gas.Count)
            {
                foreach (var o in gas.OrderBy(_ => rng.Next()).Take(need))
                    o.CheckType = "Electronic";
                electronicAssigned = elecTarget;
            }
            else
            {
                foreach (var o in gas) o.CheckType = "Electronic";
                electronicAssigned = vape.Count + tobacco.Count + gas.Count;
                warning = $"Electronic target of {elecTarget} could not be met. "
                        + $"Sample has only {electronicAssigned} Vape + Tobacco Shop + Gas Station outlets. "
                        + "Consider redrawing with a larger sample or lowering the Electronic %.";
            }
        }

        // --- Cigarette / Smokeless / Menthol on the remainder ---
        // Stratified by business_type: within each type, allocate
        // (cig, smk, men) counts in that type's 2025 ratio so the type's
        // internal mix matches reality (Dollar Stores get a cig/smk/men
        // split, not 100% one category). Then rebalance globally to hit the
        // user's overall target percentages exactly.
        var remaining = drawn.Where(o => o.CheckType is null).ToList();

        foreach (var group in remaining.GroupBy(o => o.BusinessType ?? ""))
        {
            var members = group.ToList();
            var w = WeightOf(members[0]);
            var local = AllocateLargestRemainder(members.Count,
                new[] { w.Cig * 100, w.Smk * 100, w.Men * 100 });
            // Stable ordering within the type: by synar_maps_id so re-running
            // with the same draw produces the same per-type split.
            members.Sort((a, b) => a.SynarMapsId.CompareTo(b.SynarMapsId));
            var idx = 0;
            foreach (var o in members.Take(local[0]))                          o.CheckType = "Cigarette";
            idx += local[0];
            foreach (var o in members.Skip(idx).Take(local[1]))                o.CheckType = "Smokeless";
            idx += local[1];
            foreach (var o in members.Skip(idx).Take(local[2]))                o.CheckType = "Menthol";
        }

        // Global rebalance: stratified per-type allocation often lands close
        // to the requested totals, but not exact (rounding compounds). Swap
        // one outlet at a time from an over-quota category to an under-quota
        // category, picking the candidate whose business-type weight loss is
        // smallest (i.e., the outlet that "cares least" about the swap).
        var counts = new Dictionary<string, int>
        {
            ["Cigarette"] = drawn.Count(o => o.CheckType == "Cigarette"),
            ["Smokeless"] = drawn.Count(o => o.CheckType == "Smokeless"),
            ["Menthol"]   = drawn.Count(o => o.CheckType == "Menthol"),
        };
        var targetsByCat = new Dictionary<string, int>
        {
            ["Cigarette"] = cigTarget,
            ["Smokeless"] = smkTarget,
            ["Menthol"]   = menTarget,
        };
        for (var safety = 0; safety < remaining.Count && !TargetsMet(counts, targetsByCat); safety++)
        {
            var over  = counts.First(kv => kv.Value > targetsByCat[kv.Key]).Key;
            var under = counts.First(kv => kv.Value < targetsByCat[kv.Key]).Key;
            // Find the outlet in `over` whose weight gap to `under` is
            // smallest -- i.e., the one whose business type least prefers
            // `over` over `under`.
            SampledOutlet? best = null;
            var bestGap = double.MaxValue;
            foreach (var o in drawn.Where(o => o.CheckType == over))
            {
                var w = WeightOf(o);
                var gap = WeightFor(w, over) - WeightFor(w, under);
                if (gap < bestGap) { bestGap = gap; best = o; }
            }
            if (best is null) break;
            best.CheckType = under;
            counts[over]--; counts[under]++;
        }

        return new CheckTypeAssignmentSummary
        {
            CigaretteCount  = drawn.Count(o => o.CheckType == "Cigarette"),
            SmokelessCount  = drawn.Count(o => o.CheckType == "Smokeless"),
            ElectronicCount = drawn.Count(o => o.CheckType == "Electronic"),
            MentholCount    = drawn.Count(o => o.CheckType == "Menthol"),
            CigaretteTarget = cigTarget,
            SmokelessTarget = smkTarget,
            ElectronicTarget = elecTarget,
            MentholTarget = menTarget,
            ElectronicWarning = warning,
        };
    }

    private static (double Cig, double Smk, double Men) WeightOf(SampledOutlet o)
        => Weights.TryGetValue(o.BusinessType ?? "", out var w) ? w : DefaultWeights;

    private static bool TargetsMet(Dictionary<string, int> counts, Dictionary<string, int> targets)
        => counts["Cigarette"] == targets["Cigarette"]
        && counts["Smokeless"] == targets["Smokeless"]
        && counts["Menthol"]   == targets["Menthol"];

    private static double WeightFor((double Cig, double Smk, double Men) w, string cat) => cat switch
    {
        "Cigarette" => w.Cig,
        "Smokeless" => w.Smk,
        "Menthol"   => w.Men,
        _ => 0,
    };

    private static bool Eq(string? a, string b)
        => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);

    // Standard largest-remainder allocation: take floors, then distribute the
    // residual one-by-one to the categories with the largest fractional parts.
    // Guarantees the four targets sum exactly to `total`.
    private static int[] AllocateLargestRemainder(int total, double[] percents)
    {
        var raw = percents.Select(p => p * total / 100.0).ToArray();
        var floors = raw.Select(x => (int)Math.Floor(x)).ToArray();
        var residual = total - floors.Sum();
        var order = Enumerable.Range(0, raw.Length)
            .OrderByDescending(i => raw[i] - floors[i])
            .ToArray();
        for (var k = 0; k < residual; k++) floors[order[k]]++;
        return floors;
    }
}
