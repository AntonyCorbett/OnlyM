using System.Windows;

namespace OnlyM.Dialogs;

/// <summary>
/// A simple yes/no confirmation dialog shown in the main window's DialogHost.
/// </summary>
public partial class ConfirmDialog
{
    public ConfirmDialog(string message, string? detail, string confirmText, string cancelText)
    {
        InitializeComponent();

        MessageTextBlock.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;

        if (!string.IsNullOrEmpty(detail))
        {
            DetailTextBlock.Text = detail;
            DetailTextBlock.Visibility = Visibility.Visible;
        }
    }
}
