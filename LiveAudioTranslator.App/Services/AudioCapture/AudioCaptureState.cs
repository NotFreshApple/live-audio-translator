namespace LiveAudioTranslator.App.Services.AudioCapture;

public enum AudioCaptureState
{
    Idle,
    Starting,
    Capturing,
    Stopping,
    Faulted
}
