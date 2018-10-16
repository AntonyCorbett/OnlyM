namespace IntegrationTests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using OnlyM.Core.Subtitles;

    [TestClass]
    public class TestSubtitles
    {
        [TestMethod]
        public void TestSrtProcess()
        {
            var subtitleProvider = new SubtitleProvider(@"SubtitleFiles\sample.srt", TimeSpan.Zero);
            Assert.IsTrue(subtitleProvider.Count == 5);
        }
    }
}
