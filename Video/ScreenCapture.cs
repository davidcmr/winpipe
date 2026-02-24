using System.Diagnostics;

namespace Winpipe.Video;

public class ScreenCapture : IDisposable
{
    private readonly Screen _screen;
    private readonly Stream _videoStream;
    private readonly long _frameIntervalTicks;
    private bool _disposed;

    public int Width => _screen.Width;
    public int Height => _screen.Height;
    public int Fps { get; }

    public ScreenCapture(Stream videoStream, int fps = 30)
    {
        if (fps <= 10 || fps > 120) throw new ArgumentOutOfRangeException(nameof(fps), fps, "FPS must be between 10 and 120");
        _screen = new Screen();
        _videoStream = videoStream;
        Fps = fps;
        // Convert to ticks: 1 second / fps = TimeSpan.TicksPerSecond / fps
        _frameIntervalTicks = TimeSpan.TicksPerSecond / fps;
    }

    /// <summary>
    /// Capture frames and write raw BGRA to the stream (e.g. video named pipe).
    /// Caller owns the stream; this method does not close or dispose it.
    /// </summary>
    public void StartRecording(CancellationToken cancellationToken = default)
    {
        byte[]? firstFrame = null;
        for (int i = 0; i < 50; i++)
        {
            firstFrame = _screen.CaptureFrame(100);
            if (firstFrame != null) break;
            cancellationToken.ThrowIfCancellationRequested();
            cancellationToken.WaitHandle.WaitOne(50);
        }

        if (firstFrame == null)
            throw new InvalidOperationException("No frame acquired; is the desktop visible?");

        try
        {
            _videoStream.Write(firstFrame, 0, firstFrame.Length);
            _videoStream.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write first frame to video stream: {ex.Message}");
        }

        var sw = Stopwatch.StartNew();
        byte[] lastFrame = firstFrame;
        long nextWriteTicks = _frameIntervalTicks;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Capture frames as fast as possible (non-blocking) - no delays here
            var frame = _screen.CaptureFrame(0);
            if (frame != null)
                lastFrame = frame;

            // Convert elapsed time to TimeSpan ticks for comparison
            long elapsedTicks = sw.Elapsed.Ticks;

            // Write frame at exactly the target FPS (every frameIntervalTicks)
            if (elapsedTicks >= nextWriteTicks)
            {
                try
                {
                    _videoStream.Write(lastFrame, 0, lastFrame.Length);
                    _videoStream.Flush();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to write frame to video stream: {ex.Message}");
                }
                nextWriteTicks += _frameIntervalTicks;
            }

            // Only sleep if we're ahead of schedule - otherwise keep capturing
            long sleepTicks = nextWriteTicks - elapsedTicks;
            if (sleepTicks > TimeSpan.TicksPerMillisecond) // 1ms in ticks
            {
                int sleepMs = (int)(sleepTicks / TimeSpan.TicksPerMillisecond);
                cancellationToken.WaitHandle.WaitOne(Math.Min(sleepMs, 1)); // Max 1ms sleep
            }
            // If sleepTicks <= 1ms, don't sleep - keep the loop tight for maximum capture rate
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _screen.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}