using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OnlyM.Controls
{
    // "TextBlock" control to display a file path or url truncated to space available
    // with ellipsis in the middle of the path.
    public sealed class PathTextBlock : UserControl
    {
        public static readonly DependencyProperty PathProperty = DependencyProperty.Register(
            "Path", 
            typeof(string), 
            typeof(PathTextBlock), 
            new UIPropertyMetadata(string.Empty, OnPathChanged));

        private readonly TextBlock _textBlock = new();
        
        public PathTextBlock()
        {
            AddChild(_textBlock);
        }

        public string Path
        {
            get => (string)GetValue(PathProperty);
            set => SetValue(PathProperty, value);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            base.MeasureOverride(constraint);

            var measurement = TrimToFit(Path, constraint);
            
            _textBlock.Text = measurement.Item1;

            return measurement.Item2;
        }

        private static void OnPathChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
        {
            var @this = (PathTextBlock)o;
            @this.InvalidateMeasure();
        }

        private Tuple<string, Size> TrimToFit(string? path, Size constraint)
        {
            path ??= string.Empty;

            path = path.Trim();

            var size = MeasureString(path);
            if (size.Width < constraint.Width)
            {
                // no trimming needed
                return new Tuple<string, Size>(path, size);
            }

            if (!IsPathOrUrl(path))
            {
                return new Tuple<string, Size>(path, size);
            }

            const int minCharLength = 5;
            if (path.Length < minCharLength)
            {
                // don't bother!
                return new Tuple<string, Size>(path, size);
            }

            const int charCountIncrement = 2;
            var charsToTrim = charCountIncrement;
            var result = path;

            while (path.Length > minCharLength)
            {
                result = MidTrim(path, charsToTrim);
                size = MeasureString(result);

                if (size.Width <= constraint.Width)
                {
                    break;
                }

                charsToTrim += charCountIncrement;
            }

            return new Tuple<string, Size>(result, size);
        }

        private static bool IsPathOrUrl(string path)
        {
            if (Uri.IsWellFormedUriString(path, UriKind.RelativeOrAbsolute))
            {
                return true;
            }

            var isValidUri = Uri.TryCreate(path, UriKind.Absolute, out var pathUri);
            return isValidUri && pathUri != null && pathUri.IsLoopback;
        }

        private static string MidTrim(string s, int charsToTrim)
        {
            var targetLength = s.Length - charsToTrim;
            var beginLength = targetLength / 2;
            var endLength = targetLength - beginLength;

            return $"{s[..beginLength]}...{s.Substring(s.Length - endLength, endLength)}";
        }

        /// <summary>
        /// Returns the size of the given string if it were to be rendered.
        /// </summary>
        /// <param name="str">The string to measure.</param>
        /// <returns>The size of the string.</returns>
        private Size MeasureString(string str)
        {
            var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            var typeFace = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            var text = new FormattedText(
                str, 
                CultureInfo.CurrentCulture, 
                FlowDirection.LeftToRight, 
                typeFace, 
                FontSize,
                Foreground,
                pixelsPerDip);

            return new Size(text.Width, text.Height);
        }
    }
}
