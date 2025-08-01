﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OnlyM.Core.Subtitles;

namespace IntegrationTests;

[TestClass]
public class TestSubtitles
{
    [TestMethod]
    public void TestSrtProcess()
    {
        var subtitleProvider = new SubtitleProvider(@"SubtitleFiles\sample.srt", TimeSpan.Zero);
        Assert.AreEqual(5, subtitleProvider.Count);
    }
}
