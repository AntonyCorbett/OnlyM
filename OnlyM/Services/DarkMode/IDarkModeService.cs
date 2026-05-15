using System.Windows;

namespace OnlyM.Services.DarkMode;

internal interface IDarkModeService
{
    void SystemThemeChanged();

    void ApplyTitleBarTheme(Window window);
}
