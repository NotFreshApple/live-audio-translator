namespace LiveAudioTranslator.App.Services.AudioCapture;

public sealed class AudioLevelChangedEventArgs : EventArgs
{
    public AudioLevelChangedEventArgs(float peakPercent)
    {
        PeakPercent = Math.Clamp(peakPercent, 0f, 100f);
    }

    public float PeakPercent { get; }
}
