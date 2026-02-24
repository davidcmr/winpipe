using System.Diagnostics;
using System.IO.Pipes;
using Winpipe.Sound;

const string videoPipeName = "winpipe_video";
const string videoPipePath = @"\\.\pipe\winpipe_video";
const string audioPipeName = "winpipe_audio";
const string audioPipePath = @"\\.\pipe\winpipe_audio";
var fileOutPath = Path.Combine(Directory.GetCurrentDirectory(), $".winpipe-full/{DateTime.Now:yyyyMMdd_HHmmss}.wav");
Directory.CreateDirectory(Path.GetDirectoryName(fileOutPath)!);

// using var videoPipe = new NamedPipeServerStream(videoPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte);
using var audioPipe = new NamedPipeServerStream(audioPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte);

// Task videoReady = Task.Run(() => videoPipe.WaitForConnection());
Task audioReady = Task.Run(() => audioPipe.WaitForConnection());
using var audio = new Audio(audioPipe);

var startInfo = new ProcessStartInfo
{
    FileName = "ffmpeg",
    Arguments = $"-y -f {audio.Format} -ac {audio.Channels} -ar {audio.SampleRate} -i \"{audioPipePath}\" \"{fileOutPath}\"",
    UseShellExecute = false,
    CreateNoWindow = true,
};
Console.WriteLine($"Executing: {startInfo.FileName} {startInfo.Arguments}");
using var ffmpeg = Process.Start(startInfo);
if (ffmpeg == null)
{
    Console.WriteLine("Failed to start ffmpeg. Is it on PATH?");
    Environment.Exit(1);
}
Task.WaitAll(audioReady);
Console.WriteLine("Recording audio...");
audio.StartRecording();
ffmpeg.WaitForExit();
Console.WriteLine($"FFmpeg exited with code {ffmpeg.ExitCode}");
