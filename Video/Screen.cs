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
    private int _width;
    private int _height;
    private bool _disposed;

    public int Width => _width;
    public int Height => _height;

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
        _width = 0;
        _height = 0;
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
                _width = (int)desc.Width;
                _height = (int)desc.Height;
            }

            _deviceResources.Context.CopyResource(_stagingTexture, texture);

            var box = _deviceResources.Context.Map(_stagingTexture!, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            try
            {
                int rowPitch = (int)box.RowPitch;
                int size = rowPitch * (int)desc.Height;
                var buffer = new byte[size];
                unsafe
                {
                    fixed (byte* pDest = buffer)
                    {
                        var pSrc = (byte*)box.DataPointer;
                        for (int y = 0; y < desc.Height; y++)
                        {
                            Buffer.MemoryCopy(pSrc + y * rowPitch, pDest + y * box.RowPitch, box.RowPitch, box.RowPitch);
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