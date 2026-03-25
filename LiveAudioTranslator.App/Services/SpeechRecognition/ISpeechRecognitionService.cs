using LiveAudioTranslator.App.Services.AudioCapture;

namespace LiveAudioTranslator.App.Services.SpeechRecognition;

public interface ISpeechRecognitionService : IDisposable
{
    event EventHandler<RecognizedSpeechEventArgs>? SpeechRecognized;

    event EventHandler<SpeechRecognitionStateChangedEventArgs>? StateChanged;

    SpeechRecognitionState State { get; }

    string? SelectedLanguageCode { get; }

    void Start();

    void Stop();

    void SetRecognitionLanguage(string languageCode);

    void ProcessAudioChunk(object? sender, AudioChunkAvailableEventArgs e);
}
