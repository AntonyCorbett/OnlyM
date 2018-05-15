using OnlyM.Core.Models;

namespace IntegrationTests
{
    using System;
    using System.Drawing.Imaging;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using OnlyM.Core.Services.Media;
    using OnlyM.Core.Utils;

    [TestClass]
    public class TestThumbnails
    {
        [TestMethod]
        public void TestMethod1()
        {
            IThumbnailService service = new ThumbnailService();
            service.ClearCache();

            var folder = Path.Combine(FileUtils.GetSystemTempFolder(), "OnlyMIntegrationTests");
            FileUtils.CreateDirectory(folder);

            var filePath = Path.Combine(folder, "TestImage01.jpg");

            var bmp = Properties.Resources.Test01;
            bmp.Save(filePath, ImageFormat.Jpeg);

            DateTime lastWrite = File.GetLastWriteTimeUtc(filePath);

            var thumb1 = service.GetThumbnail(filePath, null, MediaClassification.Image, lastWrite.Ticks, out var foundInCache);

            Assert.IsNotNull(thumb1);
            Assert.IsFalse(foundInCache);

            // try again to check we get cached version
            var thumb2 = service.GetThumbnail(filePath, null, MediaClassification.Image, lastWrite.Ticks, out foundInCache);

            Assert.IsNotNull(thumb2);
            Assert.IsTrue(foundInCache);

            // now send wrong lastchanged value
            var thumb3 = service.GetThumbnail(filePath, null, MediaClassification.Image, 123456, out foundInCache);
            Assert.IsNotNull(thumb3);
            Assert.IsFalse(foundInCache);

            service.ClearCache();
        }
    }
}
