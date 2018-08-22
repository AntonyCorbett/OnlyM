namespace OnlyM.Core.Utils
{
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Serilog;

    public static class GraphicsUtils
    {
        private const int ExifOrientationId = 0x112; // 274

        // todo: make optional use of this (but RotateFlip seems to corrupt meta data!)

        public static bool ExifRotate(this Image img)
        {
            if (!img.PropertyIdList.Contains(ExifOrientationId))
            {
                return false;
            }
            
            var prop = img.GetPropertyItem(ExifOrientationId);
            int val = BitConverter.ToUInt16(prop.Value, 0);
            var rot = RotateFlipType.RotateNoneFlipNone;

            switch (val)
            {
                case 3:
                case 4:
                    rot = RotateFlipType.Rotate180FlipNone;
                    break;

                case 5:
                case 6:
                    rot = RotateFlipType.Rotate90FlipNone;
                    break;

                case 7:
                case 8:
                    rot = RotateFlipType.Rotate270FlipNone;
                    break;
            }

            if (val == 2 || val == 4 || val == 5 || val == 7)
            {
                rot |= RotateFlipType.RotateNoneFlipX;
            }

            if (rot != RotateFlipType.RotateNoneFlipNone)
            {
                img.RotateFlip(rot);
                return true;
            }

            return false;
        }

        public static BitmapSource Downsize(string imageFilePath, int maxImageWidth, int maxImageHeight)
        {
            var image = GetBitmapImageWithCacheOnLoad(imageFilePath);
                        
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

        public static BitmapImage GetBitmapImageWithCacheOnLoad(string imageFile)
        {
            var bmp = new BitmapImage();

            // BitmapCacheOption.OnLoad prevents the source image file remaining
            // in use when the bitmap is used as an ImageSource.
            bmp.BeginInit();
            bmp.UriSource = new Uri(imageFile);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();

            return bmp;
        }

        /// <summary>
        /// Creates the thumbnail for the specified video.
        /// </summary>
        /// <param name="originalPath">The original vidoe path.</param>
        /// <param name="ffmpegFolder">The ffmpeg installation folder.</param>
        /// <param name="useEmbeddedWhereAvailable">Use an embedded thumbnail if available.</param>
        /// <returns>The temporary thumbnail image file.</returns>
        public static string CreateThumbnailForVideo(
            string originalPath, 
            string ffmpegFolder,
            bool useEmbeddedWhereAvailable)
        {
            if (useEmbeddedWhereAvailable)
            {
                try
                {
                    var result = CreateEmbeddedThumbnailForVideo(originalPath, ffmpegFolder);
                    if (result != null && File.Exists(result))
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
                return CreateFFMpegThumbnailForVideo(originalPath, ffmpegFolder);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Could not create thumbnail for video: {originalPath}");
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
            arguments.Append("-vf \"thumbnail,scale=320:180\" -y -frames:v 1 ");
            arguments.Append("\"");
            arguments.Append(tempThumbnailPath);
            arguments.Append("\" ");

            ExecuteFFMpeg(ffmpegFolder, arguments.ToString());

            return File.Exists(tempThumbnailPath)
                ? tempThumbnailPath
                : null;
        }

        private static string CreateEmbeddedThumbnailForVideo(string originalPath, string ffmpegFolder)
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
            var tempThumbnailFolder = Path.Combine(FileUtils.GetUsersTempFolder(), "OnlyM", "TempThumbs");
            FileUtils.CreateDirectory(tempThumbnailFolder);

            var origFileName = Path.GetFileName(originalFilePath);
            if (string.IsNullOrEmpty(origFileName))
            {
                return null;
            }

            return Path.Combine(tempThumbnailFolder, Path.ChangeExtension(origFileName, ".png"));
        }
    }
}
