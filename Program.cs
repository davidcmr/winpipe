using System.Diagnostics;
using Winpipe.Sound;
using Winpipe.Video;

// using var audio = new Audio();
// Console.WriteLine("Recording audio...");
// audio.StartRecording();
// Console.WriteLine("Audio recording stopped.");

var videoDir = Path.Combine(Directory.GetCurrentDirectory(), ".winpipe-video");
Directory.CreateDirectory(videoDir);
var outputPath = Path.Combine(videoDir, $"screen-{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

using var screen = new ScreenCapture();
Console.WriteLine("Screen capture started...");
byte[]? firstFrame = null;
for (int i = 0; i < 50; i++)
{
    firstFrame = screen.CaptureFrame(30);
    if (firstFrame != null) break;
    Thread.Sleep(50);
}
if (firstFrame == null)
{
    Console.WriteLine("No frame acquired; is the desktop visible?");
    Environment.Exit(1);
}

int w = screen.Width;
int h = screen.Height;
Console.WriteLine($"Screen size: {w}x{h}, Path: {outputPath}");
var startInfo = new ProcessStartInfo
{
    FileName = "ffmpeg",
    Arguments = $"-y -f rawvideo -pix_fmt bgra -s {w}x{h} -r 30 -i pipe:0 -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"",
    UseShellExecute = false,
    RedirectStandardInput = true,
    RedirectStandardError = true,
    CreateNoWindow = true,
};
using var ffmpeg = Process.Start(startInfo);
if (ffmpeg == null)
{
    Console.WriteLine("Failed to start ffmpeg. Is it on PATH?");
    Environment.Exit(1);
}
var stdin = ffmpeg.StandardInput.BaseStream;
stdin.Write(firstFrame, 0, firstFrame.Length);
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};


try
{
    while (!cts.Token.IsCancellationRequested)
    {
        var frame = screen.CaptureFrame(0);
        if (frame != null)
            stdin.Write(frame, 0, frame.Length);
        else
            Thread.Sleep(10);
    }
}
finally
{
    stdin.Close();
    ffmpeg.WaitForExit(5000);
}

Console.WriteLine("Screen capture stopped.");
Environment.Exit(0);
