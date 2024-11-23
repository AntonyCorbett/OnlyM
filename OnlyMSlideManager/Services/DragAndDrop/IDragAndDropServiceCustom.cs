using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace OnlyMSlideManager.Services.DragAndDrop;

public interface IDragAndDropServiceCustom
{
    void DragSourcePreviewMouseDown(Control card, Point position);

    void DragSourcePreviewMouseMove(Point position);

    void Drop(Rectangle rect, DragEventArgs e);

    void DragEnter(Rectangle rect, DragEventArgs e);
}
