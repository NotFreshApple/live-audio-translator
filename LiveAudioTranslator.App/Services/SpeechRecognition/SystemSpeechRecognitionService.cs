using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.Speech.Recognition;
using LiveAudioTranslator.App.Services.AudioCapture;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LiveAudioTranslator.App.Services.SpeechRecognition;

public sealed class SystemSpeechRecognitionService : ISpeechRecognitionService
{
    private const float SignalThresholdPercent = 3f;
    private static readonly TimeSpan SilenceTimeout = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan MinimumSpeechLength = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan MaximumSpeechLength = TimeSpan.FromSeconds(8);

    private readonly object _syncRoot = new();
    private readonly Dictionary<string, SpeechRecognitionEngine> _engines = [];
    private readonly MemoryStream _audioBuffer = new();
    private WaveFormat? _sourceWaveFormat;
    private DateTime? _lastSignalAt;
    private bool _isStarted;
    private bool _isProcessing;
    private string _lastRecognizedText = string.Empty;
    private SpeechRecognitionState _state = SpeechRecognitionState.Idle;
    private string? _selectedLanguageCode = "ja-JP";

    public event EventHandler<RecognizedSpeechEventArgs>? SpeechRecognized;

    public event EventHandler<SpeechRecognitionStateChangedEventArgs>? StateChanged;

    public SpeechRecognitionState State => _state;

    public string? SelectedLanguageCode => _selectedLanguageCode;

    public void Start()
    {
        lock (_syncRoot)
        {
            if (_isStarted)
            {
                return;
            }

            InitializeRecognizers();
            _isStarted = true;

            if (_engines.Count == 0)
            {
                PublishState(SpeechRecognitionState.NoRecognizer, "일본어 또는 한국어 음성 인식기를 찾지 못했습니다.");
                return;
            }

            PublishState(SpeechRecognitionState.Ready, BuildRecognizerMessage());
        }
    }

    public void Stop()
    {
        lock (_syncRoot)
        {
            _isStarted = false;
            _isProcessing = false;
            _sourceWaveFormat = null;
            _lastSignalAt = null;
            _audioBuffer.SetLength(0);
            PublishState(SpeechRecognitionState.Idle, "음성 인식을 중지했습니다.");
        }
    }

    public void SetRecognitionLanguage(string languageCode)
    {
        lock (_syncRoot)
        {
            _selectedLanguageCode = languageCode;
            _audioBuffer.SetLength(0);
            _lastSignalAt = null;
            _lastRecognizedText = string.Empty;
            PublishState(SpeechRecognitionState.Ready, $"{ToLanguageName(languageCode)} 인식 대기 중");
        }
    }

    public void ProcessAudioChunk(object? sender, AudioChunkAvailableEventArgs e)
    {
        byte[]? audioBytesToRecognize = null;
        WaveFormat? sourceFormat = null;

        lock (_syncRoot)
        {
            if (!_isStarted || _engines.Count == 0)
            {
                return;
            }

            _sourceWaveFormat ??= e.WaveFormat;
            _audioBuffer.Write(e.Buffer, 0, e.BytesRecorded);

            if (e.PeakPercent >= SignalThresholdPercent)
            {
                _lastSignalAt = DateTime.Now;
                if (_state != SpeechRecognitionState.Listening)
                {
                    PublishState(SpeechRecognitionState.Listening, "음성을 감지했습니다. 일본어와 한국어로 인식 중입니다.");
                }
            }

            var bufferedDuration = GetBufferedDuration(_audioBuffer.Length, _sourceWaveFormat);
            var shouldFlushBySilence =
                _lastSignalAt is not null &&
                DateTime.Now - _lastSignalAt.Value >= SilenceTimeout &&
                bufferedDuration >= MinimumSpeechLength;
            var shouldFlushByLength = bufferedDuration >= MaximumSpeechLength;

            if (!_isProcessing && (shouldFlushBySilence || shouldFlushByLength))
            {
                audioBytesToRecognize = _audioBuffer.ToArray();
                sourceFormat = _sourceWaveFormat;
                _audioBuffer.SetLength(0);
                _lastSignalAt = null;
                _isProcessing = true;
                PublishState(SpeechRecognitionState.Processing, "음성을 텍스트로 변환하는 중입니다.");
            }
        }

        if (audioBytesToRecognize is not null && sourceFormat is not null)
        {
            _ = Task.Run(() => RecognizeBufferedAudio(audioBytesToRecognize, sourceFormat));
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            foreach (var engine in _engines.Values)
            {
                engine.Dispose();
            }

            _engines.Clear();
            _audioBuffer.Dispose();
        }
    }

