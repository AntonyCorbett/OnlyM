using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoSauce.MagicScaler;
using Serilog;
using TagLib.Image;

namespace OnlyM.CoreSys
{
    public static class GraphicsUtils
    {
        private const int MaxDpi = 1200;
        private static readonly object TagLibLocker = new();

        public static bool AutoRotateIfRequired(string? itemFilePath)
        {
            if (itemFilePath == null)
            {
                return false;
            }

            try
            {
                if (ImageRequiresRotation(itemFilePath))
                {
                    var settings = new ProcessImageSettings { OrientationMode = OrientationMode.Normalize };

                    using var outStream = new MemoryStream();
                    MagicImageProcessor.ProcessImage(itemFilePath, outStream, settings);
                    outStream.Seek(0L, SeekOrigin.Begin);
                    System.IO.File.WriteAllBytes(itemFilePath, outStream.ToArray());
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Could not auto-rotate image {itemFilePath}");
            }

            return false;
        }

        public static BitmapSource? GetImageAutoRotatedAndResized(
            string itemFilePath, int width, int height, bool shouldPad)
        {
            var bytes = GetRawImageAutoRotatedAndResized(itemFilePath, width, height, shouldPad);
            if (bytes == null)
            {
                return null;
            }

            return ByteArrayToImage(bytes);
        }

        public static byte[]? GetRawImageAutoRotatedAndResized(
            string itemFilePath, int width, int height, bool shouldPad)
        {
            try
            {
                AutoRotateIfRequired(itemFilePath);

                var settings = new ProcessImageSettings { Width = width, Height = height };
                if (shouldPad)
                {
                    settings.ResizeMode = CropScaleMode.Pad;
                    settings.MatteColor = System.Drawing.Color.Black;
                }

                using var outStream = new MemoryStream();
                MagicImageProcessor.ProcessImage(itemFilePath, outStream, settings);
                outStream.Seek(0L, SeekOrigin.Begin);
                return outStream.ToArray();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Could not auto-rotate and resize image {itemFilePath}");
            }

            return null;
        }

        public static BitmapSource Downsize(string imageFilePath, int maxImageWidth, int maxImageHeight)
        {
            var image = GetBitmapImageWithCacheOnLoad(imageFilePath);
            return Downsize(image, maxImageWidth, maxImageHeight);
        }

        public static BitmapSource Downsize(
            BitmapSource image, 
            int maxImageWidth, 
            int maxImageHeight)
        {
            var factorWidth = (double)maxImageWidth / image.PixelWidth;
            var factorHeight = (double)maxImageHeight / image.PixelHeight;

            if (factorHeight >= 1 && factorWidth >= 1)
            {
                return image;
            }

            var factor = Math.Min(factorWidth, factorHeight);

            return new TransformedBitmap(image, new ScaleTransform(factor, factor));
        }

        public static byte[]? CreateThumbnailOfImage(string path, int maxPixelDimension, ImageFormat imageFormat)
        {
            if (!System.IO.File.Exists(path))
            {
                return null;
            }

            byte[] result;

            using (var srcBmp = new Bitmap(path))
            {
                var newSize = srcBmp.Width > srcBmp.Height
                    ? new SizeF(maxPixelDimension, maxPixelDimension * (float)srcBmp.Height / srcBmp.Width)
                    : new SizeF(maxPixelDimension * (float)srcBmp.Width / srcBmp.Height, maxPixelDimension);

                using (var target = new Bitmap((int)newSize.Width, (int)newSize.Height))
                using (var graphics = Graphics.FromImage(target))
                {
                    graphics.CompositingQuality = CompositingQuality.HighSpeed;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.DrawImage(srcBmp, 0, 0, newSize.Width, newSize.Height);
                    using (var memoryStream = new MemoryStream())
                    {
                        target.Save(memoryStream, imageFormat);
                        result = memoryStream.ToArray();
                    }
                }
            }

            return result;
        }

        public static byte[]? ImageSourceToJpegBytes(ImageSource? imageSource) => ImageSourceToBytes(new JpegBitmapEncoder(), imageSource);
        
        public static byte[]? ImageSourceToBytes(BitmapEncoder encoder, ImageSource? imageSource)
        {
            byte[]? bytes = null;

            if (imageSource is BitmapSource bitmapSource)
            {
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    bytes = stream.ToArray();
                }
            }

            return bytes;
        }

