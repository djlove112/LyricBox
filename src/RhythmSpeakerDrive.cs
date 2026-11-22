using System.Collections.Immutable;

namespace LyricsBridge;

internal readonly record struct RhythmSpeakerOnset(double Strength,double Contrast,double Confidence);
internal readonly record struct RhythmSpeakerHit(long Sequence,double Time,double Strength)
{
    // Evidence at the confirmed peak, not at a later UI/audio snapshot. Consumers
    // may select accents without changing the continuous speaker motion signal.
    public RhythmSpeakerOnset? Onset { get; init; }
}

// Constant-size publication. Held excitation survives a skipped display tick;
// readers never consume/reset a beat or own mutable audio history.
internal readonly record struct RhythmSpeakerDriveFrame(double Time, double Energy, double Background,
    double Reference, double Envelope, double Transient, double Confidence, double Strength,
    long Sequence, double HitTime)
{
    // Published arrays are immutable and replaced only when a new onset arrives.
    [System.Text.Json.Serialization.JsonIgnore]
    public ImmutableArray<RhythmSpeakerHit> Hits { get; init; } = [];
}

internal sealed class RhythmSpeakerDrive
{
    internal const int MaximumHitHistory=32;
    private double background, reference = .10, envelope, transient, filteredEnergy;
    private double previousNovelty, beforeNovelty, previousStrength, previousConfidence, previousContrast;
    private double lastHit = -1e9;
    private long sequence;
    internal RhythmSpeakerDriveFrame Frame { get; private set; }

    internal void Process(double time, double dt, double low, double body, double vocal, double snap, double rms)
    {
        static double Clean(double x) => double.IsFinite(x) ? Math.Max(0, x) : 0;
        low = Clean(low); body = Clean(body); snap = Clean(snap); rms = Clean(rms);
        var rawEnergy = low * .82 + body * .32 + Math.Min(snap, low + body) * .04;
        // A 10 ms RMS window still sees individual bass cycles. Suppress that
        // phase ripple before measuring an onset, without a long FFT delay.
        filteredEnergy += (rawEnergy - filteredEnergy) * (1 - Math.Exp(-dt / .018));
        var energy = filteredEnergy;
        // Silence cannot normalize the next small sound to a full-size hit.
        if (rms > .001) reference += (Math.Max(.035, energy) - reference) *
            (1 - Math.Exp(-dt / (energy > reference ? 2.0 : 24)));
        // A quintic silence gate has zero first/second derivatives at either
        // end, avoiding a visible corner as quiet audio crosses the threshold.
        var audibility = Math.Clamp((rms - .00035) / .00115, 0, 1);
        var gate = audibility * audibility * audibility * (audibility * (audibility * 6 - 15) + 10);
        var normalized = (1 - Math.Exp(-Math.Max(0, energy - .0003) / reference)) * gate;
        var novelty = Math.Max(0, energy - background);
        var contrast = Math.Clamp(novelty / Math.Max(.008, reference * .65), 0, 1);
        var confidence = Math.Clamp(novelty / Math.Max(.0015, background * .35), 0, 1) * gate;
        var strength = normalized * (.30 + .70 * contrast) * confidence;
        transient *= Math.Exp(-dt / .085);
        RhythmSpeakerOnset? onset = null;
        // One-frame peak confirmation. The merge interval gates only extra
        // excitation; continuous envelope never waits for a grid/primary flag.
        if (previousNovelty > beforeNovelty && previousNovelty >= novelty &&
            time - dt - lastHit >= .08 && previousConfidence > .18 && previousStrength > .008)
        {
            lastHit = time - dt; sequence++;
            transient = Math.Max(transient, previousStrength);
            onset = new(previousStrength, previousContrast, previousConfidence);
        }
        beforeNovelty = previousNovelty; previousNovelty = novelty;
        previousStrength = strength; previousConfidence = confidence; previousContrast = contrast;
        background += (energy - background) * (1 - Math.Exp(-dt / .22));
        envelope += (normalized - envelope) * (1 - Math.Exp(-dt / (normalized > envelope ? .012 : .055)));
        var drive = Math.Clamp(envelope * (.09 + .74 * contrast) + transient * .24 * (1 - envelope * .5), 0, 1);
        var hits=Frame.Hits.IsDefault ? ImmutableArray<RhythmSpeakerHit>.Empty : Frame.Hits;
        if(sequence!=Frame.Sequence)
        {
            if(hits.Length>=MaximumHitHistory)hits=hits.RemoveAt(0);
            hits=hits.Add(new(sequence,lastHit,drive) { Onset = onset });
        }
        Frame = new(time, energy, background, reference, envelope, transient, confidence, drive, sequence, lastHit) { Hits=hits };
    }
}

internal readonly record struct RhythmSpeakerPose(double Time, double Target, double Scale, double Velocity,
    int Grade, RhythmSpeakerDriveFrame Drive)
{
    public double Amplitude { get; init; } = 1;
    public int AmplitudePercent { get; init; } = 100;
    public double FrameIntervalMilliseconds { get; init; }
    // Shared response before the independent display-amplitude control.
    public double ResponseScale { get; init; }
    internal static int GradeFor(double scale) => scale < .00001 ? 0 : scale < .003 ? 1 : scale < .008 ? 2 :
        scale < .015 ? 3 : scale < .025 ? 4 : scale < .040 ? 5 : 6;
    internal static string GradeName(int grade) => grade switch
    { 1 => "微拍", 2 => "轻拍", 3 => "中拍", 4 => "较强拍", 5 => "强拍", 6 => "重拍", _ => "归位" };
}

internal sealed class RhythmSpeakerMotion
{
    private double scale, velocity;
    internal void Reset() { scale = velocity = 0; }
    internal RhythmSpeakerPose Advance(double time, double dt, bool playing, RhythmSpeakerDriveFrame drive,
        int weakResponse, int softness)
    {
        var fresh = drive.Time > 0 && time >= drive.Time - .05;
        var age = Math.Max(0, time - drive.Time);
        var strength = playing && fresh ? drive.Strength * Math.Exp(-Math.Max(0, age - .065) / .065) : 0;
        var weak = Math.Clamp(weakResponse, 0, 100) / 100d;
        // Smooth monotonic curve, zero at silence; lower-range gain does not
        // raise the maximum excursion or introduce discrete grade boundaries.
        var target = .060 * (strength * strength + (.12 + .50 * weak) * strength * (1 - strength));
        var soft = Math.Clamp(softness, 0, 100) / 100d;
        for (var remaining = Math.Clamp(dt, 0, .25); remaining > 1e-9;)
        {
            var h = Math.Min(1d / 240, remaining); remaining -= h;
            var omega = target > scale ? 72 - soft * 22 : 30 - soft * 9;
            var damping = .88 + soft * .10;
            velocity += (omega * omega * (target - scale) - 2 * damping * omega * velocity) * h;
            scale += velocity * h;
            if (scale > .060) { scale = .060; velocity = Math.Min(0, velocity); }
            if (scale < -.001) { scale = -.001; velocity = Math.Max(0, velocity); }
        }
        if (target == 0 && Math.Abs(scale) < .000001 && Math.Abs(velocity) < .00001) Reset();
        return new(time, target, scale, velocity, RhythmSpeakerPose.GradeFor(scale), drive) { ResponseScale = scale };
    }
}
