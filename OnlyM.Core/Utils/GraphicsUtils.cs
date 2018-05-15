namespace OnlyM.Core.Utils
{
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Text;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Serilog;

    public static class GraphicsUtils
    {
        public static BitmapSource Downsize(string imageFilePath, int maxImageWidth, int maxImageHeight)
        {
            var image = new BitmapImage(new Uri(imageFilePath));

            var factorWidth = maxImageWidth / image.Width;
            var factorHeight = maxImageHeight / image.Height;

            if (factorHeight >= 1 && factorWidth >= 1)
            {
                return image;
            }

            var factor = Math.Min(factorWidth, factorHeight);

            var t = new TransformedBitmap(image, new ScaleTransform(factor, factor));
            return t;
        }

        public static byte[] CreateThumbnailOfImage(string path, int maxPixelDimension, ImageFormat imageFormat)
        {
            byte[] result = null;

            if (File.Exists(path))
            {
                using (var srcBmp = new Bitmap(path))
                {
                    SizeF newSize = srcBmp.Width > srcBmp.Height
                        ? new SizeF(maxPixelDimension, maxPixelDimension * (float)srcBmp.Height / srcBmp.Width)
                        : new SizeF(maxPixelDimension * (float)srcBmp.Width / srcBmp.Height, maxPixelDimension);

                    using (var target = new Bitmap((int)newSize.Width, (int)newSize.Height))
                    {
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
                }
            }

            return result;
        }

        public static ImageSource ByteArrayToImage(byte[] imageData)
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

        /// <summary>
        /// Creates the thumbnail for the specified video.
        /// </summary>
        /// <param name="originalPath">The original vidoe path.</param>
        /// <param name="ffmpegFolder">The ffmpeg installation folder.</param>
        /// <returns>The temporary thumbnail image file.</returns>
        public static string CreateThumbnailForVideo(
            string originalPath, 
            string ffmpegFolder)
        {
            try
            {
                return CreateNativeThumbnailForVideo(originalPath, ffmpegFolder);
            }
            catch (Exception)
            {
                try
                {
                    return CreateFFMpegThumbnailForVideo(originalPath, ffmpegFolder);
                }
                catch (Exception ex)
                {
                    Log.Logger.Error(ex, $"Could not create thumbnail for video: {originalPath}");
                }
            }

            return null;
        }

        // ReSharper disable once InconsistentNaming
        private static string CreateFFMpegThumbnailForVideo(string originalPath, string ffmpegFolder)
        {
            var tempThumbnailPath = GetTempVideoThumbnailFileName(originalPath);

            // use the ffmpeg "thumbnail" argument.
            var arguments = new StringBuilder();

            arguments.Append("-i ");
            arguments.Append("\"");
            arguments.Append(originalPath);
            arguments.Append("\" ");
            arguments.Append("-vf \"thumbnail,scale=640:360\" -y -frames:v 1 ");
            arguments.Append("\"");
            arguments.Append(tempThumbnailPath);
            arguments.Append("\" ");

            return ExecuteFFMpeg(ffmpegFolder, arguments.ToString())
                ? tempThumbnailPath
                : null;
        }

        private static string CreateNativeThumbnailForVideo(string originalPath, string ffmpegFolder)
        {
            var tempThumbnailPath = GetTempVideoThumbnailFileName(originalPath);

            // get the internal thumbnail provided in the video itself.
            var arguments = new StringBuilder();
            arguments.Append("-i ");
            arguments.Append("\"");
            arguments.Append(originalPath);
            arguments.Append("\" ");
            arguments.Append("-map 0 -map -V -map -d -map -s -map -t -map -a -y ");
            arguments.Append("\"");
            arguments.Append(tempThumbnailPath);
            arguments.Append("\" ");

            return ExecuteFFMpeg(ffmpegFolder, arguments.ToString()) 
                ? tempThumbnailPath 
                : null;
        }


        // ReSharper disable once InconsistentNaming
        private static bool ExecuteFFMpeg(string ffmpegFolder, string arguments)
        {
            var ffmpegPath = Path.Combine(ffmpegFolder, "ffmpeg.exe");

            var p = new Process
            {
                StartInfo =
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            p.Start();
            return p.WaitForExit(5000);
        }

        private static string GetTempVideoThumbnailFileName(string originalFilePath)
        {
            string tempThumbnailFolder = Path.Combine(FileUtils.GetSystemTempFolder(), "OnlyM", "TempThumbs");
            FileUtils.CreateDirectory(tempThumbnailFolder);

            var origFileName = Path.GetFileName(originalFilePath);
            if (string.IsNullOrEmpty(origFileName))
            {
                return null;
            }

            return Path.Combine(tempThumbnailFolder, Path.ChangeExtension(origFileName, ".png"));
        }

        private static BitmapSource Convert(Bitmap bmp)
        {
            var bitmapData = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly, 
                bmp.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, 
                bitmapData.Height, 
                96, 
                96, 
                PixelFormats.Bgr24, 
                null,
                bitmapData.Scan0, 
                bitmapData.Stride * bitmapData.Height, 
                bitmapData.Stride);

            bmp.UnlockBits(bitmapData);

            return bitmapSource;
        }
    }
}
