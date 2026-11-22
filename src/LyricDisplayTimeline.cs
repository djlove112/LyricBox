namespace LyricsBridge;

// Timing events (including end markers) stay in the original timeline. Only
// visible text owns a row, a measured height, and a scroll target.
internal sealed class LyricDisplayTimeline
{
    private readonly IReadOnlyList<LyricLine> source;
    private readonly bool translationVisible;
    private readonly string introTitle;
    private readonly int firstLyricIndex;
    internal IReadOnlyList<LyricLine> Rows { get; }
    internal IReadOnlyList<int> SourceIndices { get; }
    internal bool HasIntro => introTitle.Length > 0;
    internal bool IsIntroRow(int index) => HasIntro && index == 0;

    internal LyricDisplayTimeline(IReadOnlyList<LyricLine> source, bool translationVisible, string introTitle = "")
    {
        this.source = source;
        this.translationVisible = translationVisible;
        var rows = new List<LyricLine>();
        var indices = new List<int>();
        firstLyricIndex = -1;
        for (var i = 0; i < source.Count; i++)
            if (!string.IsNullOrWhiteSpace(source[i].Text)) { firstLyricIndex = i; break; }
        this.introTitle = firstLyricIndex >= 0 && !string.IsNullOrWhiteSpace(introTitle) ? introTitle : "";
        if (HasIntro)
        {
            rows.Add(new LyricLine(Math.Min(0, source[firstLyricIndex].TimeSeconds), this.introTitle, ""));
            indices.Add(-1); // Display-only identity; never index the source with it.
        }
        for (var i = 0; i < source.Count; i++)
        {
            var line = source[i];
            if (string.IsNullOrWhiteSpace(line.Text) &&
                (!translationVisible || string.IsNullOrWhiteSpace(line.Translation))) continue;
            rows.Add(line);
            indices.Add(i);
        }
        Rows = rows.ToArray();
        SourceIndices = indices.ToArray();
    }

    internal bool Matches(IReadOnlyList<LyricLine> lines, bool translations, string title = "") =>
        ReferenceEquals(source, lines) && translationVisible == translations && introTitle == title;

    internal (int ScrollIndex, int ActiveIndex) Resolve(int timelineIndex)
    {
        if (Rows.Count == 0) return (-1, -1);
        if (HasIntro && timelineIndex < firstLyricIndex) return (0, 0);
        var low = 0;
        var high = SourceIndices.Count - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            if (SourceIndices[middle] <= timelineIndex) low = middle + 1;
            else high = middle - 1;
        }
        // Keep the surrounding context during a gap, without treating the
        // previous sentence as still active or moving onto an invisible row.
        var scroll = Math.Max(0, high);
        var active = high >= 0 && SourceIndices[high] == timelineIndex ? high : -1;
        return (scroll, active);
    }
}
