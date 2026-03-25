namespace LiveAudioTranslator.App.Services.AudioCapture;

public interface IAudioCaptureService : IDisposable
{
    event EventHandler<AudioChunkAvailableEventArgs>? AudioChunkAvailable;

    event EventHandler<AudioCaptureStateChangedEventArgs>? StateChanged;

    event EventHandler<AudioLevelChangedEventArgs>? LevelChanged;

    event EventHandler<AudioCaptureStatsChangedEventArgs>? StatsChanged;

    AudioCaptureState State { get; }

    void Start();

    void Stop();
}