    private void InitializeRecognizers()
    {
        if (_engines.Count > 0)
        {
            return;
        }

        foreach (var languageCode in new[] { "ja-JP", "ko-KR" })
        {
            var recognizerInfo = SpeechRecognitionEngine
                .InstalledRecognizers()
                .FirstOrDefault(info => string.Equals(info.Culture.Name, languageCode, StringComparison.OrdinalIgnoreCase));

            if (recognizerInfo is null)
            {
                continue;
            }

            var engine = new SpeechRecognitionEngine(new CultureInfo(languageCode));
            engine.LoadGrammar(new DictationGrammar());
            _engines[languageCode] = engine;
        }
    }

    private void RecognizeBufferedAudio(byte[] rawAudioBytes, WaveFormat sourceFormat)
    {
        try
        {
            var wavBytes = ConvertToRecognitionWave(rawAudioBytes, sourceFormat);
            var bestResult = RecognizeBestMatch(wavBytes);

            lock (_syncRoot)
            {
                _isProcessing = false;

                if (bestResult is null)
                {
                    PublishState(SpeechRecognitionState.Ready, BuildRecognizerMessage());
                    return;
                }

                if (string.Equals(_lastRecognizedText, bestResult.Text, StringComparison.Ordinal))
                {
                    PublishState(SpeechRecognitionState.Ready, "같은 인식 결과는 다시 표시하지 않습니다.");
                    return;
                }

                _lastRecognizedText = bestResult.Text;
                PublishState(SpeechRecognitionState.Ready, $"{ToLanguageName(bestResult.LanguageCode)} 인식 완료");
                SpeechRecognized?.Invoke(this, bestResult);
            }
        }
        catch (Exception exception)
        {
            lock (_syncRoot)
            {
                _isProcessing = false;
                PublishState(SpeechRecognitionState.Faulted, $"음성 인식 오류: {exception.Message}");
            }
        }
    }

    private RecognizedSpeechEventArgs? RecognizeBestMatch(byte[] wavBytes)
    {
        RecognizedSpeechEventArgs? bestResult = null;

        var engineEntries = _engines.Where(entry =>
            string.IsNullOrWhiteSpace(_selectedLanguageCode) ||
            string.Equals(entry.Key, _selectedLanguageCode, StringComparison.OrdinalIgnoreCase));

        foreach (var engineEntry in engineEntries)
        {
            using var stream = new MemoryStream(wavBytes, writable: false);
            engineEntry.Value.SetInputToWaveStream(stream);

            var result = engineEntry.Value.Recognize();
            if (result is null || string.IsNullOrWhiteSpace(result.Text))
            {
                continue;
            }

            var recognized = new RecognizedSpeechEventArgs(result.Text.Trim(), engineEntry.Key, result.Confidence);
            if (bestResult is null || recognized.Confidence > bestResult.Confidence)
            {
                bestResult = recognized;
            }
        }

        return bestResult;
    }

    private static byte[] ConvertToRecognitionWave(byte[] rawAudioBytes, WaveFormat sourceFormat)
    {
        using var sourceStream = new RawSourceWaveStream(new MemoryStream(rawAudioBytes, writable: false), sourceFormat);
        ISampleProvider sampleProvider = sourceStream.ToSampleProvider();

        if (sampleProvider.WaveFormat.Channels > 1)
        {
            sampleProvider = new StereoToMonoSampleProvider(sampleProvider);
        }

        if (sampleProvider.WaveFormat.SampleRate != 16000)
        {
            sampleProvider = new WdlResamplingSampleProvider(sampleProvider, 16000);
        }

        var waveProvider = new SampleToWaveProvider16(sampleProvider);
        using var outputStream = new MemoryStream();
        WaveFileWriter.WriteWavFileToStream(outputStream, waveProvider);
        return outputStream.ToArray();
    }

    private static TimeSpan GetBufferedDuration(long byteLength, WaveFormat? waveFormat)
    {
        if (waveFormat is null || waveFormat.AverageBytesPerSecond <= 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds((double)byteLength / waveFormat.AverageBytesPerSecond);
    }

    private void PublishState(SpeechRecognitionState state, string message)
    {
        _state = state;
        StateChanged?.Invoke(this, new SpeechRecognitionStateChangedEventArgs(state, message));
    }

    private string BuildRecognizerMessage()
    {
        var languages = _engines.Keys.Select(ToLanguageName).ToArray();
        return languages.Length == 0
            ? "음성 인식기 없음"
            : $"사용 가능 언어: {string.Join(", ", languages)}";
    }

    private static string ToLanguageName(string languageCode)
    {
        return languageCode switch
        {
            "ja-JP" => "일본어",
            "ko-KR" => "한국어",
            _ => languageCode
        };
    }
}
