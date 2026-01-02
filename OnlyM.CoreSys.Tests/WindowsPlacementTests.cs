using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
using OnlyM.CoreSys.WindowsPositioning;
using Size = System.Windows.Size;

namespace OnlyM.CoreSys.Tests;

public class WindowsPlacementTests
{
    [Fact]
    public void GetDpiSettings_ReturnsValidDpiValues()
    {
        // Act
        var (dpiX, dpiY) = WindowsPlacement.GetDpiSettings();

        // Assert
        Assert.True(dpiX > 0);
        Assert.True(dpiY > 0);
        Assert.True(dpiX >= 96); // Minimum DPI is typically 96
        Assert.True(dpiY >= 96);
    }

    [Fact]
    public void RECT_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var rect = new RECT(10, 20, 100, 200);

        // Assert
        Assert.Equal(10, rect.Left);
        Assert.Equal(20, rect.Top);
        Assert.Equal(100, rect.Right);
        Assert.Equal(200, rect.Bottom);
    }

    [Fact]
    public void POINT_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var point = new POINT(50, 75);

        // Assert
        Assert.Equal(50, point.X);
        Assert.Equal(75, point.Y);
    }

    [Fact]
    public void WINDOWPLACEMENT_SerializationRoundTrip_PreservesData()
    {
        // Arrange
        var originalPlacement = new WINDOWPLACEMENT
        {
            length = Marshal.SizeOf<WINDOWPLACEMENT>(),
            flags = 0,
            showCmd = 1,
            minPosition = new POINT(10, 20),
            maxPosition = new POINT(30, 40),
            normalPosition = new RECT(100, 150, 800, 600)
        };

        var serializer = new XmlSerializer(typeof(WINDOWPLACEMENT));

        // Act
        string xml;
        using (var memoryStream = new System.IO.MemoryStream())
        {
            var xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
            serializer.Serialize(xmlTextWriter, originalPlacement);
            var xmlBytes = memoryStream.ToArray();
            xml = Encoding.UTF8.GetString(xmlBytes);
        }

        WINDOWPLACEMENT deserializedPlacement;
        var xmlBytesForDeserialization = Encoding.UTF8.GetBytes(xml);
        using (var memoryStream = new System.IO.MemoryStream(xmlBytesForDeserialization))
        using (var reader = XmlReader.Create(memoryStream))
        {
            var obj = (WINDOWPLACEMENT?)serializer.Deserialize(reader);
            Assert.NotNull(obj);
            deserializedPlacement = obj.Value;
        }

        // Assert
        Assert.Equal(originalPlacement.showCmd, deserializedPlacement.showCmd);
        Assert.Equal(originalPlacement.minPosition.X, deserializedPlacement.minPosition.X);
        Assert.Equal(originalPlacement.minPosition.Y, deserializedPlacement.minPosition.Y);
        Assert.Equal(originalPlacement.maxPosition.X, deserializedPlacement.maxPosition.X);
        Assert.Equal(originalPlacement.maxPosition.Y, deserializedPlacement.maxPosition.Y);
        Assert.Equal(originalPlacement.normalPosition.Left, deserializedPlacement.normalPosition.Left);
        Assert.Equal(originalPlacement.normalPosition.Top, deserializedPlacement.normalPosition.Top);
        Assert.Equal(originalPlacement.normalPosition.Right, deserializedPlacement.normalPosition.Right);
        Assert.Equal(originalPlacement.normalPosition.Bottom, deserializedPlacement.normalPosition.Bottom);
    }

    [StaFact]
    public void SetPlacement_WithEmptyString_DoesNotThrow()
    {
        // Arrange
        var window = CreateTestWindow();

        // Act & Assert
        var exception = Record.Exception(() => window.SetPlacement(string.Empty));
        Assert.Null(exception);
    }

    [StaFact]
    public void SetPlacement_WithNull_DoesNotThrow()
    {
        // Arrange
        var window = CreateTestWindow();

        // Act & Assert
        var exception = Record.Exception(() => window.SetPlacement(null!));
        Assert.Null(exception);
    }

    [StaFact]
    public void SetPlacement_WithInvalidXml_DoesNotThrow()
    {
        // Arrange
        var window = CreateTestWindow();
        const string invalidXml = "not valid xml";

        // Act & Assert
        var exception = Record.Exception(() => window.SetPlacement(invalidXml));
        Assert.Null(exception);
    }

    [StaFact]
    public void SetPlacement_WithSizeOverride_DoesNotThrow()
    {
        // Arrange
        var window = CreateTestWindow();
        var placement = CreateValidPlacementXml();
        var sizeOverride = new Size(1024, 768);

        // Act & Assert
        var exception = Record.Exception(() => window.SetPlacement(placement, sizeOverride));
        Assert.Null(exception);
    }

    [StaFact]
    public void GetPlacement_ReturnsNonEmptyString()
    {
        // Arrange
        var window = CreateTestWindow();

        // Act
        var placement = window.GetPlacement();

        // Assert
        Assert.False(string.IsNullOrEmpty(placement));
        Assert.Contains("WINDOWPLACEMENT", placement);
    }

    [StaFact]
    public void GetPlacement_ReturnsValidXml()
    {
        // Arrange
        var window = CreateTestWindow();

        // Act
        var placement = window.GetPlacement();

        // Assert
        var exception = Record.Exception(() =>
        {
            var xmlBytes = Encoding.UTF8.GetBytes(placement);
            using var memoryStream = new System.IO.MemoryStream(xmlBytes);
            using var reader = XmlReader.Create(memoryStream);
            var serializer = new XmlSerializer(typeof(WINDOWPLACEMENT));
            var obj = serializer.Deserialize(reader);
            Assert.NotNull(obj);
        });

        Assert.Null(exception);
    }


    [Fact]
    public void POINT_StructLayout_IsSequential()
    {
        // Arrange & Act
        var structLayoutAttr = typeof(POINT).StructLayoutAttribute;

        // Assert
        Assert.NotNull(structLayoutAttr);
        Assert.Equal(LayoutKind.Sequential, structLayoutAttr.Value);
    }

    [Fact]
    public void WINDOWPLACEMENT_StructLayout_IsSequential()
    {
        // Arrange & Act
        var structLayoutAttr = typeof(WINDOWPLACEMENT).StructLayoutAttribute;

        // Assert
        Assert.NotNull(structLayoutAttr);
        Assert.Equal(LayoutKind.Sequential, structLayoutAttr.Value);
    }

    [Fact]
    public void WINDOWPLACEMENT_Size_IsCorrect()
    {
        // Arrange & Act
        var size = Marshal.SizeOf<WINDOWPLACEMENT>();

        // Assert
        // WINDOWPLACEMENT should be 44 bytes on 32-bit and 44 bytes on 64-bit
        Assert.Equal(44, size);
    }

    private static Window CreateTestWindow()
    {
        var window = new Window
        {
            Width = 800,
            Height = 600,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            Visibility = Visibility.Hidden
        };

        // Ensure window has a handle
        window.Show();
        window.Hide();

        return window;
    }

    private static string CreateValidPlacementXml()
    {
        var placement = new WINDOWPLACEMENT
        {
            length = Marshal.SizeOf<WINDOWPLACEMENT>(),
            flags = 0,
            showCmd = 1,
            minPosition = new POINT(-1, -1),
            maxPosition = new POINT(-1, -1),
            normalPosition = new RECT(100, 100, 900, 700)
        };

        var serializer = new XmlSerializer(typeof(WINDOWPLACEMENT));
        using var memoryStream = new System.IO.MemoryStream();
        var xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
        serializer.Serialize(xmlTextWriter, placement);
        var xmlBytes = memoryStream.ToArray();
        return Encoding.UTF8.GetString(xmlBytes);
    }
}
