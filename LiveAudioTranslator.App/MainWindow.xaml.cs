using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LiveAudioTranslator.App.Services.AudioCapture;
using LiveAudioTranslator.App.Services.Languages;
using LiveAudioTranslator.App.Services.SpeechRecognition;

namespace LiveAudioTranslator.App;

public partial class MainWindow : Window
{
    private const string DefaultDeviceName = "Default output device";
    private const string DefaultFormatDescription = "No format";
    private const string OriginalPlaceholderText = "Recognized text will appear here.";
    private const string TranslatedPlaceholderText = "Translated text will appear here.";
    private const string PendingTranslationText = "Recognition complete. Translation pending.";
    private const int MaxDisplayedOriginalLines = 2;

    private readonly IAudioCaptureService _audioCaptureService;
    private readonly ISpeechRecognitionService _speechRecognitionService;
    private readonly DispatcherTimer _signalMonitorTimer;
    private readonly List<LanguageOption> _languageOptions;
    private readonly Queue<string> _recentOriginalLines = new();
    private bool _isCaptureEnabled;
    private DateTime? _lastSignalDetectedAt;
    private float _latestPeakPercent;
    private string _currentDeviceName = DefaultDeviceName;
    private string _currentFormatDescription = DefaultFormatDescription;

    public MainWindow()
    {
        InitializeComponent();

        _audioCaptureService = new WasapiLoopbackAudioCaptureService();
        _speechRecognitionService = new LocalWhisperSpeechRecognitionService();
        _languageOptions =
        [
            new LanguageOption { DisplayName = "English", CultureCode = "en-US" },
            new LanguageOption { DisplayName = "Japanese", CultureCode = "ja-JP" },
            new LanguageOption { DisplayName = "Chinese", CultureCode = "zh-CN" },
            new LanguageOption { DisplayName = "Korean", CultureCode = "ko-KR" }
        ];

        RecognitionLanguageComboBox.ItemsSource = _languageOptions;
        TranslationLanguageComboBox.ItemsSource = _languageOptions;
        RecognitionLanguageComboBox.SelectedItem = _languageOptions.First(option => option.CultureCode == "ko-KR");
        TranslationLanguageComboBox.SelectedItem = _languageOptions.First(option => option.CultureCode == "ko-KR");

        _audioCaptureService.AudioChunkAvailable += _speechRecognitionService.ProcessAudioChunk;
        _audioCaptureService.StateChanged += AudioCaptureService_OnStateChanged;
        _audioCaptureService.LevelChanged += AudioCaptureService_OnLevelChanged;
        _audioCaptureService.StatsChanged += AudioCaptureService_OnStatsChanged;

        _speechRecognitionService.StateChanged += SpeechRecognitionService_OnStateChanged;
        _speechRecognitionService.SpeechRecognized += SpeechRecognitionService_OnSpeechRecognized;
        _speechRecognitionService.SetRecognitionLanguage("ko-KR");

        _signalMonitorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _signalMonitorTimer.Tick += SignalMonitorTimer_OnTick;

        ClearDisplayedRecognition();
        ApplyVisualState();
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Width = Math.Max(MinWidth, workArea.Width);
        Height = Math.Max(200, workArea.Height / 5d);
        Left = workArea.Left;
        Top = workArea.Bottom - Height;
    }

