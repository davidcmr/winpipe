using NAudio.Wave;

class SilenceOut : IDisposable
{
    private readonly IWavePlayer _player;
    private readonly SilenceProvider _provider;

    /// <summary>
    /// Starts playing silence to the default output device.
    /// </summary>
    /// <param name="format">Wave format (e.g. capture.WaveFormat). If null, uses 44.1kHz 16-bit stereo.</param>
    public SilenceOut(WaveFormat? format = null)
    {
        _provider = new SilenceProvider(format ?? new WaveFormat(44100, 16, 2));
        _player = new WasapiOut();
        _player.Init(_provider);
    }
    public void Play()
    {
        _player.Play();
    }

    public void Stop()
    {
        _player.Stop();
    }

    public void Dispose()
    {
        _player.Stop();
        _player.Dispose();
    }
}
