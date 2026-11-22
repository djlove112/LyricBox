namespace LyricsBridge;

// A bounded history of signed travel, not rendered frames. Keeping the old
// trajectory during a direction change lets a trail turn through a point.
internal sealed class StarMotionHistory
{
    private readonly (double Time, double Phase)[] samples = new (double, double)[512];
    private int start, count;
    internal static double Exposure(double length) => Math.Clamp(length / 900, .025, .4);
    internal void Clear() { start = count = 0; }
    internal void Record(double time, double phase)
    {
        if (count > 0 && time <= samples[(start + count - 1) % samples.Length].Time)
        {
            samples[(start + count - 1) % samples.Length] = (time, phase);
            return;
        }
        if (count == samples.Length) { start = (start + 1) % samples.Length; count--; }
        samples[(start + count++) % samples.Length] = (time, phase);
        while (count > 2 && samples[(start + 1) % samples.Length].Time < time - .6)
        { start = (start + 1) % samples.Length; count--; }
    }
    internal double Previous(double now, double length, double fallback)
    {
        if (count == 0) return fallback;
        var time = now - Exposure(length);
        var a = samples[start];
        if (time <= a.Time) return a.Phase;
        for (int i = 1; i < count; i++)
        {
            var b = samples[(start + i) % samples.Length];
            if (time <= b.Time)
                return a.Phase + (b.Phase - a.Phase) * ((time - a.Time) / (b.Time - a.Time));
            a = b;
        }
        return a.Phase;
    }
}
