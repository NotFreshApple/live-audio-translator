# Live Audio Translator

Windows PC에서 재생 중인 시스템 오디오를 캡처하고, 이후 음성 인식과 한국어 번역을 거쳐 화면 하단 자막으로 표시하는 데스크톱 애플리케이션입니다.

## 현재 상태

- `C#`, `.NET 9`, `WPF` 기반 프로젝트 생성 완료
- `NAudio`를 이용한 Windows 루프백 오디오 캡처 추가
- 장치 정보, 오디오 포맷, 실시간 입력 레벨을 확인할 수 있는 기본 화면 구현
- 일본어/한국어 음성 인식 기능 추가
- 인식 언어/번역 언어 선택 콤보박스 및 언어팩 설치 안내 추가

## 문서

- [프로젝트 개요](./docs/project-overview.md)
- [오디오 캡처 기능](./docs/features/audio-capture.md)
- [개발 기록](./docs/development-notes.md)

## 실행 방법

```powershell
dotnet build .\LiveAudioTranslator.sln
dotnet run --project .\LiveAudioTranslator.App
```

## 다음 단계

1. 캡처한 PCM 오디오를 STT 입력용 청크로 분리
2. 한국어 번역 엔진 연결
3. 항상 위에 표시되는 자막 오버레이 창 구현
