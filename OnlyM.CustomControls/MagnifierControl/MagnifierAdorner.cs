/*************************************************************************************
Adapted by Antony Corbett from the Xceed WPF Extended Toolkit.
Original copyright is shown below.
***********************************************************************************/

/*************************************************************************************
   Extended WPF Toolkit
   Copyright (C) 2007-2013 Xceed Software Inc.
   This program is provided to you under the terms of the Microsoft Public
   License (Ms-PL) as published at http://wpftoolkit.codeplex.com/license 
   For more features, controls, and fast professional support,
   pick up the Plus Edition at http://xceed.com/wpf_toolkit
   Stay informed: follow @datagrid on Twitter or Like http://facebook.com/datagrids
  ***********************************************************************************/

namespace OnlyM.CustomControls.MagnifierControl
{
    using System.Windows;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Media;

    public class MagnifierAdorner : Adorner
    {
        private readonly Magnifier _magnifier;
        private Point _currentMousePosition;
        private double _currentZoomFactor;

        public MagnifierAdorner(UIElement element, Magnifier magnifier)
          : base(element)
        {
            _magnifier = magnifier;
            _currentZoomFactor = _magnifier.ZoomFactor;
            UpdateViewBox();
            AddVisualChild(_magnifier);

            Loaded += (s, e) => InputManager.Current.PostProcessInput += OnProcessInput;
            Unloaded += (s, e) => InputManager.Current.PostProcessInput -= OnProcessInput;
        }

        protected override int VisualChildrenCount => 1;

        internal void UpdateViewBox()
        {
            var viewBoxLocation = CalculateViewBoxLocation();
            _magnifier.ViewBox = new Rect(viewBoxLocation, _magnifier.ViewBox.Size);
        }

        protected override Visual GetVisualChild(int index)
        {
            return _magnifier;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            _magnifier.Measure(constraint);
            return base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var x = _currentMousePosition.X - (_magnifier.Width / 2);
            var y = _currentMousePosition.Y - (_magnifier.Height / 2);
            _magnifier.Arrange(new Rect(x, y, _magnifier.Width, _magnifier.Height));
            return base.ArrangeOverride(finalSize);
        }

        private void OnProcessInput(object sender, ProcessInputEventArgs e)
        {
            var pt = Mouse.GetPosition(this);

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_currentMousePosition == pt && _magnifier.ZoomFactor == _currentZoomFactor)
            {
                return;
            }

            if (_magnifier.IsFrozen)
            {
                return;
            }

            _currentMousePosition = pt;
            _currentZoomFactor = _magnifier.ZoomFactor;
            UpdateViewBox();
            InvalidateArrange();
        }

        private Point CalculateViewBoxLocation()
        {
            var adorner = Mouse.GetPosition(this);
            var element = Mouse.GetPosition(AdornedElement);

            var offsetX = element.X - adorner.X;
            var offsetY = element.Y - adorner.Y;

            // An element will use the offset from its parent (StackPanel, Grid, etc.) to be rendered.
            // When this element is put in a VisualBrush, the element will draw with that offset applied. 
            // To fix this: we add that parent offset to Magnifier location.
            var parentOffsetVector = VisualTreeHelper.GetOffset(_magnifier.Target);
            var parentOffset = new Point(parentOffsetVector.X, parentOffsetVector.Y);

            var left = _currentMousePosition.X - ((_magnifier.ViewBox.Width / 2) + offsetX) + parentOffset.X;
            var top = _currentMousePosition.Y - ((_magnifier.ViewBox.Height / 2) + offsetY) + parentOffset.Y;

            return new Point(left, top);
        }
    }
}