        public static byte[] CreateThumbnailOfImage(BitmapImage srcBmp, int maxPixelDimension)
        {
            var factorWidth = (double)maxPixelDimension / srcBmp.PixelWidth;
            var factorHeight = (double)maxPixelDimension / srcBmp.PixelHeight;

            if (factorHeight >= 1 && factorWidth >= 1)
            {
                using (var memoryStream = new MemoryStream())
                {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(srcBmp));

                    encoder.Save(memoryStream);
                    return memoryStream.ToArray();
                }
            }

            var factor = Math.Min(factorWidth, factorHeight);

            var t = new TransformedBitmap(srcBmp, new ScaleTransform(factor, factor));

            using (var memoryStream = new MemoryStream())
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(t));

                encoder.Save(memoryStream);
                return memoryStream.ToArray();
            }
        }

        public static BitmapImage BitmapToBitmapImage(Bitmap src)
        {
            var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Bmp);
            var image = new BitmapImage();
            image.BeginInit();
            ms.Seek(0, SeekOrigin.Begin);
            image.StreamSource = ms;
            image.EndInit();
            return image;
        }

        public static BitmapImage? ByteArrayToImage(byte[]? imageData)
        {
            if (imageData == null)
            {
                return null;
            }

            var img = new BitmapImage();
            var ms = new MemoryStream(imageData);
            
            img.BeginInit();
            img.StreamSource = ms;
            img.EndInit();
            
            return img;
        }

        public static BitmapImage GetBitmapImageWithCacheOnLoad(string imageFile)
        {
            BitmapImage bmp;

            try
            {
                bmp = InternalGetBitmapImageWithCacheOnLoad(imageFile, ignoreColorProfile: false);
            }
            catch (ArgumentException)
            {
                // probably colour profile corruption
                bmp = InternalGetBitmapImageWithCacheOnLoad(imageFile, ignoreColorProfile: true);
            }

            if (IsBadDpi(bmp))
            {
                // NB - if the DpiX and DpiY metadata is bad then the bitmap can't be displayed
                // correctly, so fix it here...
                return FixBadDpi(imageFile);
            }

            return bmp;
        }

        /// <summary>
        /// Creates the thumbnail for the specified video.
        /// </summary>
        /// <param name="originalPath">The original video path.</param>
        /// <param name="ffmpegFolder">The ffmpeg installation folder.</param>
        /// <param name="tempFolder">Temp folder for thumbnail images.</param>
        /// <param name="useEmbeddedWhereAvailable">Use an embedded thumbnail if available.</param>
        /// <returns>The temporary thumbnail image file.</returns>
        public static string? CreateThumbnailForVideo(
            string originalPath, 
            string ffmpegFolder,
            string tempFolder,
            bool useEmbeddedWhereAvailable)
        {
            if (useEmbeddedWhereAvailable)
            {
                try
                {
                    var result = CreateEmbeddedThumbnailForVideo(originalPath, ffmpegFolder, tempFolder);
                    if (result != null && System.IO.File.Exists(result))
                    {
                        return result;
                    }

                    Log.Logger.Debug($"Embedded thumbnail unavailable for video: {originalPath}");
                }
                catch (Exception ex)
                {
                    Log.Logger.Error(ex, $"Embedded thumbnail unavailable for video: {originalPath}");
                }
            }

            try
            {
                return CreateFFMpegThumbnailForVideo(originalPath, ffmpegFolder, tempFolder);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Could not create thumbnail for video: {originalPath}");
            }

            return null;
        }

        public static bool GenerateSubtitleFile(string ffmpegFolder, string videoFilePath, string destinationSrtFilePath)
        {
            try
            {
                var arguments = new StringBuilder();

                arguments.Append("-i ");
                arguments.Append('\"');
                arguments.Append(videoFilePath);
                arguments.Append("\" ");
                arguments.Append("-map 0:s:0 ");
                arguments.Append('\"');
                arguments.Append(destinationSrtFilePath);
                arguments.Append("\" -y");

                ExecuteFFMpeg(ffmpegFolder, arguments.ToString());

                return System.IO.File.Exists(destinationSrtFilePath);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Could not create subtitle file for video: {videoFilePath}");
            }

            return false;
        }

        // ReSharper disable once InconsistentNaming
        private static string? CreateFFMpegThumbnailForVideo(
            string originalPath, string ffmpegFolder, string tempFolder)
        {
            var tempThumbnailPath = GetTempVideoThumbnailFileName(originalPath, tempFolder);

            // use the ffmpeg "thumbnail" argument.
            var arguments = new StringBuilder();

            arguments.Append("-i ");
            arguments.Append('\"');
            arguments.Append(originalPath);
            arguments.Append("\" ");
            arguments.Append("-vf \"thumbnail,scale=320:180\" -y -frames:v 1 ");
            arguments.Append('\"');
            arguments.Append(tempThumbnailPath);
            arguments.Append("\" ");

            ExecuteFFMpeg(ffmpegFolder, arguments.ToString());

            return System.IO.File.Exists(tempThumbnailPath)
                ? tempThumbnailPath
                : null;
        }

        private static string? CreateEmbeddedThumbnailForVideo(
            string originalPath, string ffmpegFolder, string tempFolder)
        {
            var tempThumbnailPath = GetTempVideoThumbnailFileName(originalPath, tempFolder);

            // get the internal thumbnail provided in the video itself.
            var arguments = new StringBuilder();
            arguments.Append("-i ");
            arguments.Append('\"');
            arguments.Append(originalPath);
            arguments.Append("\" ");
            arguments.Append("-map 0 -map -V -map -d -map -s -map -t -map -a -y ");
            arguments.Append('\"');
            arguments.Append(tempThumbnailPath);
            arguments.Append("\" ");

            return ExecuteFFMpeg(ffmpegFolder, arguments.ToString()) 
                ? tempThumbnailPath 
                : null;
        }

        // ReSharper disable once InconsistentNaming
        private static bool ExecuteFFMpeg(string ffmpegFolder, string arguments)
        {
            Log.Logger.Debug($"Executing ffmpeg with args = {arguments}");

            var ffmpegPath = Path.Combine(ffmpegFolder, "ffmpeg.exe");

            var p = new Process
            {
                StartInfo =
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            using (p)
            {
                p.Start();

                p.BeginOutputReadLine();
                p.StandardError.ReadToEnd();

                var rv = p.WaitForExit(5000);
                if (!p.HasExited)
                {
                    p.Kill();
                }

                Log.Logger.Debug($"Ffmpeg return code = {rv}");

                return rv;
            }
        }

        private static string? GetTempVideoThumbnailFileName(string originalFilePath, string tempThumbnailFolder)
        {
            var origFileName = Path.GetFileName(originalFilePath);
            if (string.IsNullOrEmpty(origFileName))
            {
                return null;
            }

            return Path.Combine(tempThumbnailFolder, Path.ChangeExtension(origFileName, ".png"));
        }
        
        private static bool ImageRequiresRotation(string imageFilePath)
        {
            try
            {
                // The TagLib call below is not thread-safe
                lock (TagLibLocker)
                {
                    using (var tf = TagLib.File.Create(imageFilePath))
                    {
                        tf.Mode = TagLib.File.AccessMode.Read;

                        using (var imageFile = tf as TagLib.Image.File)
                        {
                            if (imageFile != null)
                            {
                                // see here for Exif discussion:
                                // http://sylvana.net/jpegcrop/exif_orientation.html
                                // https://www.daveperrett.com/articles/2012/07/28/exif-orientation-handling-is-a-ghetto/
                                var orientation = imageFile.ImageTag.Orientation;

                                return orientation != ImageOrientation.None &&
                                       orientation != ImageOrientation.TopLeft;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not determine image orientation");
            }

            return false;
        }

        private static bool IsBadDpi(BitmapImage bmp) => bmp.DpiX > MaxDpi || bmp.DpiY > MaxDpi;
        
        private static BitmapImage FixBadDpi(string imageFile)
        {
            var settings = new ProcessImageSettings { DpiX = 96, DpiY = 96 };

            using var outStream = new MemoryStream();
            MagicImageProcessor.ProcessImage(imageFile, outStream, settings);
            outStream.Seek(0L, SeekOrigin.Begin);
            System.IO.File.WriteAllBytes(imageFile, outStream.ToArray());

            outStream.Seek(0L, SeekOrigin.Begin);

            var bitmapImage = new BitmapImage();

            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = outStream;
            bitmapImage.EndInit();

            return bitmapImage;
        }

        private static BitmapImage InternalGetBitmapImageWithCacheOnLoad(string imageFile, bool ignoreColorProfile)
        {
            var bmp = new BitmapImage();

            // BitmapCacheOption.OnLoad prevents the source image file remaining
            // in use when the bitmap is used as an ImageSource.
            bmp.BeginInit();
            bmp.CreateOptions = ignoreColorProfile ? BitmapCreateOptions.IgnoreColorProfile : BitmapCreateOptions.None;
            bmp.UriSource = new Uri(imageFile);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();

            return bmp;
        }
    }
}
