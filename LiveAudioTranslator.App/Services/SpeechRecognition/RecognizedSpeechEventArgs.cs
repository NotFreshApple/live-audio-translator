namespace LiveAudioTranslator.App.Services.SpeechRecognition;

public sealed class RecognizedSpeechEventArgs : EventArgs
{
    public RecognizedSpeechEventArgs(string text, string languageCode, float confidence)
    {
        Text = text;
        LanguageCode = languageCode;
        Confidence = confidence;
    }

    public string Text { get; }

    public string LanguageCode { get; }

    public float Confidence { get; }
}
