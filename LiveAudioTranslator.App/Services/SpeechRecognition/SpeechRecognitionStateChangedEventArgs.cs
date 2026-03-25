namespace LiveAudioTranslator.App.Services.SpeechRecognition;

public sealed class SpeechRecognitionStateChangedEventArgs : EventArgs
{
    public SpeechRecognitionStateChangedEventArgs(SpeechRecognitionState state, string message)
    {
        State = state;
        Message = message;
    }

    public SpeechRecognitionState State { get; }

    public string Message { get; }
}
