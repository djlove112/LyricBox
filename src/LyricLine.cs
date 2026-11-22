namespace LyricsBridge;

internal sealed record LyricLine(
    double TimeSeconds,
    string Text,
    string Translation)
{
    public long? DurationMilliseconds { get; init; }
    public IReadOnlyList<LyricWord>? Words { get; init; }
    public bool IsTimingGroup { get; init; }

    public bool SweepEnabled => !IsTimingGroup;
}

internal sealed record LyricWord(long TimeMs, long DurationMs, string Text);
