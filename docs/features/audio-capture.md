# Audio Capture

## Summary

The application captures audio from the default Windows output device using `WASAPI loopback`. Captured chunks are forwarded to the recognition service and also used for live UI status updates.

## What It Tracks

- Capture state
- Device name
- Audio format
- Total captured bytes
- Total capture duration
- Current peak level
- Last detected signal time

## Current UI Integration

- Capture toggle button starts and stops loopback capture
- Status labels show capture and recognition health
- A signal indicator lights up when recent audio activity is detected
- Capture details and running stats are shown in the status line

## Current Limits

- Only the default output device is captured
- No device picker is implemented yet
- Capture quality depends on the Windows output routing and source content

## Related Files

- `LiveAudioTranslator.App/Services/AudioCapture/WasapiLoopbackAudioCaptureService.cs`
- `LiveAudioTranslator.App/Services/AudioCapture/AudioCaptureStatsChangedEventArgs.cs`
- `LiveAudioTranslator.App/MainWindow.xaml.cs`
