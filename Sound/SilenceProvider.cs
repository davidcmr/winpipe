
using NAudio.Wave;

class SilenceProvider : IWaveProvider
{
    public SilenceProvider(WaveFormat format) => WaveFormat = format;
    public WaveFormat WaveFormat { get; }
    public int Read(byte[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        return count;
    }
}