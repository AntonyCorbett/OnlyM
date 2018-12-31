namespace OnlyMSlideManager.Services.DragAndDrop
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Shapes;
    
    public interface IDragAndDropServiceCustom
    {
        void DragSourcePreviewMouseDown(Control card, Point position);

        void DragSourcePreviewMouseMove(Point position);

        void Drop(Rectangle rect);
    }
}