    private void RecognitionLanguageComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecognitionLanguageComboBox.SelectedItem is not LanguageOption selectedLanguage)
        {
            return;
        }

        _speechRecognitionService.SetRecognitionLanguage(selectedLanguage.CultureCode);
        _recentOriginalLines.Clear();
        OriginalTextBlock.Text = OriginalPlaceholderText;
        DetailTextBlock.Text = $"Recognition language: {selectedLanguage.DisplayName}";
    }

    private void TranslationLanguageComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TranslationLanguageComboBox.SelectedItem is not LanguageOption selectedLanguage)
        {
            return;
        }

        DetailTextBlock.Text = $"Translation language: {selectedLanguage.DisplayName}";
    }

    private void CaptureToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isCaptureEnabled)
        {
            _audioCaptureService.Stop();
            _speechRecognitionService.Stop();
            return;
        }

        try
        {
            _speechRecognitionService.Start();
            _audioCaptureService.Start();
        }
        catch (Exception exception)
        {
            _isCaptureEnabled = false;
            StatusTextBlock.Text = "Capture start failed";
            DetailTextBlock.Text = exception.Message;
            ApplyVisualState();

            MessageBox.Show(
                this,
                exception.Message,
                "Audio capture error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AudioCaptureService_OnStateChanged(object? sender, AudioCaptureStateChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _isCaptureEnabled = e.State == AudioCaptureState.Capturing || e.State == AudioCaptureState.Starting;
            _currentDeviceName = string.IsNullOrWhiteSpace(e.DeviceName) ? DefaultDeviceName : e.DeviceName;
            _currentFormatDescription = string.IsNullOrWhiteSpace(e.FormatDescription) ? DefaultFormatDescription : e.FormatDescription;

            StatusTextBlock.Text = ConvertCaptureStateToDisplayText(e.State);
            DetailTextBlock.Text = BuildCaptureDetailMessage(e.State);

            if (e.State == AudioCaptureState.Capturing)
            {
                _signalMonitorTimer.Start();
            }
            else
            {
                _signalMonitorTimer.Stop();

                if (e.State == AudioCaptureState.Idle)
                {
                    _latestPeakPercent = 0f;
                    _lastSignalDetectedAt = null;
                    ClearDisplayedRecognition();
                }
            }

            ApplyVisualState(e.State, _speechRecognitionService.State);
        });
    }

    private void AudioCaptureService_OnLevelChanged(object? sender, AudioLevelChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _latestPeakPercent = e.PeakPercent;
            ApplyVisualState(_audioCaptureService.State, _speechRecognitionService.State);
        });
    }

    private void AudioCaptureService_OnStatsChanged(object? sender, AudioCaptureStatsChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _lastSignalDetectedAt = e.LastSignalDetectedAt;
            DetailTextBlock.Text = BuildStatsDetailMessage(e.TotalCaptureDuration, e.TotalBytesCaptured);
            ApplyVisualState(_audioCaptureService.State, _speechRecognitionService.State);
        });
    }

    private void SpeechRecognitionService_OnStateChanged(object? sender, SpeechRecognitionStateChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.State is SpeechRecognitionState.NoRecognizer or SpeechRecognitionState.Faulted)
            {
                DetailTextBlock.Text = e.Message;
            }

            ApplyVisualState(_audioCaptureService.State, e.State);
        });
    }

    private void SpeechRecognitionService_OnSpeechRecognized(object? sender, RecognizedSpeechEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            AppendOriginalLine(e.Text);

            var selectedTranslationLanguage = TranslationLanguageComboBox.SelectedItem as LanguageOption;
            var translationLabel = selectedTranslationLanguage?.DisplayName ?? "Translation";
            TranslatedTextBlock.Text = $"{PendingTranslationText} ({translationLabel})";

            DetailTextBlock.Text = $"Detected: {ConvertLanguageCodeToDisplayName(e.LanguageCode)} | Confidence: {e.Confidence:P0}";
            ApplyVisualState(_audioCaptureService.State, _speechRecognitionService.State);
        });
    }

    private void SignalMonitorTimer_OnTick(object? sender, EventArgs e)
    {
        ApplyVisualState(_audioCaptureService.State, _speechRecognitionService.State);
    }

    private void ApplyVisualState()
    {
        ApplyVisualState(_audioCaptureService.State, _speechRecognitionService.State);
    }

    private void ApplyVisualState(AudioCaptureState captureState, SpeechRecognitionState recognitionState)
    {
        CaptureToggleButton.Content = _isCaptureEnabled ? "||" : ">>";
        CaptureToggleButton.ToolTip = _isCaptureEnabled ? "Stop audio capture" : "Start audio capture";

        CaptureEnabledIndicator.Fill = _isCaptureEnabled ? CreateBrush("#39D353") : CreateBrush("#6E6E6E");
        CaptureHealthIndicator.Fill = recognitionState switch
        {
            SpeechRecognitionState.Listening or SpeechRecognitionState.Ready or SpeechRecognitionState.Processing => CreateBrush("#39D353"),
            SpeechRecognitionState.NoRecognizer => CreateBrush("#F2C94C"),
            SpeechRecognitionState.Faulted => CreateBrush("#FF5F56"),
            _ => captureState == AudioCaptureState.Capturing ? CreateBrush("#39D353") : CreateBrush("#6E6E6E")
        };
        AudioSignalIndicator.Fill = IsSignalActive() ? CreateBrush("#39D353") : CreateBrush("#6E6E6E");

        var placeholderVisibility = _isCaptureEnabled ? Visibility.Collapsed : Visibility.Visible;
        OriginalPlaceholderBorder.Visibility = placeholderVisibility;
        TranslatedPlaceholderBorder.Visibility = placeholderVisibility;
    }

    private bool IsSignalActive()
    {
        if (_lastSignalDetectedAt is null)
        {
            return false;
        }

        return DateTime.Now - _lastSignalDetectedAt.Value <= TimeSpan.FromSeconds(2);
    }

    private string BuildCaptureDetailMessage(AudioCaptureState state)
    {
        return state switch
        {
            AudioCaptureState.Starting => "Starting loopback capture.",
            AudioCaptureState.Capturing => $"Device: {_currentDeviceName} | Format: {_currentFormatDescription}",
            AudioCaptureState.Stopping => "Stopping audio capture.",
            AudioCaptureState.Faulted => "An error occurred while capturing audio.",
            _ => "Start capture to monitor system audio."
        };
    }

    private string BuildStatsDetailMessage(TimeSpan duration, long totalBytesCaptured)
    {
        return $"Device: {_currentDeviceName} | Format: {_currentFormatDescription} | Duration: {duration:hh\\:mm\\:ss} | Captured: {FormatBytes(totalBytesCaptured)} | Peak: {_latestPeakPercent:0}%";
    }

    private void AppendOriginalLine(string text)
    {
        var normalized = text.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (_recentOriginalLines.Count > 0 && string.Equals(_recentOriginalLines.Last(), normalized, StringComparison.Ordinal))
        {
            OriginalTextBlock.Text = string.Join(Environment.NewLine, _recentOriginalLines);
            return;
        }

        _recentOriginalLines.Enqueue(normalized);

        while (_recentOriginalLines.Count > MaxDisplayedOriginalLines)
        {
            _recentOriginalLines.Dequeue();
        }

        OriginalTextBlock.Text = string.Join(Environment.NewLine, _recentOriginalLines);
    }

    private void ClearDisplayedRecognition()
    {
        _recentOriginalLines.Clear();
        OriginalTextBlock.Text = OriginalPlaceholderText;
        TranslatedTextBlock.Text = TranslatedPlaceholderText;
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        _signalMonitorTimer.Stop();
        _signalMonitorTimer.Tick -= SignalMonitorTimer_OnTick;

        _audioCaptureService.AudioChunkAvailable -= _speechRecognitionService.ProcessAudioChunk;
        _audioCaptureService.StateChanged -= AudioCaptureService_OnStateChanged;
        _audioCaptureService.LevelChanged -= AudioCaptureService_OnLevelChanged;
        _audioCaptureService.StatsChanged -= AudioCaptureService_OnStatsChanged;

        _speechRecognitionService.StateChanged -= SpeechRecognitionService_OnStateChanged;
        _speechRecognitionService.SpeechRecognized -= SpeechRecognitionService_OnSpeechRecognized;

        _speechRecognitionService.Dispose();
        _audioCaptureService.Dispose();
    }

    private static string ConvertCaptureStateToDisplayText(AudioCaptureState state)
    {
        return state switch
        {
            AudioCaptureState.Idle => "Idle",
            AudioCaptureState.Starting => "Starting",
            AudioCaptureState.Capturing => "Capturing",
            AudioCaptureState.Stopping => "Stopping",
            AudioCaptureState.Faulted => "Capture error",
            _ => state.ToString()
        };
    }

    private static string ConvertLanguageCodeToDisplayName(string languageCode)
    {
        return languageCode switch
        {
            "en-US" => "English",
            "ja-JP" => "Japanese",
            "zh-CN" => "Chinese",
            "ko-KR" => "Korean",
            _ => languageCode
        };
    }

    private static Brush CreateBrush(string color)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}
