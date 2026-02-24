

using System.Diagnostics;
using System.IO.Pipes;

const string videoPipeName = "winpipe_video";
const string audioPipeName = "winpipe_audio";

using var videoPipe = new NamedPipeServerStream(videoPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte);
using var audioPipe = new NamedPipeServerStream(audioPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte);

Task videoReady = Task.Run(() => videoPipe.WaitForConnection());
Task audioReady = Task.Run(() => audioPipe.WaitForConnection());

var startInfo = new ProcessStartInfo
{
    FileName = "ffmpeg",
    Arguments = $"-y -f rawvideo -pix_fmt bgra -s {w}x{h} -r 30 -i \\\\.\\pipe\\{videoPipeName} -f s16le -ac 2 -ar 48000 -i \\\\.\\pipe\\{audioPipeName} -c:v libx264 -pix_fmt yuv420p -c:a aac -shortest \"{outputPath}\"",
    UseShellExecute = false,
    CreateNoWindow = true,
};

using var ffmpeg = Process.Start(startInfo);
Task.WaitAll(videoReady, audioReady);