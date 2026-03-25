using System.Speech.Recognition;

namespace LiveAudioTranslator.App.Services.SpeechRecognition;

public sealed class SystemSpeechRecognizerAvailabilityService : ISpeechRecognizerAvailabilityService
{
    public bool IsRecognizerAvailable(string languageCode)
    {
        return SpeechRecognitionEngine
            .InstalledRecognizers()
            .Any(info => string.Equals(info.Culture.Name, languageCode, StringComparison.OrdinalIgnoreCase));
    }
}
