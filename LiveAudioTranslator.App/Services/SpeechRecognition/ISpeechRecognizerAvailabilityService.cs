namespace LiveAudioTranslator.App.Services.SpeechRecognition;

public interface ISpeechRecognizerAvailabilityService
{
    bool IsRecognizerAvailable(string languageCode);
}
