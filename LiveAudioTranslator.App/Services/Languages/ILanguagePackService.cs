namespace LiveAudioTranslator.App.Services.Languages;

public interface ILanguagePackService
{
    Task<bool> IsLanguageInstalledAsync(string cultureCode, CancellationToken cancellationToken = default);

    Task<bool> InstallLanguageAsync(string cultureCode, CancellationToken cancellationToken = default);
}
