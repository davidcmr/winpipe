using System.Diagnostics;

namespace Winpipe.Video;

public class ScreenCapture : IDisposable
{
    private readonly Screen _screen;
    private Process? _ffmpeg;
    private Stream? _ffmpegStdin;
    private string _outputPath;
    private bool _disposed;

    public int Width => _screen.Width;
    public int Height => _screen.Height;
    public string OutputPath => _outputPath;

    public ScreenCapture(string? outputPath = null)
    {
        _screen = new Screen();
        _outputPath = outputPath ?? string.Empty;
        if (string.IsNullOrEmpty(_outputPath))
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), ".winpipe-video");
            Directory.CreateDirectory(dir);
            _outputPath = Path.Combine(dir, $"screen-{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
        }
    }

    /// <summary>
    /// Start recording: capture frames, pipe to ffmpeg, block until cancelled.
    /// </summary>
    public void StartRecording(CancellationToken cancellationToken = default)
    {
        byte[]? firstFrame = null;
        for (int i = 0; i < 50; i++)
        {
            firstFrame = _screen.CaptureFrame(100);
            if (firstFrame != null) break;
            cancellationToken.ThrowIfCancellationRequested();
            Thread.Sleep(50);
        }

        if (firstFrame == null)
            throw new InvalidOperationException("No frame acquired; is the desktop visible?");

        int w = _screen.Width;
        int h = _screen.Height;

        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -f rawvideo -pix_fmt bgra -s {w}x{h} -r 30 -i pipe:0 -c:v libx264 -pix_fmt yuv420p \"{_outputPath}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        _ffmpeg = Process.Start(startInfo);
        if (_ffmpeg == null)
            throw new InvalidOperationException("Failed to start ffmpeg. Is it on PATH?");

        _ffmpegStdin = _ffmpeg.StandardInput.BaseStream;

        try
        {
            _ffmpegStdin.Write(firstFrame, 0, firstFrame.Length);

            const int frameIntervalMs = 33; // ~30 fps
            var sw = Stopwatch.StartNew();
            byte[]? lastFrame = firstFrame;

            while (!cancellationToken.IsCancellationRequested)
            {
                var frame = _screen.CaptureFrame(0);
                if (frame != null)
                    lastFrame = frame;
                if (lastFrame != null)
                    _ffmpegStdin.Write(lastFrame, 0, lastFrame.Length);

                var elapsed = sw.ElapsedMilliseconds;
                var next = (int)((elapsed / frameIntervalMs + 1) * frameIntervalMs - elapsed);
                if (next > 0)
                    cancellationToken.WaitHandle.WaitOne(Math.Min(next, 100));
            }
        }
        finally
        {
            _ffmpegStdin?.Close();
            _ffmpegStdin = null;
            _ffmpeg?.WaitForExit(5000);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _ffmpegStdin?.Close();
        _ffmpegStdin = null;
        _ffmpeg?.Dispose();
        _ffmpeg = null;
        _screen.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}