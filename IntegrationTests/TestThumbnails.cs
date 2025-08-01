﻿using System.Drawing.Imaging;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OnlyM.Core.Models;
using OnlyM.Core.Services.CommandLine;
using OnlyM.Core.Services.Database;
using OnlyM.Core.Services.Media;
using OnlyM.Core.Services.Options;
using OnlyM.Core.Utils;

namespace IntegrationTests;

[TestClass]
public class TestThumbnails
{
    [TestMethod]
    public void TestMethod1()
    {
        var logSwitchService = new LogLevelSwitchService();
        var commandLineService = new CommandLineService();
        var db = new DatabaseService();
        var optionsService = new OptionsService(logSwitchService, commandLineService);
        var service = new ThumbnailService(db, optionsService);
        service.ClearThumbCache();

        var folder = Path.Combine(FileUtils.GetUsersTempFolder(), "OnlyMIntegrationTests");
        FileUtils.CreateDirectory(folder);

        var filePath = Path.Combine(folder, "TestImage01.jpg");

        var bmp = Properties.Resources.Test01;
        bmp.Save(filePath, ImageFormat.Jpeg);

        var lastWrite = File.GetLastWriteTimeUtc(filePath);

        var dummyFfmpegFolder = string.Empty; // not needed when getting image thumbs

        var thumb1 = service.GetThumbnail(filePath, dummyFfmpegFolder, MediaClassification.Image, lastWrite.Ticks, out var foundInCache);

        Assert.IsNotNull(thumb1);
        Assert.IsFalse(foundInCache);

        // try again to check we get cached version
        var thumb2 = service.GetThumbnail(filePath, dummyFfmpegFolder, MediaClassification.Image, lastWrite.Ticks, out foundInCache);

        Assert.IsNotNull(thumb2);
        Assert.IsTrue(foundInCache);

        // now send wrong lastchanged value
        var thumb3 = service.GetThumbnail(filePath, dummyFfmpegFolder, MediaClassification.Image, 123456, out foundInCache);
        Assert.IsNotNull(thumb3);
        Assert.IsFalse(foundInCache);

        service.ClearThumbCache();
    }
}
