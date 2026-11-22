namespace LyricsBridge;

internal sealed record LyricTiming(
    LyricLine? CurrentLine,
    double Progress,
    double? StartedAtSeconds,
    double? EndsAtSeconds);

internal static class LyricProgressCalculator
{
    public static double Calculate(IReadOnlyList<LyricLine> lines, double positionSeconds) =>
        GetTiming(lines, positionSeconds).Progress;

    public static LyricTiming GetWholeTrackTiming(
        double positionSeconds,
        double? durationSeconds)
    {
        if (durationSeconds is not > 0 ||
            !double.IsFinite(durationSeconds.Value) ||
            !double.IsFinite(positionSeconds))
        {
            return new LyricTiming(null, 0, null, null);
        }

        var duration = durationSeconds.Value;
        var position = Math.Clamp(positionSeconds, 0, duration);
        return new LyricTiming(null, position / duration, 0, duration);
    }

    public static LyricTiming GetTiming(IReadOnlyList<LyricLine> lines, double positionSeconds)
    {
        if (lines.Count == 0)
        {
            return new LyricTiming(null, 0, null, null);
        }

        var currentIndex = -1;
        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].TimeSeconds > positionSeconds)
            {
                break;
            }

            currentIndex = index;
        }

        if (currentIndex < 0)
        {
            return new LyricTiming(null, 0, null, lines[0].TimeSeconds);
        }

        var current = lines[currentIndex];
        if (currentIndex == lines.Count - 1)
        {
            return new LyricTiming(current, 1, current.TimeSeconds, null);
        }

        var nextTime = lines[currentIndex + 1].TimeSeconds;
        var duration = Math.Max(0.001, nextTime - current.TimeSeconds);
        var progress = Math.Clamp((positionSeconds - current.TimeSeconds) / duration, 0, 1);
        return new LyricTiming(current, progress, current.TimeSeconds, nextTime);
    }

    public static LyricTiming? GetTimingForText(
        IReadOnlyList<LyricLine> lines,
        string text,
        double positionSeconds)
    {
        var normalizedText = NormalizeText(text);
        if (normalizedText.Length == 0)
        {
            return null;
        }

        var matchingIndex = Enumerable.Range(0, lines.Count)
            .Where(index => TextMatches(lines[index].Text, normalizedText))
            .OrderBy(index => Math.Abs(lines[index].TimeSeconds - positionSeconds))
            .FirstOrDefault(-1);
        if (matchingIndex < 0)
        {
            return null;
        }

        var current = lines[matchingIndex];
        var nextTime = matchingIndex < lines.Count - 1
            ? lines[matchingIndex + 1].TimeSeconds
            : (double?)null;
        var progress = nextTime is null
            ? 1
            : Math.Clamp(
                (positionSeconds - current.TimeSeconds) /
                Math.Max(0.001, nextTime.Value - current.TimeSeconds),
                0,
                1);
        return new LyricTiming(current, progress, current.TimeSeconds, nextTime);
    }

    private static bool TextMatches(string lineText, string normalizedText) =>
        NormalizeText(lineText) == normalizedText ||
        lineText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(part => NormalizeText(part) == normalizedText);

    private static string NormalizeText(string value)
    {
        var normalized = string.Join(
            " ",
            (value ?? "")
                .Replace('’', '\'')
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant()
            .Replace('：', ':');
        return normalized
            .Replace(" :", ":", StringComparison.Ordinal)
            .Replace(": ", ":", StringComparison.Ordinal);
    }
}
