using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using OnlyM.Models;
using OnlyM.Windows;

namespace OnlyM.Tests;

public sealed class OperatorPageMouseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task TextElement_ResolvesMediaItemWithoutBlockingDrag(bool lineBreak) => RunOnStaThread(() =>
    {
        var item = new MediaItem();
        var text = new TextBlock { DataContext = item };
        Inline source = lineBreak ? new LineBreak() : new Run("Title");
        text.Inlines.Add(new Span(source));

        Assert.Same(item, OperatorPage.GetMediaItemFromOriginalSource(source));
        Assert.False(OperatorPage.IsDragBlockedSource(source));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task TextElement_InsideButtonStillBlocksDrag(bool lineBreak) => RunOnStaThread(() =>
    {
        var item = new MediaItem();
        var text = new TextBlock();
        Inline source = lineBreak ? new LineBreak() : new Run("Play");
        text.Inlines.Add(source);
        var button = new Button { DataContext = item, Content = text };
        button.Measure(new Size(200, 100));
        button.Arrange(new Rect(0, 0, 200, 100));

        Assert.Same(item, OperatorPage.GetMediaItemFromOriginalSource(source));
        Assert.True(OperatorPage.IsDragBlockedSource(source));
    });

    [Fact]
    public Task DetachedAndNonVisualSources_AreSafe() => RunOnStaThread(() =>
    {
        DependencyObject?[] sources = [null, new Run("Detached"), new LineBreak(), new DependencyObject()];
        foreach (var source in sources)
        {
            Assert.Null(OperatorPage.GetMediaItemFromOriginalSource(source));
            Assert.False(OperatorPage.IsDragBlockedSource(source));
        }
    });

    private static async Task RunOnStaThread(Action action)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                action();
                completed.SetResult();
            }
            catch (Exception ex)
            {
                completed.SetException(ex);
            }
        })
        {
            IsBackground = true,
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }
}
