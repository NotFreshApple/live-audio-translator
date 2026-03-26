using System.IO;
using LiveAudioTranslator.App.Services.AudioCapture;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Whisper.net;
using Whisper.net.Ggml;

namespace LiveAudioTranslator.App.Services.SpeechRecognition;

public sealed class LocalWhisperSpeechRecognitionService : ISpeechRecognitionService
{
    private const float SignalThresholdPercent = 3f;
    private const int RecognitionSampleRate = 16000;
    private const int MaxThreads = 8;
    private static readonly TimeSpan SilenceTimeout = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan MinimumSpeechLength = TimeSpan.FromMilliseconds(550);
    private static readonly TimeSpan MaximumSpeechLength = TimeSpan.FromMilliseconds(5500);

    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _modelSetupLock = new(1, 1);
    private readonly MemoryStream _audioBuffer = new();
    private string _lastRecognizedText = string.Empty;
    private WaveFormat? _sourceWaveFormat;
    private WhisperFactory? _whisperFactory;
    private WhisperProcessor? _whisperProcessor;
    private string? _processorLanguageCode;
    private DateTime? _lastSignalAt;
    private bool _isStarted;
    private bool _isProcessing;
    private bool _isModelReady;
    private SpeechRecognitionState _state = SpeechRecognitionState.Idle;
    private string? _selectedLanguageCode = "ko-KR";

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

