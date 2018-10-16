namespace OnlyM.Services
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using Models;
    using OnlyM.Core.Models;

    internal static class ScreenPositionHelper
    {
        public static void SetScreenPosition(FrameworkElement element, ScreenPosition position)
        {
            var parent = GetParentWindow(element);
            if (parent != null)
            {
                if (position.IsFullScreen())
                {
                    element.Margin = new Thickness(0, 0, 0, 0);
                }
                else
                {
                    double leftMargin = (parent.ActualWidth * position.LeftMarginPercentage) / 100.0;
                    double topMargin = (parent.ActualHeight * position.TopMarginPercentage) / 100.0;
                    double rightMargin = (parent.ActualWidth * position.RightMarginPercentage) / 100.0;
                    double bottomMargin = (parent.ActualHeight * position.BottomMarginPercentage) / 100.0;

                    element.Margin = new Thickness(leftMargin, topMargin, rightMargin, bottomMargin);
                }
            }
        }

        public static void SetSubtitleBlockScreenPosition(TextBlock element, ScreenPosition position)
        {
            var parent = GetParentWindow(element);
            if (parent != null)
            {
                if (position.IsFullScreen())
                {
                    element.Margin = new Thickness(0, 0, 0, parent.ActualHeight / 10);
                    element.FontSize = parent.ActualHeight / 22;
                }
                else
                {
                    double leftMargin = (parent.ActualWidth * position.LeftMarginPercentage) / 100.0;
                    double topMargin = (parent.ActualHeight * position.TopMarginPercentage) / 100.0;
                    double rightMargin = (parent.ActualWidth * position.RightMarginPercentage) / 100.0;
                    double bottomMargin = (parent.ActualHeight * position.BottomMarginPercentage) / 100.0;

                    var displayHeight = parent.ActualHeight - topMargin - bottomMargin;

                    element.Margin = new Thickness(leftMargin, topMargin, rightMargin, bottomMargin + (displayHeight / 10));
                    element.FontSize = displayHeight / 22;
                }
            }
        }

        public static void ModifyScreenPosition(
            ScreenPosition screenPosition,
            ScreenMarginSide marginSide,
            int newMarginValue,
            out bool opposingMarginChanged)
        {
            var opposingMarginSide = GetOpposingMarginSide(marginSide);
            var opposingMarginValue = GetScreenMarginValue(screenPosition, opposingMarginSide);

            opposingMarginChanged = newMarginValue + opposingMarginValue > 90;
            if (opposingMarginChanged)
            {
                SetScreenMarginValue(screenPosition, opposingMarginSide, 90 - newMarginValue);
            }

            SetScreenMarginValue(screenPosition, marginSide, newMarginValue);
        }

        private static Window GetParentWindow(DependencyObject child)
        {
            var parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
            {
                return null;
            }

            if (parentObject is Window parent)
            {
                return parent;
            }

            return GetParentWindow(parentObject);
        }

        private static ScreenMarginSide GetOpposingMarginSide(ScreenMarginSide marginSide)
        {
            switch (marginSide)
            {
                case ScreenMarginSide.Left:
                    return ScreenMarginSide.Right;

                case ScreenMarginSide.Right:
                    return ScreenMarginSide.Left;

                case ScreenMarginSide.Top:
                    return ScreenMarginSide.Bottom;

                case ScreenMarginSide.Bottom:
                    return ScreenMarginSide.Top;
            }

            throw new ArgumentException();
        }

        private static int GetScreenMarginValue(ScreenPosition screenPosition, ScreenMarginSide marginSide)
        {
            switch (marginSide)
            {
                case ScreenMarginSide.Left:
                    return screenPosition.LeftMarginPercentage;

                case ScreenMarginSide.Right:
                    return screenPosition.RightMarginPercentage;

                case ScreenMarginSide.Top:
                    return screenPosition.TopMarginPercentage;

                case ScreenMarginSide.Bottom:
                    return screenPosition.BottomMarginPercentage;
            }

            throw new ArgumentException();
        }

        private static void SetScreenMarginValue(
            ScreenPosition screenPosition,
            ScreenMarginSide marginSide,
            int newMarginValue)
        {
            switch (marginSide)
            {
                case ScreenMarginSide.Left:
                    screenPosition.LeftMarginPercentage = newMarginValue;
                    break;

                case ScreenMarginSide.Right:
                    screenPosition.RightMarginPercentage = newMarginValue;
                    break;

                case ScreenMarginSide.Top:
                    screenPosition.TopMarginPercentage = newMarginValue;
                    break;

                case ScreenMarginSide.Bottom:
                    screenPosition.BottomMarginPercentage = newMarginValue;
                    break;

                default:
                    throw new ArgumentException();
            }
        }
    }
}
