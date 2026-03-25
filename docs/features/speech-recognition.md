# 음성 인식 기능

## 목적

캡처된 시스템 오디오에서 음성을 감지하고, 이를 텍스트로 변환해 중앙 자막 영역에 표시하는 기능입니다.

## 지원 언어

- 영어
- 일본어
- 중국어
- 한국어

## 현재 구현 내용

- `Whisper.net` 기반 로컬 Whisper 엔진 사용
- 앱 시작 후 필요 시 기본 Whisper 모델 자동 다운로드
- 인식 언어 선택 콤보박스 제공
- 번역 언어 선택 콤보박스 제공
- 오디오 캡처 청크를 누적한 뒤, 간단한 무음 구간 기준으로 발화 단위 인식 수행
- 입력 오디오를 `16kHz mono PCM`으로 변환한 뒤 Whisper에 전달
- 인식 결과를 중앙 자막 영역의 원문 줄에 표시
- 두 번째 줄에는 현재 인식된 언어를 표시

## 관련 파일

- `LiveAudioTranslator.App/Services/SpeechRecognition/ISpeechRecognitionService.cs`
- `LiveAudioTranslator.App/Services/SpeechRecognition/LocalWhisperSpeechRecognitionService.cs`
- `LiveAudioTranslator.App/Services/SpeechRecognition/SpeechRecognitionState.cs`
- `LiveAudioTranslator.App/Services/SpeechRecognition/SpeechRecognitionStateChangedEventArgs.cs`
- `LiveAudioTranslator.App/Services/SpeechRecognition/RecognizedSpeechEventArgs.cs`
- `LiveAudioTranslator.App/Services/AudioCapture/AudioChunkAvailableEventArgs.cs`
- `LiveAudioTranslator.App/MainWindow.xaml`
- `LiveAudioTranslator.App/MainWindow.xaml.cs`

## 동작 방식

1. 캡처 서비스가 원시 오디오 청크를 전달합니다.
2. 음성 인식 서비스가 청크를 버퍼에 누적합니다.
3. 일정 시간 무음이 이어지면 하나의 발화로 간주합니다.
4. 오디오를 인식용 16kHz mono PCM 형식으로 변환합니다.
5. 선택된 인식 언어에 맞춰 로컬 Whisper 처리기를 생성합니다.
6. 인식 결과를 UI에 표시합니다.

## 전제 조건

- 앱 실행 중 기본 Whisper 모델을 내려받을 수 있어야 하거나, 미리 `%LOCALAPPDATA%\LiveAudioTranslator\models\ggml-base.bin` 파일이 준비되어 있어야 합니다.
- Windows에서 Whisper 런타임을 사용할 수 있는 환경이어야 합니다.

## 현재 한계

- 번역 기능은 아직 연결되지 않았습니다.
- 단순 무음 기준 분리이므로 긴 문장에서는 분리 품질이 떨어질 수 있습니다.
- 배경음악이 큰 환경에서는 인식 정확도가 떨어질 수 있습니다.
