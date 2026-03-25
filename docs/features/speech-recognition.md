# 음성 인식 기능

## 목적

캡처된 시스템 오디오에서 음성을 감지하고, 이를 텍스트로 변환해 중앙 자막 영역에 표시하는 기능입니다.

## 지원 언어

- 영어
- 일본어
- 중국어
- 한국어

## 현재 구현 내용

- `System.Speech` 기반 Windows 음성 인식 엔진 사용
- 설치된 일본어/한국어 음성 인식기 자동 탐색
- 인식 언어 선택 콤보박스 제공
- 번역 언어 선택 콤보박스 제공
- 각 콤보박스 옆에 `설정 열기` 버튼 제공
- 버튼 상태는 단순 언어팩이 아니라 이 프로그램에서 실제 사용할 수 있는 음성 인식기 존재 여부를 기준으로 표시
- 미사용 상태에서 버튼을 누르면 Windows `언어 및 지역` 설정 화면을 엽니다.
- 오디오 캡처 청크를 누적한 뒤, 간단한 무음 구간 기준으로 발화 단위 인식 수행
- 두 언어 결과 중 신뢰도가 더 높은 결과를 선택
- 인식 결과를 중앙 자막 영역의 원문 줄에 표시
- 두 번째 줄에는 현재 인식된 언어를 표시

## 관련 파일

- `LiveAudioTranslator.App/Services/SpeechRecognition/ISpeechRecognitionService.cs`
- `LiveAudioTranslator.App/Services/SpeechRecognition/SystemSpeechRecognitionService.cs`
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
5. 선택된 인식 언어에 대응하는 음성 인식 엔진으로 전달합니다.
6. 인식 결과를 UI에 표시합니다.

## 전제 조건

- 선택한 언어에 대응하는 Windows 음성 인식기가 실제로 사용 가능해야 합니다.
- Windows 설정에서 언어팩과 음성 관련 구성 요소를 설치한 뒤에도 바로 사용 가능하지 않을 수 있으며, 재로그인 또는 재부팅이 필요할 수 있습니다.

## 현재 한계

- 번역 기능은 아직 연결되지 않았습니다.
- 단순 무음 기준 분리이므로 긴 문장에서는 분리 품질이 떨어질 수 있습니다.
- 시스템 오디오 환경에 따라 인식 정확도가 달라질 수 있습니다.
