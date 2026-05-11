using SynarSSES.Core.Models;

namespace SynarSSES.Core.Services;

// Picks `size` outlets uniformly at random without replacement from the
// supplied frame. Uses Random.Shared by default; tests can inject a seeded
// Random to make draws reproducible.
public sealed class SampleDrawer
{
    public IReadOnlyList<SampledOutlet> Draw(
        IReadOnlyList<SampledOutlet> frame, int size, Random? rng = null)
    {
        if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
        if (size > frame.Count)
            throw new ArgumentException(
                $"Requested sample size ({size}) exceeds frame size ({frame.Count}).",
                nameof(size));

        rng ??= Random.Shared;

        // Fisher-Yates partial shuffle: shuffle the first `size` positions, then
        // return them. O(size) work even when frame is huge.
        var arr = frame.ToArray();
        for (var i = 0; i < size; i++)
        {
            var j = rng.Next(i, arr.Length);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return arr.Take(size).ToList();
    }
}
