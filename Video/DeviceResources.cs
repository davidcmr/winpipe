using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;

namespace Winpipe.Video;

public sealed class DeviceResources : IDisposable
{
    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;
    private readonly IDXGIAdapter _adapter;
    private bool _disposed;

    public ID3D11Device Device => _device;
    public ID3D11DeviceContext Context => _context;
    public IDXGIAdapter Adapter => _adapter;

    public DeviceResources()
    {
        var featureLevels = new[]
        {
            FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0
        };

        var result = D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            featureLevels,
            out ID3D11Device? device,
            out ID3D11DeviceContext? context);

        if (!result.Success || device == null || context == null)
            throw new InvalidOperationException($"Failed to create D3D11 device: {result}");

        _device = device;
        _context = context;
        _adapter = device.QueryInterface<IDXGIDevice>().GetAdapter();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _adapter?.Dispose();
        _context?.Dispose();
        _device?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
