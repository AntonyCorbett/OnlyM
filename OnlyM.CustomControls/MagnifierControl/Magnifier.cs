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
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;

    [TemplatePart(Name = PartVisualBrush, Type = typeof(VisualBrush))]
    public class Magnifier : Control
    {
        public static readonly DependencyProperty FrameTypeProperty = DependencyProperty.Register(
            "FrameType", 
            typeof(FrameType), 
            typeof(Magnifier), 
            new UIPropertyMetadata(FrameType.Circle, OnFrameTypeChanged));

        public static readonly DependencyProperty IsUsingZoomOnMouseWheelProperty = DependencyProperty.Register(
            "IsUsingZoomOnMouseWheel", 
            typeof(bool), 
            typeof(Magnifier), 
            new UIPropertyMetadata(true));

        public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
            "Radius", 
            typeof(double), 
            typeof(Magnifier), 
            new FrameworkPropertyMetadata(DefaultSize / 2, OnRadiusPropertyChanged));

        public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
            "Target", 
            typeof(UIElement), 
            typeof(Magnifier));

        public static readonly DependencyProperty ZoomFactorProperty = DependencyProperty.Register(
            "ZoomFactor", 
            typeof(double), 
            typeof(Magnifier), 
            new FrameworkPropertyMetadata(0.5, OnZoomFactorPropertyChanged), 
            OnValidationCallback);

        public static readonly DependencyProperty ZoomFactorOnMouseWheelProperty = DependencyProperty.Register(
            "ZoomFactorOnMouseWheel", 
            typeof(double), 
            typeof(Magnifier), 
            new FrameworkPropertyMetadata(0.1d, OnZoomFactorOnMouseWheelPropertyChanged), 
            OnZoomFactorOnMouseWheelValidationCallback);

        private const double DefaultSize = 100d;
        private const string PartVisualBrush = "PART_VisualBrush";
        
        private VisualBrush _visualBrush = new VisualBrush();

        static Magnifier()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Magnifier), new FrameworkPropertyMetadata(typeof(Magnifier)));
            HeightProperty.OverrideMetadata(typeof(Magnifier), new FrameworkPropertyMetadata(DefaultSize));
            WidthProperty.OverrideMetadata(typeof(Magnifier), new FrameworkPropertyMetadata(DefaultSize));
        }

        public Magnifier()
        {
            SizeChanged += OnSizeChangedEvent;
        }

        public FrameType FrameType
        {
            get => (FrameType)GetValue(FrameTypeProperty);
            set => SetValue(FrameTypeProperty, value);
        }

        public bool IsUsingZoomOnMouseWheel
        {
            get => (bool)GetValue(IsUsingZoomOnMouseWheelProperty);
            set => SetValue(IsUsingZoomOnMouseWheelProperty, value);
        }

        public bool IsFrozen
        {
            get;
            private set;
        }

        public double Radius
        {
            get => (double)GetValue(RadiusProperty);
            set => SetValue(RadiusProperty, value);
        }

        public UIElement Target
        {
            get => (UIElement)GetValue(TargetProperty);
            set => SetValue(TargetProperty, value);
        }

        public double ZoomFactor
        {
            get => (double)GetValue(ZoomFactorProperty);
            set => SetValue(ZoomFactorProperty, value);
        }

        public double ZoomFactorOnMouseWheel
        {
            get => (double)GetValue(ZoomFactorOnMouseWheelProperty);
            set => SetValue(ZoomFactorOnMouseWheelProperty, value);
        }

        internal Rect ViewBox
        {
            get => _visualBrush.Viewbox;
            set => _visualBrush.Viewbox = value;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var newBrush = GetTemplateChild(PartVisualBrush) as VisualBrush ?? new VisualBrush();

            // Just create a brush as placeholder even if there is no such brush.
            // This avoids having to "if" each access to the _visualBrush member.
            // Do not keep the current _visualBrush whatsoever to avoid memory leaks.
            newBrush.Viewbox = _visualBrush.Viewbox;
            _visualBrush = newBrush;
        }

        public void Freeze(bool freeze)
        {
            IsFrozen = freeze;
        }

        protected virtual void OnFrameTypeChanged(FrameType oldValue, FrameType newValue)
        {
            UpdateSizeFromRadius();
        }

        protected virtual void OnRadiusChanged(DependencyPropertyChangedEventArgs e)
        {
            UpdateSizeFromRadius();
        }

        protected virtual void OnZoomFactorChanged(DependencyPropertyChangedEventArgs e)
        {
            UpdateViewBox();
        }

        protected virtual void OnZoomFactorOnMouseWheelChanged(DependencyPropertyChangedEventArgs e)
        {
        }

        private static void OnFrameTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var m = (Magnifier)d;
            m.OnFrameTypeChanged((FrameType)e.OldValue, (FrameType)e.NewValue);
        }

        private static void OnRadiusPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var m = (Magnifier)d;
            m.OnRadiusChanged(e);
        }

        private static bool OnValidationCallback(object baseValue)
        {
            var zoomFactor = (double)baseValue;
            return zoomFactor >= 0;
        }

        private static void OnZoomFactorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var m = (Magnifier)d;
            m.OnZoomFactorChanged(e);
        }

        private static bool OnZoomFactorOnMouseWheelValidationCallback(object baseValue)
        {
            var zoomFactorOnMouseWheel = (double)baseValue;
            return zoomFactorOnMouseWheel >= 0;
        }

        private static void OnZoomFactorOnMouseWheelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var m = (Magnifier)d;
            m.OnZoomFactorOnMouseWheelChanged(e);
        }

        private void OnSizeChangedEvent(object sender, SizeChangedEventArgs e)
        {
            UpdateViewBox();
        }

        private void UpdateSizeFromRadius()
        {
            // APC removed so that Rectangle can be sized too
            ////if (FrameType == FrameType.Circle)
            {
                double newSize = Radius * 2;
                if (!AreVirtuallyEqual(Width, newSize))
                {
                    Width = newSize;
                }

                if (!AreVirtuallyEqual(Height, newSize))
                {
                    Height = newSize;
                }
            }
        }

        private bool AreVirtuallyEqual(double d1, double d2)
        {
            var difference = Math.Abs(d1 * .00001);
            return Math.Abs(d1 - d2) <= difference;
        }

        private void UpdateViewBox()
        {
            if (!IsInitialized)
            {
                return;
            }

            ViewBox = new Rect(
              ViewBox.Location,
              new Size(ActualWidth * ZoomFactor, ActualHeight * ZoomFactor));
        }
    }
}