using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace OnlyM.CoreSys.Tests;

public class GraphicsUtilsTests : IDisposable
{
    private readonly string _tempFolder;

    public GraphicsUtilsTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "OnlyMGraphicsUtilsTests");
        Directory.CreateDirectory(_tempFolder);
    }

    [Fact]
    public void ByteArrayToImage_WithNull_ReturnsNull()
    {
        // Act
        var result = GraphicsUtils.ByteArrayToImage(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ByteArrayToImage_WithValidImageData_ReturnsImage()
    {
        // Arrange
        var imageBytes = CreateSimpleJpegImage();

        // Act
        var result = GraphicsUtils.ByteArrayToImage(imageBytes);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.PixelWidth > 0);
        Assert.True(result.PixelHeight > 0);
        Assert.True(result.IsFrozen); // should be frozen for thread safety
    }

    [Fact]
    public void ByteArrayToImage_WithInvalidData_ThrowsException()
    {
        // Arrange
        var invalidBytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => GraphicsUtils.ByteArrayToImage(invalidBytes));
    }

    [Fact]
    public void BitmapToBitmapImage_ValidBitmap_ReturnsImage()
    {
        // Arrange
        using var bitmap = new Bitmap(100, 100);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Red);

        // Act
        var result = GraphicsUtils.BitmapToBitmapImage(bitmap);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.PixelWidth > 0);
        Assert.True(result.PixelHeight > 0);
    }

    [Fact]
    public void ImageSourceToJpegBytes_WithNull_ReturnsNull()
    {
        // Act
        var result = GraphicsUtils.ImageSourceToJpegBytes(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ImageSourceToJpegBytes_WithValidImage_ReturnsBytes()
    {
        // Arrange
        var imageBytes = CreateSimpleJpegImage();
        var bitmapImage = GraphicsUtils.ByteArrayToImage(imageBytes);

        // Act
        var result = GraphicsUtils.ImageSourceToJpegBytes(bitmapImage);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void ImageSourceToBytes_WithPngEncoder_ReturnsBytes()
    {
        // Arrange
        var imageBytes = CreateSimpleJpegImage();
        var bitmapImage = GraphicsUtils.ByteArrayToImage(imageBytes);
        var encoder = new PngBitmapEncoder();

        // Act
        var result = GraphicsUtils.ImageSourceToBytes(encoder, bitmapImage);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void Downsize_WithNull_ReturnsNull()
    {
        // Act
        var result = GraphicsUtils.Downsize(null, 100, 100);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Downsize_WithImageSmallerThanMax_ReturnsSameImage()
    {
        // Arrange
        var imageBytes = CreateSimpleJpegImage(width: 50, height: 50);
        var image = GraphicsUtils.ByteArrayToImage(imageBytes);

        // Act
        var result = GraphicsUtils.Downsize(image, 200, 200);

        // Assert
        Assert.Same(image, result);
    }

    [Fact]
    public void Downsize_WithImageLargerThanMax_ReturnsDownsizedImage()
    {
        // Arrange
        var imageBytes = CreateSimpleJpegImage(width: 400, height: 400);
        var image = GraphicsUtils.ByteArrayToImage(imageBytes);

        // Act
        var result = GraphicsUtils.Downsize(image, 200, 200);

        // Assert
        Assert.NotNull(result);
        Assert.NotSame(image, result);
        Assert.True(result.PixelWidth <= 200);
        Assert.True(result.PixelHeight <= 200);
    }

    [Fact]
    public void Downsize_WithWideImage_MaintainsAspectRatio()
    {
        // Arrange
        var imageBytes = CreateSimpleJpegImage(width: 400, height: 200);
        var image = GraphicsUtils.ByteArrayToImage(imageBytes);

        // Act
        var result = GraphicsUtils.Downsize(image, 200, 200);

        // Assert
        Assert.NotNull(result);
        var aspectRatio = (double)result.PixelWidth / result.PixelHeight;
        Assert.InRange(aspectRatio, 1.9, 2.1); // ~2.0 with some tolerance
    }

    [Fact]
    public void Downsize_WithTallImage_MaintainsAspectRatio()
    {
        // Arrange
        var imageBytes = CreateSimpleJpegImage(width: 200, height: 400);
        var image = GraphicsUtils.ByteArrayToImage(imageBytes);

        // Act
        var result = GraphicsUtils.Downsize(image, 200, 200);

        // Assert
        Assert.NotNull(result);
        var aspectRatio = (double)result.PixelWidth / result.PixelHeight;
        Assert.InRange(aspectRatio, 0.45, 0.55); // ~0.5 with some tolerance
    }

    [Fact]
    public void CreateThumbnailOfImage_WithBitmapImage_ReturnsThumbnail()
    {
        // Arrange
        var imageBytes = CreateSimpleJpegImage(width: 400, height: 400);
        var srcBmp = GraphicsUtils.ByteArrayToImage(imageBytes);

        // Act
        var result = GraphicsUtils.CreateThumbnailOfImage(srcBmp!, 100);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void CreateThumbnailOfImage_WithBitmapImageAlreadySmall_ReturnsImage()
    {
        // Arrange
        var imageBytes = CreateSimpleJpegImage(width: 50, height: 50);
        var srcBmp = GraphicsUtils.ByteArrayToImage(imageBytes);

        // Act
        var result = GraphicsUtils.CreateThumbnailOfImage(srcBmp!, 100);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void CreateThumbnailOfImage_WithNonExistentFile_ReturnsNull()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempFolder, "nonexistent.jpg");

        // Act
        var result = GraphicsUtils.CreateThumbnailOfImage(nonExistentPath, 100);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CreateThumbnailOfImage_WithValidJpegFile_ReturnsThumbnail()
    {
        // Arrange
        var imagePath = CreateTempImageFile("test.jpg", 400, 400);

        // Act
        var result = GraphicsUtils.CreateThumbnailOfImage(imagePath, 100);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void GetBitmapImage_WithValidJpegFile_ReturnsImage()
    {
        // Arrange
        var imagePath = CreateTempImageFile("test.jpg", 200, 200);

        // Act
        var result = GraphicsUtils.GetBitmapImage(imagePath, ignoreInternalCache: false);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.PixelWidth > 0);
        Assert.True(result.PixelHeight > 0);
    }

    [Fact]
    public void GetBitmapImage_WithIgnoreCache_ReturnsImage()
    {
        // Arrange
        var imagePath = CreateTempImageFile("test_cache.jpg", 200, 200);

        // Act
        var result = GraphicsUtils.GetBitmapImage(imagePath, ignoreInternalCache: true);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.PixelWidth > 0);
    }

    [Fact]
    public void AutoRotateIfRequired_WithNullPath_ReturnsFalse()
    {
        // Act
        var result = GraphicsUtils.AutoRotateIfRequired(null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AutoRotateIfRequired_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempFolder, "nonexistent.jpg");

        // Act
        var result = GraphicsUtils.AutoRotateIfRequired(nonExistentPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetRawImageAutoRotatedAndResized_WithNonExistentFile_ReturnsNull()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempFolder, "nonexistent.jpg");

        // Act
        var result = GraphicsUtils.GetRawImageAutoRotatedAndResized(nonExistentPath, 100, 100);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetRawImageAutoRotatedAndResized_WithValidJpeg_ReturnsBytes()
    {
        // Arrange
        var imagePath = CreateTempImageFile("resize_test.jpg", 400, 400);

        // Act
        var result = GraphicsUtils.GetRawImageAutoRotatedAndResized(imagePath, 200, 200);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void GetImageAutoRotatedAndResized_WithValidJpeg_ReturnsImage()
    {
        // Arrange
        var imagePath = CreateTempImageFile("resize_image_test.jpg", 400, 400);

        // Act
        var result = GraphicsUtils.GetImageAutoRotatedAndResized(imagePath, 200, 200);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.PixelWidth <= 200);
        Assert.True(result.PixelHeight <= 200);
    }

    [Fact]
    public void Downsize_WithFilePath_AndIgnoreCache_ReturnsImage()
    {
        // Arrange
        var imagePath = CreateTempImageFile("downsize_file_test.jpg", 400, 400);

        // Act
        var result = GraphicsUtils.Downsize(imagePath, 200, 200, ignoreInternalCache: true);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.PixelWidth <= 200);
        Assert.True(result.PixelHeight <= 200);
    }

    [Fact]
    public void Downsize_WithFilePath_AndUseCache_ReturnsImage()
    {
        // Arrange
        var imagePath = CreateTempImageFile("downsize_cache_test.jpg", 400, 400);

        // Act
        var result = GraphicsUtils.Downsize(imagePath, 200, 200, ignoreInternalCache: false);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void HeicCodecMissingDetected_EventExists()
    {
        // Assert
        var eventInfo = typeof(GraphicsUtils).GetEvent("HeicCodecMissingDetected");
        Assert.NotNull(eventInfo);
    }

    [Fact]
    public void GenerateSubtitleFile_WithEmptyFfmpegFolder_ReturnsFalse()
    {
        // Arrange
        var videoPath = CreateTempImageFile("video.mp4", 100, 100);
        var srtPath = Path.Combine(_tempFolder, "output.srt");

        // Act
        var result = GraphicsUtils.GenerateSubtitleFile(string.Empty, videoPath, srtPath);

        // Assert
        Assert.False(result);
    }

    private static byte[] CreateSimpleJpegImage(int width = 100, int height = 100)
    {
        using var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Blue);

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Jpeg);
        return ms.ToArray();
    }

    private string CreateTempImageFile(string filename, int width, int height)
    {
        var path = Path.Combine(_tempFolder, filename);
        var imageBytes = CreateSimpleJpegImage(width, height);
        File.WriteAllBytes(path, imageBytes);
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempFolder))
            {
                Directory.Delete(_tempFolder, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
