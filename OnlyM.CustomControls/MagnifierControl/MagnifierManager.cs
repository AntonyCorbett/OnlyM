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

using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace OnlyM.CustomControls.MagnifierControl
{
    public class MagnifierManager : DependencyObject
    {
        public static readonly DependencyProperty CurrentProperty = DependencyProperty.RegisterAttached(
            "Magnifier", 
            typeof(Magnifier), 
            typeof(UIElement), 
            new FrameworkPropertyMetadata(null, OnMagnifierChanged));

        private MagnifierAdorner _adorner;
        private UIElement _element;
        
        public static void SetMagnifier(UIElement element, Magnifier value) => element.SetValue(CurrentProperty, value);
        
        public static Magnifier GetMagnifier(UIElement element) => (Magnifier)element.GetValue(CurrentProperty);
        
        private static void OnMagnifierChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = d as UIElement;

            if (target == null)
            {
                throw new ArgumentException("Magnifier can only be attached to a UIElement.");
            }

            var manager = new MagnifierManager();
            manager.AttachToMagnifier(target, e.NewValue as Magnifier);
        }

        private void Element_MouseLeave(object sender, MouseEventArgs e)
        {
            if (GetMagnifier(_element) is Magnifier magnifier && magnifier.IsFrozen)
            {
                return;
            }

            HideAdorner();
        }

        private void Element_MouseEnter(object sender, MouseEventArgs e)
        {
            ShowAdorner();
        }

        private void Element_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (GetMagnifier(_element) is Magnifier magnifier && magnifier.IsUsingZoomOnMouseWheel)
            {
                if (e.Delta < 0)
                {
                    var newValue = magnifier.ZoomFactor + magnifier.ZoomFactorOnMouseWheel;
                    magnifier.SetCurrentValue(Magnifier.ZoomFactorProperty, newValue);
                }
                else if (e.Delta > 0)
                {
                    var newValue = magnifier.ZoomFactor >= magnifier.ZoomFactorOnMouseWheel 
                        ? magnifier.ZoomFactor - magnifier.ZoomFactorOnMouseWheel 
                        : 0d;

                    magnifier.SetCurrentValue(Magnifier.ZoomFactorProperty, newValue);
                }

                _adorner.UpdateViewBox();
            }
        }

        private void AttachToMagnifier(UIElement element, Magnifier magnifier)
        {
            _element = element;
            _element.MouseEnter += Element_MouseEnter;
            _element.MouseLeave += Element_MouseLeave;
            _element.MouseWheel += Element_MouseWheel;

            magnifier.Target = _element;

            _adorner = new MagnifierAdorner(_element, magnifier);
        }

        private void ShowAdorner()
        {
            VerifyAdornerLayer();
            _adorner.Visibility = Visibility.Visible;
        }

        private void VerifyAdornerLayer()
        {
            if (_adorner.Parent != null)
            {
                return;
            }

            var layer = AdornerLayer.GetAdornerLayer(_element);

            layer?.Add(_adorner);
        }

        private void HideAdorner()
        {
            if (_adorner.Visibility == Visibility.Visible)
            {
                _adorner.Visibility = Visibility.Collapsed;
            }
        }
    }
}
