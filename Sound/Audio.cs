using NAudio.Wave;
namespace Winpipe.Sound;

public class Audio : IDisposable
{
    private readonly WasapiLoopbackCapture _capture;
    private readonly Stream _outStream;
    private SilenceOut _silenceOut;
    private bool _disposed;
    public int SampleRate => _capture.WaveFormat.SampleRate;
    public int Channels => _capture.WaveFormat.Channels;
    public int BitsPerSample => _capture.WaveFormat.BitsPerSample;
    public string Format
    {
        get
        {
            return (_capture.WaveFormat.Encoding, _capture.WaveFormat.BitsPerSample) switch
            {
                (WaveFormatEncoding.Pcm, 16) => "s16le",
                (WaveFormatEncoding.Pcm, 24) => "s24le",
                (WaveFormatEncoding.Pcm, 32) => "s32le",
                (WaveFormatEncoding.IeeeFloat, 32) => "f32le",
                (WaveFormatEncoding.IeeeFloat, 64) => "f64le",
                _ => throw new InvalidOperationException($"Unsupported format: {Encoding} {BitsPerSample}"),
            };
        }
    }

    public WaveFormatEncoding Encoding => _capture.WaveFormat.Encoding;

    public Audio(Stream outStream)
    {
        _capture = new WasapiLoopbackCapture();
        _outStream = outStream;
        _silenceOut = new SilenceOut(_capture.WaveFormat);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try { _outStream.Write(e.Buffer, 0, e.BytesRecorded); }
        catch (Exception) { }
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
        if (_disposed) return;
        _silenceOut.Dispose();
        _outStream.Dispose();
        _capture.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
