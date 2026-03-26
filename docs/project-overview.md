# Project Overview

## Goal

This project aims to show recognized speech from Windows system audio in a persistent bottom overlay. The long-term goal is live translation, but the current implementation focuses on reliable local audio capture and local speech recognition.

## Current Architecture

- UI: WPF overlay window
- Audio input: `NAudio` with `WASAPI loopback`
- Speech recognition: `Whisper.net`
- Target framework: `net9.0-windows`
- IDE target: Visual Studio 2022

## Main User Flow

1. Start capture from the overlay UI.
2. Capture system audio from the default Windows output device.
3. Buffer audio until a speech segment is ready.
4. Convert audio to Whisper-friendly mono `16kHz` samples.
5. Run local Whisper inference.
6. Show recognized text in the original-text area.

## Current UX Behavior

- The overlay sits near the bottom of the screen.
- The original text area keeps the latest two recognized lines.
- The translation area is still placeholder-only.
- Capture state, recognition state, and live signal state are shown through indicators.

## Current Limitations

- No real translation engine is connected yet.
- Audio capture uses the default output device only.
- Recognition still depends heavily on CPU performance.
- Background music and mixed audio can reduce accuracy.

## Important Files

- `LiveAudioTranslator.App/MainWindow.xaml`
- `LiveAudioTranslator.App/MainWindow.xaml.cs`
- `LiveAudioTranslator.App/Services/AudioCapture/WasapiLoopbackAudioCaptureService.cs`
- `LiveAudioTranslator.App/Services/SpeechRecognition/LocalWhisperSpeechRecognitionService.cs`

## Verified Build

```powershell
dotnet build .\LiveAudioTranslator.sln
```
