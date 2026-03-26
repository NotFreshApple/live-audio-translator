# Development Notes

## 2026-03-26

### Environment preparation

- Installed Git and GitHub CLI on the development machine
- Logged into GitHub and configured global Git settings
- Created a stable local workspace under `C:\Users\wotkd\code`
- Installed `.NET 9 SDK`
- Verified that the solution builds successfully on this machine

### Speech recognition performance changes

- Moved away from WAV byte serialization during recognition
- Converted audio directly to `float[]` samples for Whisper
- Reused the Whisper processor instead of rebuilding it for each recognition pass
- Set Whisper thread usage dynamically from available CPU cores

### Recognition profile update

- Tested a more aggressive low-latency configuration
- Switched to a balanced profile after review to avoid cutting speech too early
- Current tuned values:
  - `SilenceTimeout = 700ms`
  - `MinimumSpeechLength = 550ms`
  - `MaximumSpeechLength = 5.5s`

### UI update

- Rebuilt `MainWindow.xaml.cs` into a readable clean state because the previous file had corrupted display strings
- Changed the original-text area to keep the latest two recognized lines
- Left translated output as a placeholder because translation is not implemented yet

### Verification

```powershell
dotnet build .\LiveAudioTranslator.sln -c Debug
```
