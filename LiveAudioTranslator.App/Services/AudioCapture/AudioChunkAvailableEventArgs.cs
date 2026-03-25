using NAudio.Wave;

namespace LiveAudioTranslator.App.Services.AudioCapture;

public sealed class AudioChunkAvailableEventArgs : EventArgs
{
    public AudioChunkAvailableEventArgs(byte[] buffer, int bytesRecorded, WaveFormat waveFormat, float peakPercent)
    {
        Buffer = buffer;
        BytesRecorded = bytesRecorded;
        WaveFormat = waveFormat;
        PeakPercent = peakPercent;
    }

    public byte[] Buffer { get; }

    public int BytesRecorded { get; }

    public WaveFormat WaveFormat { get; }

    public float PeakPercent { get; }
}
