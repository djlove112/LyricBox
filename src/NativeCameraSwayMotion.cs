namespace LyricsBridge;

internal readonly record struct CameraSwaySample(double X, double Y, double Rotation, double Scale = 1);

/// <summary>
/// Native equivalent of the 0.21.0.172 camera-sway trajectory. Wallpaper and
/// foreground outputs sample the same deterministic curve from one monotonic
/// scene clock.
/// </summary>
internal static class NativeCameraSwayMotion
{
    private const double PositionNormalizer = 12.125342;
    private const double ReferenceStrength = 24;

    private static readonly double[] XCoefficients =
    [
        0.894023, 1.616445, 3.249072, -0.272442, -1.12725,
        0.712737, 1.180242, 0.056209, -1.112797, -0.440364,
    ];

    private static readonly double[] YCoefficients =
    [
        2.257405, -2.769669, -3.894988, 0.3275, 2.163105,
        2.32905, -1.164724, -2.792327, 0.806357, -0.895738,
    ];

    private static readonly double[] RotationCoefficients =
    [
        -0.140076, 0.16498, 0.218375, 0.062715, -0.125892,
        -0.025081, 0.038331, 0.201389, -0.113266, 0.131833,
    ];

    internal static CameraSwaySample Sample(double phase, double strength = ReferenceStrength)
    {
        var normalizedPhase = ((phase % 1) + 1) % 1;
        var safeStrength = double.IsFinite(strength) ? strength : ReferenceStrength;
        var strengthRatio = safeStrength / ReferenceStrength;
        return new CameraSwaySample(
            SampleHarmonics(XCoefficients, normalizedPhase) / PositionNormalizer * safeStrength,
            SampleHarmonics(YCoefficients, normalizedPhase) / PositionNormalizer * safeStrength,
            SampleHarmonics(RotationCoefficients, normalizedPhase) * strengthRatio);
    }

    internal static IReadOnlyList<CameraSwaySample> CreateSamples(
        double strength = ReferenceStrength,
        int sampleCount = 140)
    {
        var count = Math.Max(40, sampleCount);
        var samples = new CameraSwaySample[count + 1];
        for (var index = 0; index <= count; index++)
        {
            samples[index] = Sample(index / (double)count, strength);
        }
        return samples;
    }

    private static double SampleHarmonics(IReadOnlyList<double> coefficients, double phase)
    {
        var value = 0D;
        for (var harmonic = 1; harmonic <= 5; harmonic++)
        {
            var angle = Math.PI * 2 * harmonic * phase;
            value += coefficients[(harmonic - 1) * 2] * Math.Cos(angle);
            value += coefficients[(harmonic - 1) * 2 + 1] * Math.Sin(angle);
        }
        return value;
    }
}
