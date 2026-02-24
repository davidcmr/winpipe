using System.Diagnostics;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Winpipe.Video;

public sealed class Screen : IDisposable
{
    private readonly DeviceResources _deviceResources;
    private readonly IDXGIOutputDuplication _duplication;
    private readonly IDXGIOutput _output;
    private readonly IDXGIOutput1 _output1;
    private ID3D11Texture2D? _stagingTexture;
    private int _timeoutMs = 1000;
    private uint _width;  // Store as uint to match desc.Width type
    private uint _height; // Store as uint to match desc.Height type
    private bool _disposed;

    public int Width => (int)_width;  // Cast only when accessed
    public int Height => (int)_height; // Cast only when accessed

    /// <summary>
    /// Captures from the first output (monitor) of the default adapter.
    /// </summary>
    public Screen() : this(0, 0) { }

    /// <param name="adapterIndex">Adapter index (GPU).</param>
    /// <param name="outputIndex">Output index (monitor).</param>
    public Screen(int adapterIndex, int outputIndex)
    {
        _deviceResources = new DeviceResources();
        _deviceResources.Adapter.EnumOutputs((uint)outputIndex, out IDXGIOutput output);
        _output = output;
        _output1 = _output.QueryInterface<IDXGIOutput1>();
        _duplication = _output1.DuplicateOutput(_deviceResources.Device);
        var sw = Stopwatch.StartNew();
        while ((_width == 0 || _height == 0) && sw.ElapsedMilliseconds < _timeoutMs)
        {
            var frame = CaptureFrame(100);
            if (frame != null && _width > 0 && _height > 0) break;
            Thread.Sleep(50);
        }
        if (_width == 0 || _height == 0)
            throw new InvalidOperationException("Failed to get screen size");
    }

    /// <summary>
    /// Captures one frame. Returns null if no new frame (timeout) or on error.
    /// Caller must not keep the array; copy if needed.
    /// </summary>
    public byte[]? CaptureFrame(int timeoutMs = 100)
    {
        var result = _duplication.AcquireNextFrame((uint)timeoutMs, out OutduplFrameInfo frameInfo, out IDXGIResource? desktopResource);

        if (result == Vortice.DXGI.ResultCode.WaitTimeout)
            return null; // no new frame
        if (result.Failure || desktopResource == null)
            return null;

        try
        {
            var texture = desktopResource.QueryInterface<ID3D11Texture2D>();
            var desc = texture.Description;

            if (_width != desc.Width || _height != desc.Height)
            {
                _stagingTexture?.Dispose();
                _stagingTexture = _deviceResources.Device.CreateTexture2D(new Texture2DDescription
                {
                    Width = desc.Width,
                    Height = desc.Height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = desc.Format,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Staging,
                    BindFlags = BindFlags.None,
                    CPUAccessFlags = CpuAccessFlags.Read
                });
                _width = desc.Width;   // No cast needed!
                _height = desc.Height;  // No cast needed!
            }

            _deviceResources.Context.CopyResource(_stagingTexture, texture);

            var box = _deviceResources.Context.Map(_stagingTexture!, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            try
            {
                uint rowPitch = box.RowPitch;  // No cast needed!
                uint widthBytes = _width * 4;  // BGRA = 4 bytes per pixel, no cast!
                uint size = widthBytes * _height;  // No cast!
                var buffer = new byte[size];
                unsafe
                {
                    fixed (byte* pDest = buffer)
                    {
                        var pSrc = (byte*)box.DataPointer;
                        for (uint y = 0; y < _height; y++)  // Use uint in loop
                        {
                            Buffer.MemoryCopy(
                                pSrc + y * rowPitch,      // Source: start of row (may have padding)
                                pDest + y * widthBytes,   // Dest: start of row (no padding)
                                widthBytes,                // Copy only actual pixel data
                                widthBytes);
                        }
                    }
                }
                return buffer;
            }
            finally
            {
                _deviceResources.Context.Unmap(_stagingTexture, 0);
            }
        }
        finally
        {
            desktopResource?.Dispose();
            _duplication.ReleaseFrame();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _stagingTexture?.Dispose();
        _duplication?.Dispose();
        _output1?.Dispose();
        _output?.Dispose();
        _deviceResources?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}