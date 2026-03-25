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
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly ISpeechRecognitionService _speechRecognitionService;
    private readonly DispatcherTimer _signalMonitorTimer;
    private readonly List<LanguageOption> _languageOptions;
    private bool _isCaptureEnabled;
    private DateTime? _lastSignalDetectedAt;
    private float _latestPeakPercent;
    private string _currentDeviceName = "기본 출력 장치";
    private string _currentFormatDescription = "알 수 없음";

    public MainWindow()
    {
        InitializeComponent();

        _audioCaptureService = new WasapiLoopbackAudioCaptureService();
        _speechRecognitionService = new LocalWhisperSpeechRecognitionService();
        _languageOptions =
        [
            new LanguageOption { DisplayName = "영어", CultureCode = "en-US" },
            new LanguageOption { DisplayName = "일본어", CultureCode = "ja-JP" },
            new LanguageOption { DisplayName = "중국어", CultureCode = "zh-CN" },
            new LanguageOption { DisplayName = "한국어", CultureCode = "ko-KR" }
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
        DetailTextBlock.Text = $"{selectedLanguage.DisplayName} 인식 언어를 선택했습니다.";
    }

    private void TranslationLanguageComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TranslationLanguageComboBox.SelectedItem is not LanguageOption selectedLanguage)
        {
            return;
        }

        DetailTextBlock.Text = $"{selectedLanguage.DisplayName} 번역 언어를 선택했습니다.";
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
            StatusTextBlock.Text = "캡처 시작 실패";
            DetailTextBlock.Text = exception.Message;
            ApplyVisualState();

            MessageBox.Show(
                this,
                exception.Message,
                "오디오 캡처 오류",
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
            _currentDeviceName = string.IsNullOrWhiteSpace(e.DeviceName) ? "기본 출력 장치" : e.DeviceName;
            _currentFormatDescription = string.IsNullOrWhiteSpace(e.FormatDescription) ? "알 수 없음" : e.FormatDescription;

            StatusTextBlock.Text = ConvertCaptureStateToKorean(e.State);
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
                    OriginalTextBlock.Text = "원문 텍스트가 이 줄에 표시됩니다.";
                    TranslatedTextBlock.Text = "번역 텍스트가 이 줄에 표시됩니다.";
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
                if (e.State == SpeechRecognitionState.NoRecognizer)
                {
                    TranslatedTextBlock.Text = "선택한 인식 언어의 음성 인식이 아직 사용 불가합니다.";
                }
            }

            ApplyVisualState(_audioCaptureService.State, e.State);
        });
    }

    private void SpeechRecognitionService_OnSpeechRecognized(object? sender, RecognizedSpeechEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            OriginalTextBlock.Text = e.Text;

            var selectedTranslationLanguage = TranslationLanguageComboBox.SelectedItem as LanguageOption;
            var translationLabel = selectedTranslationLanguage?.DisplayName ?? "번역";
            TranslatedTextBlock.Text = $"{ConvertLanguageCodeToDisplayName(e.LanguageCode)} 인식 완료 / {translationLabel} 번역 대기";

            DetailTextBlock.Text = $"인식 언어: {ConvertLanguageCodeToDisplayName(e.LanguageCode)} | 신뢰도: {e.Confidence:P0}";
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
        CaptureToggleButton.Content = _isCaptureEnabled ? "■" : "●";
        CaptureToggleButton.ToolTip = _isCaptureEnabled ? "오디오 캡처 끄기" : "오디오 캡처 켜기";

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
            AudioCaptureState.Starting => "기본 출력 장치에서 오디오 캡처를 시작하는 중입니다.",
            AudioCaptureState.Capturing => $"장치: {_currentDeviceName} | 포맷: {_currentFormatDescription}",
            AudioCaptureState.Stopping => "오디오 캡처를 중지하는 중입니다.",
            AudioCaptureState.Faulted => "오디오 캡처 중 오류가 발생했습니다.",
            _ => "캡처를 켜면 기본 출력 장치의 시스템 오디오를 감시합니다."
        };
    }

    private string BuildStatsDetailMessage(TimeSpan duration, long totalBytesCaptured)
    {
        return $"장치: {_currentDeviceName} | 포맷: {_currentFormatDescription} | 누적 시간: {duration:hh\\:mm\\:ss} | 수집 크기: {FormatBytes(totalBytesCaptured)} | 최근 피크: {_latestPeakPercent:0}%";
    }

    private void Window_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
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

    private static string ConvertCaptureStateToKorean(AudioCaptureState state)
    {
        return state switch
        {
            AudioCaptureState.Idle => "대기 중",
            AudioCaptureState.Starting => "캡처 시작 중",
            AudioCaptureState.Capturing => "캡처 동작 중",
            AudioCaptureState.Stopping => "캡처 중지 중",
            AudioCaptureState.Faulted => "캡처 오류",
            _ => state.ToString()
        };
    }

    private static string ConvertLanguageCodeToDisplayName(string languageCode)
    {
        return languageCode switch
        {
            "en-US" => "영어",
            "ja-JP" => "일본어",
            "zh-CN" => "중국어",
            "ko-KR" => "한국어",
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
