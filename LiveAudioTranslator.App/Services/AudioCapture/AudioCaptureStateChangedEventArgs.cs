namespace LiveAudioTranslator.App.Services.AudioCapture;

public sealed class AudioCaptureStateChangedEventArgs : EventArgs
{
    public AudioCaptureStateChangedEventArgs(
        AudioCaptureState state,
        string? deviceName = null,
        string? formatDescription = null)
    {
        State = state;
        DeviceName = deviceName;
        FormatDescription = formatDescription;
    }

    public AudioCaptureState State { get; }

    public string? DeviceName { get; }

    public string? FormatDescription { get; }
}
