namespace LiveAudioTranslator.App.Services.SpeechRecognition;

public enum SpeechRecognitionState
{
    Idle,
    Ready,
    Listening,
    Processing,
    NoRecognizer,
    Faulted
}
