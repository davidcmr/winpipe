using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using Winpipe.Sound;
using Winpipe.Video;

const string videoPipeName = "winpipe_video";
const string videoPipePath = @"\\.\pipe\winpipe_video";
const string audioPipeName = "winpipe_audio";
const string audioPipePath = @"\\.\pipe\winpipe_audio";
const int fps = 30;

var fileOutPath = Path.Combine(Directory.GetCurrentDirectory(), $".winpipe-full/{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
Directory.CreateDirectory(Path.GetDirectoryName(fileOutPath)!);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\nStopping recording...");
};

using var videoPipe = new NamedPipeServerStream(videoPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte);
// using var audioPipe = new NamedPipeServerStream(audioPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte);

Task videoReady = Task.Run(() => videoPipe.WaitForConnection());
// Task audioReady = Task.Run(() => audioPipe.WaitForConnection());
// using var audio = new Audio(audioPipe);
using var screen = new ScreenCapture(videoPipe, fps);

var startInfo = new ProcessStartInfo
{
    FileName = "ffmpeg",
    Arguments = $"-y -f rawvideo -pix_fmt bgra -s {screen.Width}x{screen.Height} -r {screen.Fps} -i \"{videoPipePath}\" -c:v libx264 -pix_fmt yuv420p \"{fileOutPath}\"",
    UseShellExecute = false,
    CreateNoWindow = true,
    RedirectStandardError = true,
    RedirectStandardOutput = true,
};
Console.WriteLine($"Executing: {startInfo.FileName} {startInfo.Arguments}");
using var ffmpeg = Process.Start(startInfo);
if (ffmpeg == null)
{
    Console.WriteLine("Failed to start ffmpeg. Is it on PATH?");
    Environment.Exit(1);
}

// Capture ffmpeg output
var ffmpegError = new StringBuilder();
ffmpeg.ErrorDataReceived += (s, e) =>
{
    if (e.Data != null)
    {
        Console.WriteLine($"FFmpeg: {e.Data}"); // Show ffmpeg output in real-time
        ffmpegError.AppendLine(e.Data);
    }
};
ffmpeg.BeginErrorReadLine();

Task.WaitAll(videoReady);
// Task.WaitAll(audioReady, videoReady);
Console.WriteLine("Recording video... Press Ctrl+C to stop.");
// audio.StartRecording(cts.Token);
var screenTask = Task.Run(() => screen.StartRecording(cts.Token));

// Wait for cancellation or ffmpeg to exit unexpectedly
Task.WaitAny(screenTask, Task.Run(() => ffmpeg.WaitForExit()));

// Stop recording gracefully
cts.Cancel();
try { screenTask.Wait(2000); } catch { }

// Close pipe so ffmpeg gets EOF and can finalize the MP4
Console.WriteLine("Closing pipe...");
videoPipe.Close();

// Wait for ffmpeg to finish writing
Console.WriteLine("Waiting for ffmpeg to finalize...");
ffmpeg.WaitForExit();
Console.WriteLine($"FFmpeg exited with code {ffmpeg.ExitCode}");