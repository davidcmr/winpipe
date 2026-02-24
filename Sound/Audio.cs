using NAudio.Wave;
namespace Winpipe.Sound;

public class Audio : IDisposable
{
    private readonly WasapiLoopbackCapture _capture;
    private readonly WaveFileWriter _writer;
    private SilenceOut _silenceOut;

    public Audio()
    {
        var outputFolder = Path.Combine(Directory.GetCurrentDirectory(), ".winpipe-audio");
        Directory.CreateDirectory(outputFolder);
        var outputFilePath = Path.Combine(outputFolder, $"recorded-{DateTime.Now:yyyyMMdd_HHmmss}.wav");
        _capture = new WasapiLoopbackCapture();
        _writer = new WaveFileWriter(outputFilePath, _capture.WaveFormat);
        _silenceOut = new SilenceOut(_capture.WaveFormat);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _writer.Write(e.Buffer, 0, e.BytesRecorded);
    }

    public void StartRecording(CancellationToken cancellationToken = default)
    {
        _capture.DataAvailable += OnDataAvailable;
        _silenceOut.Play();
        _capture.StartRecording();
        WaitForCaptureState(cancellationToken);
    }

    private void WaitForCaptureState(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.CanBeCanceled)
            cancellationToken.Register(() => _capture.StopRecording());
        try
        {
            while (_capture.CaptureState != NAudio.CoreAudioApi.CaptureState.Stopped)
            {
                cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
            }
        }
        finally
        {
            _capture.DataAvailable -= OnDataAvailable;
        }
    }

    public void Dispose()
    {
        _silenceOut.Dispose();
        _writer.Dispose();
        _capture.Dispose();
    }
}
