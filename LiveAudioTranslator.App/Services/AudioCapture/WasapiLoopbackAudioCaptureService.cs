using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LiveAudioTranslator.App.Services.AudioCapture;

public sealed class WasapiLoopbackAudioCaptureService : IAudioCaptureService
{
    private readonly object _syncRoot = new();
    private const float SignalDetectionThresholdPercent = 3f;
    private MMDeviceEnumerator? _deviceEnumerator;
    private MMDevice? _device;
    private WasapiLoopbackCapture? _capture;
    private AudioCaptureState _state = AudioCaptureState.Idle;
    private long _totalBytesCaptured;
    private DateTime? _lastSignalDetectedAt;

    public event EventHandler<AudioChunkAvailableEventArgs>? AudioChunkAvailable;

    public event EventHandler<AudioCaptureStateChangedEventArgs>? StateChanged;

    public event EventHandler<AudioLevelChangedEventArgs>? LevelChanged;

    public event EventHandler<AudioCaptureStatsChangedEventArgs>? StatsChanged;

    public AudioCaptureState State => _state;

    public void Start()
    {
        lock (_syncRoot)
        {
            if (_capture is not null || _state == AudioCaptureState.Capturing)
            {
                return;
            }

            PublishState(AudioCaptureState.Starting);

            try
            {
                ResetStats();
                _deviceEnumerator = new MMDeviceEnumerator();
                _device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                _capture = new WasapiLoopbackCapture(_device);
                _capture.DataAvailable += Capture_OnDataAvailable;
                _capture.RecordingStopped += Capture_OnRecordingStopped;
                _capture.StartRecording();

                PublishState(
                    AudioCaptureState.Capturing,
                    _device.FriendlyName,
                    DescribeFormat(_capture.WaveFormat));
            }
            catch
            {
                CleanupCapture();
                PublishState(AudioCaptureState.Faulted);
                throw;
            }
        }
    }

    public void Stop()
    {
        lock (_syncRoot)
        {
            var capture = _capture;
            if (capture is null)
            {
                PublishState(AudioCaptureState.Idle);
                return;
            }

            PublishState(AudioCaptureState.Stopping, _device?.FriendlyName, DescribeFormat(capture.WaveFormat));
            capture.StopRecording();
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            CleanupCapture();
            PublishState(AudioCaptureState.Idle);
        }
    }

    private void Capture_OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var capture = _capture;
        if (capture is null)
        {
            return;
        }

        var bufferCopy = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, bufferCopy, 0, e.BytesRecorded);
        var peakPercent = CalculatePeakPercent(e.Buffer, e.BytesRecorded, capture.WaveFormat);
        UpdateStats(e.BytesRecorded, capture.WaveFormat, peakPercent);
        AudioChunkAvailable?.Invoke(this, new AudioChunkAvailableEventArgs(bufferCopy, e.BytesRecorded, capture.WaveFormat, peakPercent));
        LevelChanged?.Invoke(this, new AudioLevelChangedEventArgs(peakPercent));
    }

    private void Capture_OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        lock (_syncRoot)
        {
            CleanupCapture();
            PublishState(e.Exception is null ? AudioCaptureState.Idle : AudioCaptureState.Faulted);
        }
    }

    private void PublishState(
        AudioCaptureState state,
        string? deviceName = null,
        string? formatDescription = null)
    {
        _state = state;
        StateChanged?.Invoke(this, new AudioCaptureStateChangedEventArgs(state, deviceName, formatDescription));
    }

    private void CleanupCapture()
    {
        if (_capture is not null)
        {
            _capture.DataAvailable -= Capture_OnDataAvailable;
            _capture.RecordingStopped -= Capture_OnRecordingStopped;
            _capture.Dispose();
            _capture = null;
        }

        _device?.Dispose();
        _device = null;

        _deviceEnumerator?.Dispose();
        _deviceEnumerator = null;
    }

    private void ResetStats()
    {
        _totalBytesCaptured = 0;
        _lastSignalDetectedAt = null;
        PublishStats(TimeSpan.Zero, 0f);
    }

    private void UpdateStats(int bytesRecorded, WaveFormat waveFormat, float peakPercent)
    {
        if (bytesRecorded > 0)
        {
            _totalBytesCaptured += bytesRecorded;
        }

        if (peakPercent >= SignalDetectionThresholdPercent)
        {
            _lastSignalDetectedAt = DateTime.Now;
        }

        var duration = waveFormat.AverageBytesPerSecond > 0
            ? TimeSpan.FromSeconds((double)_totalBytesCaptured / waveFormat.AverageBytesPerSecond)
            : TimeSpan.Zero;

        PublishStats(duration, peakPercent);
    }

    private void PublishStats(TimeSpan duration, float peakPercent)
    {
        StatsChanged?.Invoke(
            this,
            new AudioCaptureStatsChangedEventArgs(
                _totalBytesCaptured,
                duration,
                _lastSignalDetectedAt,
                peakPercent));
    }

    private static string DescribeFormat(WaveFormat waveFormat)
    {
        return $"{waveFormat.SampleRate} Hz, {waveFormat.BitsPerSample}-bit, {waveFormat.Channels} ch";
    }

    private static float CalculatePeakPercent(byte[] buffer, int bytesRecorded, WaveFormat waveFormat)
    {
        if (bytesRecorded <= 0)
        {
            return 0f;
        }

        if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat && waveFormat.BitsPerSample == 32)
        {
            return CalculateFloatPeakPercent(buffer, bytesRecorded);
        }

        if (waveFormat.BitsPerSample == 16)
        {
            return CalculatePcm16PeakPercent(buffer, bytesRecorded);
        }

        return 0f;
    }

    private static float CalculateFloatPeakPercent(byte[] buffer, int bytesRecorded)
    {
        var peak = 0f;

        for (var index = 0; index <= bytesRecorded - 4; index += 4)
        {
            var sample = Math.Abs(BitConverter.ToSingle(buffer, index));
            if (sample > peak)
            {
                peak = sample;
            }
        }

        return Math.Clamp(peak * 100f, 0f, 100f);
    }

    private static float CalculatePcm16PeakPercent(byte[] buffer, int bytesRecorded)
    {
        var peak = 0f;

        for (var index = 0; index <= bytesRecorded - 2; index += 2)
        {
            var sample = Math.Abs(BitConverter.ToInt16(buffer, index) / 32768f);
            if (sample > peak)
            {
                peak = sample;
            }
        }

        return Math.Clamp(peak * 100f, 0f, 100f);
    }
}
