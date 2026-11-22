using System.Diagnostics;

namespace EpsilonCore.Engine;

/// <summary>
/// The host's clock, in seconds since the program started.
/// All host side scheduling uses this clock.
/// </summary>
internal static class HostClock
{
    private static readonly Stopwatch stopwatch = Stopwatch.StartNew();

    /// <summary>
    /// Seconds since the program started.
    /// </summary>
    public static double NowSeconds => stopwatch.Elapsed.TotalSeconds;
}

/// <summary>
/// Estimates the relationship between the host's clock and one board's clock from
/// <see cref="Commands.CommandCode.CLOCK_SYNC_REQUEST"/> / <see cref="Commands.CommandCode.CLOCK_SYNC_RESPONSE"/> exchanges,
/// so the host can tell the board to start a command at an exact board clock tick.
///
/// Each exchange gives a sample: the board's tick count and the host time halfway between sending the request and
/// receiving the response. The board's frequency is fitted with least squares over recent samples (falling back to the
/// board's nominal frequency), and the offset comes from the sample with the smallest round trip time because it has
/// the least uncertainty about when the board read its clock.
/// </summary>
internal class ClockSync
{
    private record Sample(double HostSeconds, ulong BoardTicks, double RoundTripSeconds);

    private const int MAX_SAMPLES = 16;

    /// <summary>
    /// Fitted frequencies further than this fraction from nominal are ignored as bad data.
    /// </summary>
    private const double MAX_FREQUENCY_ERROR = 0.01;

    /// <summary>
    /// Samples must span at least this long before the frequency is fitted.
    /// </summary>
    private const double MIN_FIT_SPAN_SECONDS = 2;

    private readonly List<Sample> samples = [];
    private Sample? reference;

    /// <summary>
    /// The board's clock frequency from its <see cref="Boards.Board.ClockFrequency"/>.
    /// </summary>
    public double NominalFrequencyHz { get; set; }

    /// <summary>
    /// Best estimate of the board's clock frequency.
    /// </summary>
    public double FrequencyHz { get; private set; }

    /// <summary>
    /// True once at least one sample has been received, so host times can be converted to board ticks.
    /// </summary>
    public bool IsSynced => reference is not null && FrequencyHz > 0;

    /// <summary>
    /// Host time the last clock sync request was sent, or null if no request is waiting for a response.
    /// </summary>
    public double? RequestSentAt { get; set; }

    /// <summary>
    /// Host time the last clock sync request was sent, whether or not it was answered.
    /// </summary>
    public double LastRequestAt { get; set; } = double.NegativeInfinity;

    /// <summary>
    /// Number of samples received.
    /// </summary>
    public int SampleCount => samples.Count;

    /// <summary>
    /// Adds a sample from a clock sync exchange.
    /// </summary>
    /// <param name="requestSentAt">Host time the request was sent.</param>
    /// <param name="responseReceivedAt">Host time the response was received.</param>
    /// <param name="boardTicks">Board clock ticks reported in the response.</param>
    public void AddSample(double requestSentAt, double responseReceivedAt, ulong boardTicks)
    {
        double roundTrip = responseReceivedAt - requestSentAt;
        if (roundTrip < 0)
            return;

        samples.Add(new Sample((requestSentAt + responseReceivedAt) / 2, boardTicks, roundTrip));
        if (samples.Count > MAX_SAMPLES)
            samples.RemoveAt(0);

        FrequencyHz = FitFrequency();
        reference = samples.MinBy(sample => sample.RoundTripSeconds);
    }

    /// <summary>
    /// Fits the board's clock frequency to the samples with least squares,
    /// falling back to <see cref="NominalFrequencyHz"/> when there isn't enough data or the fit is implausible.
    /// </summary>
    /// <returns>The best estimate of the board's clock frequency in hertz.</returns>
    private double FitFrequency()
    {
        if (samples.Count < 3 || samples[^1].HostSeconds - samples[0].HostSeconds < MIN_FIT_SPAN_SECONDS)
            return NominalFrequencyHz;

        // Least squares slope of board ticks against host seconds, relative to the first sample to keep precision.
        double hostOrigin = samples[0].HostSeconds;
        double tickOrigin = samples[0].BoardTicks;
        double meanX = samples.Average(sample => sample.HostSeconds - hostOrigin);
        double meanY = samples.Average(sample => sample.BoardTicks - tickOrigin);
        double numerator = 0;
        double denominator = 0;
        foreach (Sample sample in samples)
        {
            double dx = sample.HostSeconds - hostOrigin - meanX;
            numerator += dx * (sample.BoardTicks - tickOrigin - meanY);
            denominator += dx * dx;
        }

        if (denominator <= 0)
            return NominalFrequencyHz;

        double fitted = numerator / denominator;
        if (NominalFrequencyHz > 0 && Math.Abs(fitted - NominalFrequencyHz) > NominalFrequencyHz * MAX_FREQUENCY_ERROR)
            return NominalFrequencyHz;

        return fitted;
    }

    /// <summary>
    /// Converts a host time to the board's clock ticks. Only valid when <see cref="IsSynced"/>.
    /// </summary>
    /// <param name="hostSeconds">Host time, in seconds.</param>
    /// <returns>The board clock tick at that host time.</returns>
    public ulong HostToBoardTicks(double hostSeconds)
    {
        Debug.Assert(reference is not null);
        double ticks = reference.BoardTicks + ((hostSeconds - reference.HostSeconds) * FrequencyHz);
        return ticks <= 0 ? 0 : (ulong)ticks;
    }

    /// <summary>
    /// Converts board clock ticks to a host time. Only valid when <see cref="IsSynced"/>.
    /// </summary>
    /// <param name="boardTicks">Board clock ticks.</param>
    /// <returns>The host time, in seconds, at that board clock tick.</returns>
    public double BoardTicksToHost(ulong boardTicks)
    {
        Debug.Assert(reference is not null);
        return reference.HostSeconds + (((double)boardTicks - reference.BoardTicks) / FrequencyHz);
    }

    /// <summary>
    /// Converts a duration to board ticks using the best frequency estimate.
    /// </summary>
    /// <param name="seconds">Duration in seconds.</param>
    /// <returns>The duration in board clock ticks.</returns>
    public ulong SecondsToTicks(double seconds) =>
        seconds <= 0 || FrequencyHz <= 0 ? 0 : (ulong)(seconds * FrequencyHz);
}
