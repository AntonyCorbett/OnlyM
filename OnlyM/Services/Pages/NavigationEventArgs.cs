using System;

namespace OnlyM.Services.Pages;

internal sealed class NavigationEventArgs : EventArgs
{
    public string? PageName { get; set; }
}
