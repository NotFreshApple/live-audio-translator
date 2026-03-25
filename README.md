# Live Audio Translator

Windows PC에서 재생 중인 시스템 오디오를 캡처하고, 이후 음성 인식과 한국어 번역을 거쳐 화면 하단 자막으로 표시하는 데스크톱 애플리케이션입니다.

## 현재 상태

- `C#`, `.NET 9`, `WPF` 기반 프로젝트 생성 완료
- `NAudio`를 이용한 Windows 루프백 오디오 캡처 추가
- 하단 오버레이형 자막 UI 구현
- 로컬 `Whisper` 기반 음성 인식 구조로 교체
- 인식 언어/번역 언어 선택 콤보박스 유지
- Windows 언어팩 의존성 제거

## 문서

- [프로젝트 개요](./docs/project-overview.md)
- [오디오 캡처 기능](./docs/features/audio-capture.md)
- [개발 기록](./docs/development-notes.md)

## 실행 방법

```powershell
dotnet build .\LiveAudioTranslator.sln
dotnet run --project .\LiveAudioTranslator.App
```

## 참고 사항

- 첫 실행 시 로컬 `Whisper` 기본 모델을 자동으로 내려받습니다.
- 모델 파일은 `%LOCALAPPDATA%\LiveAudioTranslator\models\ggml-base.bin`에 저장됩니다.

## 다음 단계

1. Whisper 인식 품질 개선용 VAD 및 전처리 추가
2. 실제 번역 엔진 연결
3. 부분 인식 결과와 최종 결과를 구분해 자막 반응성 개선
