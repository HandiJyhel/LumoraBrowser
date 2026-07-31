namespace Lumora.WinUI;

internal static class WheelScrollMath
{
    private const double DefaultScrollStep = 56d;

    public static bool TryComputeNextVerticalOffset(
        double previousOffset,
        double scrollableHeight,
        int wheelDelta,
        out double newOffset)
    {
        newOffset = previousOffset;

        if (wheelDelta == 0 || scrollableHeight <= 0)
        {
            return false;
        }

        var scrollStep = DefaultScrollStep * Math.Max(1d, Math.Abs(wheelDelta) / 120d);
        var candidateOffset = Math.Clamp(previousOffset - (Math.Sign(wheelDelta) * scrollStep), 0, scrollableHeight);
        if (Math.Abs(candidateOffset - previousOffset) < 0.5)
        {
            return false;
        }

        newOffset = candidateOffset;
        return true;
    }
}
