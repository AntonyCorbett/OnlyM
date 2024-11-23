using System;
using NAudio.Wave;

namespace OnlyM.Services;

public static class WaveStreamExtensions
{
    // Set position of WaveStream to nearest block to supplied position
    public static void SetPosition(this WaveStream waveStream, long position)
    {
        // distance from block boundary (may be 0)
        var adj = position % waveStream.WaveFormat.BlockAlign;

        // adjust position to boundary and clamp to valid range
        var newPos = Math.Max(0, Math.Min(waveStream.Length, position - adj));

        // set playback position
        waveStream.Position = newPos;
    }

    // Set playback position of WaveStream by seconds
    public static void SetPosition(this WaveStream waveStream, double seconds)
    {
        waveStream.SetPosition((long)(seconds * waveStream.WaveFormat.AverageBytesPerSecond));
    }

    // Set playback position of WaveStream by time (as a TimeSpan)
    public static void SetPosition(this WaveStream waveStream, TimeSpan time)
    {
        waveStream.SetPosition(time.TotalSeconds);
    }

    // Set playback position of WaveStream relative to current position
    public static void Seek(this WaveStream waveStream, double offset) =>
        waveStream.SetPosition(waveStream.Position + (long)(offset * waveStream.WaveFormat.AverageBytesPerSecond));

    // Set playback position of WaveStream by seconds
    public static TimeSpan GetPosition(this WaveStream waveStream)
    {
        return TimeSpan.FromSeconds((double)waveStream.Position / waveStream.WaveFormat.AverageBytesPerSecond);
    }
}