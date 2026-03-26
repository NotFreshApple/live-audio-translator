# Live Audio Translator

Windows desktop app for capturing system audio, running local speech recognition with Whisper, and presenting recognized text in a bottom overlay UI.

## Current Status

- Built with `C#`, `.NET 9`, and `WPF`
- Captures Windows output audio through `WASAPI loopback`
- Uses local `Whisper.net` speech recognition
- Supports recognition language selection: English, Japanese, Chinese, Korean
- Uses a balanced recognition profile tuned for lower latency without cutting speech too aggressively
- Keeps the most recent two recognized lines visible in the original text area
- Translation UI is present, but an actual translation engine is not connected yet

## Build And Run

```powershell
dotnet build .\LiveAudioTranslator.sln
dotnet run --project .\LiveAudioTranslator.App
```

## Key Runtime Notes

- The Whisper model is stored at `%LOCALAPPDATA%\LiveAudioTranslator\models\ggml-base.bin`
- The first run may download the default model automatically
- Recognition quality and latency still depend on CPU performance because inference is local

## Documentation

- [Project Overview](./docs/project-overview.md)
- [Development Notes](./docs/development-notes.md)
- [Audio Capture](./docs/features/audio-capture.md)
- [Speech Recognition](./docs/features/speech-recognition.md)

## Recent Functional Changes

- Installed and validated the local Git/GitHub CLI environment
- Prepared the machine for VS2022 / `.NET 9` builds
- Reduced recognition overhead by reusing the Whisper processor and feeding `float[]` samples directly
- Moved recognition tuning from aggressive low-latency values to a balanced profile
- Changed the original-text display from single-line replacement to a rolling two-line view

## Next Steps

1. Connect a real translation engine
2. Add runtime options for latency / quality profiles
3. Evaluate GPU-backed Whisper runtime options for Windows