            _isStarted = true;
            _audioBuffer.SetLength(0);
            _sourceWaveFormat = null;
            _lastSignalAt = null;
            PublishState(SpeechRecognitionState.Processing, "로컬 Whisper 모델을 준비하는 중입니다.");
        }

        _ = EnsureModelReadyAsync();
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
            PublishState(SpeechRecognitionState.Idle, "로컬 음성 인식을 중지했습니다.");
        }
    }

    public void SetRecognitionLanguage(string languageCode)
    {
        lock (_syncRoot)
        {
            _selectedLanguageCode = languageCode;
            _processorLanguageCode = null;
            _audioBuffer.SetLength(0);
            _lastSignalAt = null;
            _lastRecognizedText = string.Empty;
            PublishState(_isModelReady ? SpeechRecognitionState.Ready : SpeechRecognitionState.Processing, BuildReadyMessage());
        }
    }

    public void ProcessAudioChunk(object? sender, AudioChunkAvailableEventArgs e)
    {
        byte[]? audioBytesToRecognize = null;
        WaveFormat? sourceFormat = null;
        string? selectedLanguageCode = null;

        lock (_syncRoot)
        {
            if (!_isStarted || !_isModelReady || _whisperFactory is null)
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
                    PublishState(SpeechRecognitionState.Listening, $"{ToLanguageName(_selectedLanguageCode)} 음성을 감지했습니다.");
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
                selectedLanguageCode = _selectedLanguageCode;
                _audioBuffer.SetLength(0);
                _lastSignalAt = null;
                _isProcessing = true;
                PublishState(SpeechRecognitionState.Processing, "Whisper로 음성을 텍스트로 변환하는 중입니다.");
            }
        }

        if (audioBytesToRecognize is not null && sourceFormat is not null)
        {
            _ = RecognizeBufferedAudioAsync(audioBytesToRecognize, sourceFormat, selectedLanguageCode);
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _isStarted = false;
            _isProcessing = false;
            _isModelReady = false;
            DisposeProcessor(_whisperProcessor);
            _whisperProcessor = null;
            _processorLanguageCode = null;
            _whisperFactory?.Dispose();
            _whisperFactory = null;
            _audioBuffer.Dispose();
        }

        _modelSetupLock.Dispose();
    }

    private async Task EnsureModelReadyAsync()
    {
        await _modelSetupLock.WaitAsync();

        try
        {
            lock (_syncRoot)
            {
                if (!_isStarted)
                {
                    return;
                }

                if (_isModelReady && _whisperFactory is not null)
                {
                    PublishState(SpeechRecognitionState.Ready, BuildReadyMessage());
                    return;
                }
            }

            var modelPath = GetModelPath();
            var modelDirectory = Path.GetDirectoryName(modelPath)!;
            Directory.CreateDirectory(modelDirectory);

            if (!File.Exists(modelPath))
            {
                var legacyModelPath = Path.Combine(AppContext.BaseDirectory, "models", "ggml-base.bin");
                if (File.Exists(legacyModelPath))
                {
                    File.Copy(legacyModelPath, modelPath, overwrite: true);
                }
                else
                {
                    PublishState(SpeechRecognitionState.Processing, "Whisper 기본 모델을 내려받는 중입니다.");
                    await using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.Base);
                    await using var fileStream = File.Create(modelPath);
                    await modelStream.CopyToAsync(fileStream);
                }
            }

            var factory = WhisperFactory.FromPath(modelPath);

            lock (_syncRoot)
            {
                DisposeProcessor(_whisperProcessor);
                _whisperProcessor = null;
                _processorLanguageCode = null;
                _whisperFactory?.Dispose();
                _whisperFactory = factory;
                _isModelReady = true;
                PublishState(SpeechRecognitionState.Ready, BuildReadyMessage());
            }
        }
        catch (Exception exception)
        {
            lock (_syncRoot)
            {
                _isModelReady = false;
                PublishState(SpeechRecognitionState.Faulted, $"로컬 Whisper 준비 실패: {exception.Message}");
            }
        }
        finally
        {
            _modelSetupLock.Release();
        }
    }

    private async Task RecognizeBufferedAudioAsync(byte[] rawAudioBytes, WaveFormat sourceFormat, string? selectedLanguageCode)
    {
        try
        {
            var samples = ConvertToRecognitionSamples(rawAudioBytes, sourceFormat);
            var recognized = await RecognizeAsync(samples, selectedLanguageCode);

            lock (_syncRoot)
            {
                _isProcessing = false;

                if (recognized is null)
                {
                    PublishState(SpeechRecognitionState.Ready, BuildReadyMessage());
                    return;
                }

                if (string.Equals(_lastRecognizedText, recognized.Text, StringComparison.Ordinal))
                {
                    PublishState(SpeechRecognitionState.Ready, "같은 인식 결과는 다시 표시하지 않습니다.");
                    return;
                }

                _lastRecognizedText = recognized.Text;
                PublishState(SpeechRecognitionState.Ready, $"{ToLanguageName(recognized.LanguageCode)} 인식 완료");
                SpeechRecognized?.Invoke(this, recognized);
            }
        }
        catch (Exception exception)
        {
            lock (_syncRoot)
            {
                _isProcessing = false;
                PublishState(SpeechRecognitionState.Faulted, $"로컬 Whisper 인식 오류: {exception.Message}");
            }
        }
    }

    private async Task<RecognizedSpeechEventArgs?> RecognizeAsync(float[] samples, string? selectedLanguageCode)
    {
        WhisperProcessor? processor;

        lock (_syncRoot)
        {
            processor = GetOrCreateProcessor_NoLock(selectedLanguageCode);
        }

        if (processor is null || samples.Length == 0)
        {
            return null;
        }

        var textParts = new List<string>();
        var probabilityTotal = 0f;
        var segmentCount = 0;
        var detectedLanguage = selectedLanguageCode ?? "ko-KR";

        await foreach (var segment in processor.ProcessAsync(samples))
        {
            if (string.IsNullOrWhiteSpace(segment.Text))
            {
                continue;
            }

            textParts.Add(segment.Text.Trim());
            probabilityTotal += segment.Probability;
            segmentCount++;

            if (!string.IsNullOrWhiteSpace(segment.Language))
            {
                detectedLanguage = MapFromWhisperLanguageCode(segment.Language);
            }
        }

        var combinedText = string.Join(" ", textParts).Trim();
        if (string.IsNullOrWhiteSpace(combinedText))
        {
            return null;
        }

        var confidence = segmentCount == 0 ? 0f : probabilityTotal / segmentCount;
        return new RecognizedSpeechEventArgs(combinedText, detectedLanguage, confidence);
    }

    private WhisperProcessor? GetOrCreateProcessor_NoLock(string? selectedLanguageCode)
    {
        if (_whisperFactory is null)
        {
            return null;
        }

        var whisperLanguageCode = MapToWhisperLanguageCode(selectedLanguageCode);
        if (_whisperProcessor is not null && string.Equals(_processorLanguageCode, whisperLanguageCode, StringComparison.Ordinal))
        {
            return _whisperProcessor;
        }

        DisposeProcessor(_whisperProcessor);

        var threadCount = Math.Clamp(Environment.ProcessorCount, 2, MaxThreads);
        var builder = _whisperFactory.CreateBuilder()
            .WithThreads(threadCount)
            .WithMaxSegmentLength(180);

        if (string.Equals(whisperLanguageCode, "auto", StringComparison.Ordinal))
        {
            builder.WithLanguageDetection();
        }
        else
        {
            builder.WithLanguage(whisperLanguageCode);
        }

        _whisperProcessor = builder.Build();
        _processorLanguageCode = whisperLanguageCode;
        return _whisperProcessor;
    }

    private static float[] ConvertToRecognitionSamples(byte[] rawAudioBytes, WaveFormat sourceFormat)
    {
        using var sourceStream = new RawSourceWaveStream(new MemoryStream(rawAudioBytes, writable: false), sourceFormat);
        ISampleProvider sampleProvider = sourceStream.ToSampleProvider();

        if (sampleProvider.WaveFormat.Channels > 1)
        {
            sampleProvider = new StereoToMonoSampleProvider(sampleProvider);
        }

        if (sampleProvider.WaveFormat.SampleRate != RecognitionSampleRate)
        {
            sampleProvider = new WdlResamplingSampleProvider(sampleProvider, RecognitionSampleRate);
        }

        var estimatedSamples = Math.Max(
            RecognitionSampleRate / 2,
            (int)Math.Ceiling(GetBufferedDuration(rawAudioBytes.Length, sourceFormat).TotalSeconds * RecognitionSampleRate));

        var samples = new float[estimatedSamples];
        var readBuffer = new float[4096];
        var totalSamples = 0;

        while (true)
        {
            var samplesRead = sampleProvider.Read(readBuffer, 0, readBuffer.Length);
            if (samplesRead <= 0)
            {
                break;
            }

            if (totalSamples + samplesRead > samples.Length)
            {
                Array.Resize(ref samples, Math.Max(samples.Length * 2, totalSamples + samplesRead));
            }

            Array.Copy(readBuffer, 0, samples, totalSamples, samplesRead);
            totalSamples += samplesRead;
        }

        if (totalSamples != samples.Length)
        {
            Array.Resize(ref samples, totalSamples);
        }

        return samples;
    }

    private static void DisposeProcessor(WhisperProcessor? processor)
    {
        if (processor is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private string BuildReadyMessage()
    {
        return _isModelReady
            ? $"로컬 Whisper 준비 완료 | 인식 언어: {ToLanguageName(_selectedLanguageCode)}"
            : "로컬 Whisper 모델을 준비하는 중입니다.";
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

    private static string GetModelPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataPath, "LiveAudioTranslator", "models", "ggml-base.bin");
    }

    private static string MapToWhisperLanguageCode(string? languageCode)
    {
        return languageCode switch
        {
            "en-US" => "en",
            "ja-JP" => "ja",
            "zh-CN" => "zh",
            "ko-KR" => "ko",
            _ => "auto"
        };
    }

    private static string MapFromWhisperLanguageCode(string languageCode)
    {
        return languageCode switch
        {
            "en" => "en-US",
            "ja" => "ja-JP",
            "zh" => "zh-CN",
            "ko" => "ko-KR",
            _ => languageCode
        };
    }

    private static string ToLanguageName(string? languageCode)
    {
        return languageCode switch
        {
            "en-US" => "영어",
            "ja-JP" => "일본어",
            "zh-CN" => "중국어",
            "ko-KR" => "한국어",
            _ => "자동 감지"
        };
    }
}
