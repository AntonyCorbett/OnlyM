using FFmpeg.AutoGen;
using OnlyM.MediaElementAdaption;

namespace OnlyM.Tests;

public class HardwareDecoderProbeTests
{
    private static readonly AVHWDeviceType[] Candidates =
    [
        AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA,
        AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2,
    ];

    [Fact]
    public void SelectDeviceType_D3D11VAAvailable_PrefersD3D11VAAndStopsChecking()
    {
        var checkedTypes = new List<AVHWDeviceType>();

        var result = HardwareDecoderProbe.SelectDeviceType(Candidates, t =>
        {
            checkedTypes.Add(t);
            return true;
        });

        Assert.Equal(AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA, result);
        Assert.Equal([AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA], checkedTypes);
    }

    [Fact]
    public void SelectDeviceType_D3D11VAFails_FallsBackToDXVA2()
    {
        var result = HardwareDecoderProbe.SelectDeviceType(
            Candidates,
            t => t == AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2);

        Assert.Equal(AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2, result);
    }

    [Fact]
    public void SelectDeviceType_NoDeviceAvailable_ReturnsNone()
    {
        var checkedTypes = new List<AVHWDeviceType>();

        var result = HardwareDecoderProbe.SelectDeviceType(Candidates, t =>
        {
            checkedTypes.Add(t);
            return false;
        });

        Assert.Equal(AVHWDeviceType.AV_HWDEVICE_TYPE_NONE, result);
        Assert.Equal(Candidates, checkedTypes);
    }

    [Theory]
    [InlineData(AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA, "d3d11va")]
    [InlineData(AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2, "dxva2")]
    [InlineData(AVHWDeviceType.AV_HWDEVICE_TYPE_NONE, "none")]
    public void GetDisplayName_ReturnsFFmpegStyleName(AVHWDeviceType deviceType, string expected)
    {
        Assert.Equal(expected, HardwareDecoderProbe.GetDisplayName(deviceType));
    }
}
