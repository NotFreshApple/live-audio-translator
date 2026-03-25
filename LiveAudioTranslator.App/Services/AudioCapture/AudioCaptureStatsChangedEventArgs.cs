namespace LiveAudioTranslator.App.Services.AudioCapture;

public sealed class AudioCaptureStatsChangedEventArgs : EventArgs
{
    public AudioCaptureStatsChangedEventArgs(
        long totalBytesCaptured,
        TimeSpan totalCaptureDuration,
        DateTime? lastSignalDetectedAt,
        float latestPeakPercent)
    {
        TotalBytesCaptured = totalBytesCaptured;
        TotalCaptureDuration = totalCaptureDuration;
        LastSignalDetectedAt = lastSignalDetectedAt;
        LatestPeakPercent = latestPeakPercent;
    }

    public long TotalBytesCaptured { get; }

    public TimeSpan TotalCaptureDuration { get; }

    public DateTime? LastSignalDetectedAt { get; }

    public float LatestPeakPercent { get; }
}
