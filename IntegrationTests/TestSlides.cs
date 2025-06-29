using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OnlyM.Slides;

namespace IntegrationTests;

[TestClass]
public class TestSlides
{
    [TestMethod]
    public void TestImageWithBadColorProfile()
    {
        var slideFilePath = "test" + SlideFile.FileExtension;

        var fb = new SlideFileBuilder(1920, 1080);
        fb.AddSlide(GetAbsolutePath(@"TestImages\JPGwithBadColorProfile.jpg"), true, true, true, true);

        fb.Build(slideFilePath, true);
        var file = new SlideFile(slideFilePath);
        var slide = file.GetSlide(0);
        Assert.IsNotNull(slide.Image);
    }

    [TestMethod]
    public void TestSlideCreation()
    {
        var slideFilePath = "test" + SlideFile.FileExtension;

        var fb = new SlideFileBuilder(1920, 1080);
        fb.AddSlide(GetAbsolutePath(@"TestImages\001.jpg"), true, true, true, true);
        fb.AddSlide(GetAbsolutePath(@"TestImages\002.jpg"), true, true, true, true);
        fb.AddSlide(GetAbsolutePath(@"TestImages\003.jpg"), true, true, true, true);

        fb.AddSlide(GetAbsolutePath(@"TestImages\001.jpg"), false, false, false, false);
        fb.AddSlide(GetAbsolutePath(@"TestImages\002.jpg"), false, false, false, false);
        fb.AddSlide(GetAbsolutePath(@"TestImages\003.jpg"), false, false, false, false);

        fb.Loop = true;
        fb.AutoPlay = true;
        fb.DwellTimeMilliseconds = 5000;

        fb.Build(slideFilePath, true);

        var file = new SlideFile(slideFilePath);

        for (var n = 0; n < file.SlideCount; ++n)
        {
            var slide = file.GetSlide(n);

            var encoder = new PngBitmapEncoder();
            Assert.IsNotNull(slide.Image);
            encoder.Frames.Add(BitmapFrame.Create(slide.Image));

            Assert.IsNotNull(slide.ArchiveEntryName);
            using (var fileStream = new FileStream(slide.ArchiveEntryName, FileMode.Create))
            {
                encoder.Save(fileStream);
            }
        }
    }

    private static string GetAbsolutePath(string relativePath) => Path.GetFullPath(relativePath);
}
