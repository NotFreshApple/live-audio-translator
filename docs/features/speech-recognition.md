# Speech Recognition

## Summary

The application uses local Whisper inference through `Whisper.net`. Audio captured from the Windows output device is buffered, converted to mono `16kHz` samples, and transcribed locally.

## Current Recognition Pipeline

1. Audio capture emits chunks continuously.
2. The recognition service buffers those chunks.
3. A speech segment is flushed when silence is detected or the buffered length reaches the configured cap.
4. The buffered audio is converted to mono `16kHz` samples.
5. Whisper processes the samples and returns transcribed segments.
6. The UI displays the newest recognized line while keeping the previous line visible.

## Balanced Profile

The current profile is intentionally balanced between latency and readability:

- Signal threshold: `3%`
- Silence timeout: `700ms`
- Minimum speech length: `550ms`
- Maximum speech length: `5.5s`

This is slower than an aggressive low-latency setup, but it reduces premature sentence splitting and helps preserve recognition quality.

## Performance Improvements Already Applied

- Whisper processor reuse
- Explicit thread selection
- Direct `float[]` sample feeding
- Avoiding repeated WAV serialization

## User-Facing Behavior

- Recognition language can be selected from the UI
- The original text area shows the latest two recognized lines
- Duplicate consecutive recognition results are suppressed
- Translation output is still placeholder-only

## Known Limits

- CPU performance has a strong effect on latency
- Background music or noisy mixed audio reduces accuracy
- There is no VAD model yet; segmentation still relies on simple signal and silence timing
- There is no true translation backend yet

## Related Files

- `LiveAudioTranslator.App/Services/SpeechRecognition/LocalWhisperSpeechRecognitionService.cs`
- `LiveAudioTranslator.App/Services/SpeechRecognition/RecognizedSpeechEventArgs.cs`
- `LiveAudioTranslator.App/MainWindow.xaml.cs`
