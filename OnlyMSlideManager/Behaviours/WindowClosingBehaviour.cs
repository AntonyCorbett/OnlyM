using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace OnlyMSlideManager.Behaviours
{
    public class WindowClosingBehaviour
    {
        public static readonly DependencyProperty ClosedProperty
            = DependencyProperty.RegisterAttached(
                "Closed",
                typeof(ICommand),
                typeof(WindowClosingBehaviour),
                new UIPropertyMetadata(ClosedChanged));

        public static readonly DependencyProperty ClosingProperty
            = DependencyProperty.RegisterAttached(
                "Closing",
                typeof(ICommand),
                typeof(WindowClosingBehaviour),
                new UIPropertyMetadata(ClosingChanged));

        public static readonly DependencyProperty CancelClosingProperty
            = DependencyProperty.RegisterAttached(
                "CancelClosing",
                typeof(ICommand),
                typeof(WindowClosingBehaviour));

        public static ICommand? GetClosed(DependencyObject? obj)
        {
            if (obj == null)
            {
                return null;
            }

            return (ICommand)obj.GetValue(ClosedProperty);
        }

        public static void SetClosed(DependencyObject obj, ICommand value)
        {
            obj.SetValue(ClosedProperty, value);
        }

        public static ICommand? GetClosing(DependencyObject? obj)
        {
            if (obj == null)
            {
                return null;
            }

            return (ICommand)obj.GetValue(ClosingProperty);
        }

        public static void SetClosing(DependencyObject obj, ICommand value)
        {
            obj.SetValue(ClosingProperty, value);
        }

        public static ICommand? GetCancelClosing(DependencyObject? obj)
        {
            if (obj == null)
            {
                return null;
            }

            return (ICommand)obj.GetValue(CancelClosingProperty);
        }

        public static void SetCancelClosing(DependencyObject obj, ICommand value)
        {
            obj.SetValue(CancelClosingProperty, value);
        }

        private static void ClosedChanged(
            DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is Window window)
            {
                if (e.NewValue != null)
                {
                    window.Closed += OnWindowClosed;
                }
                else
                {
                    window.Closed -= OnWindowClosed;
                }
            }
        }

        private static void ClosingChanged(
          DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is Window window)
            {
                if (e.NewValue != null)
                {
                    window.Closing += OnWindowClosing;
                }
                else
                {
                    window.Closing -= OnWindowClosing;
                }
            }
        }

        private static void OnWindowClosed(object? sender, EventArgs e)
        {
            var closed = GetClosed(sender as Window);
            closed?.Execute(null);
        }

        private static void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            var closing = GetClosing(sender as Window);
            if (closing != null)
            {
                if (closing.CanExecute(null))
                {
                    closing.Execute(null);
                }
                else
                {
                    var cancelClosing = GetCancelClosing(sender as Window);
                    cancelClosing?.Execute(null);

                    e.Cancel = true;
                }
            }
        }
    }
}
