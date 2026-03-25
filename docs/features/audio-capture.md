# 오디오 캡처 기능

## 목적

Windows에서 현재 재생 중인 시스템 오디오를 실시간으로 읽고, 이후 음성 인식과 번역 단계에서 사용할 수 있는 입력 데이터로 제공하는 기능입니다.

## 현재 구현 내용

- `WASAPI loopback capture` 방식으로 기본 출력 장치의 오디오를 캡처합니다.
- 프로그램 실행 시 화면 하단에 반투명한 검은색 오버레이 박스를 표시합니다.
- 좌측에 오디오 캡처 켜기/끄기 버튼을 제공합니다.
- 버튼 옆에 현재 상태를 보여주는 인디케이터 3개를 제공합니다.
  - 캡처 활성화 여부
  - 캡처 세션 정상 동작 여부
  - 실제 오디오 신호 감지 여부
- 중앙에는 자막 표시 영역을 두고, 원문 1줄과 번역문 1줄을 출력할 수 있도록 구성합니다.
- 우측 상단에 프로그램 종료 버튼을 제공합니다.
- 캡처 중에는 장치명, 포맷, 누적 시간, 수집 크기, 최근 피크 값을 상태 문구에 반영합니다.
- 캡처 오디오는 내부적으로 음성 인식 서비스에도 전달됩니다.

## 관련 파일

- `LiveAudioTranslator.App/MainWindow.xaml`
- `LiveAudioTranslator.App/MainWindow.xaml.cs`
- `LiveAudioTranslator.App/Services/AudioCapture/IAudioCaptureService.cs`
- `LiveAudioTranslator.App/Services/AudioCapture/WasapiLoopbackAudioCaptureService.cs`
- `LiveAudioTranslator.App/Services/AudioCapture/AudioCaptureState.cs`
- `LiveAudioTranslator.App/Services/AudioCapture/AudioCaptureStateChangedEventArgs.cs`
- `LiveAudioTranslator.App/Services/AudioCapture/AudioLevelChangedEventArgs.cs`
- `LiveAudioTranslator.App/Services/AudioCapture/AudioCaptureStatsChangedEventArgs.cs`

## 동작 방식

1. 프로그램이 열리면 작업 영역 하단 1/10 높이에 맞춰 오버레이 창을 배치합니다.
2. 사용자가 캡처 버튼을 누르면 기본 출력 장치에 대해 루프백 캡처를 시작합니다.
3. 들어오는 오디오 버퍼에서 피크 레벨을 계산합니다.
4. 수집 바이트 수와 누적 시간을 계산합니다.
5. 일정 수준 이상의 신호가 들어오면 최근 신호 감지 시각을 갱신합니다.
6. 계산된 상태를 인디케이터와 상태 문구에 반영합니다.

## 인디케이터 기준

- `캡처 활성화`: 캡처 기능이 켜져 있으면 초록색
- `캡처 정상 동작`: 시작 중/중지 중은 노란색, 정상 캡처 중은 초록색, 오류는 빨간색
- `오디오 신호 감지`: 최근 2초 안에 유효한 오디오 피크가 감지되면 초록색

## 현재 한계

- 기본 출력 장치만 캡처합니다.
- 장치 선택 기능은 아직 없습니다.
- 실제 자막 출력은 아직 연결되지 않았습니다.
- 번역 엔진은 아직 연결되지 않았습니다.
