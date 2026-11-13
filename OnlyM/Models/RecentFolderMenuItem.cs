using System.Windows.Input;

namespace OnlyM.Models;

/// <summary>
/// An entry in the "Recent folders" sub-menu of the Operator page's media list menu.
/// </summary>
public sealed class RecentFolderMenuItem
{
    public RecentFolderMenuItem(string path, bool isCurrent, ICommand command)
    {
        Path = path;
        IsCurrent = isCurrent;
        Command = command;
    }

    /// <summary>
    /// Gets the folder path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets a value indicating whether this is the current media folder.
    /// </summary>
    public bool IsCurrent { get; }

    /// <summary>
    /// Gets the command that switches to this folder (parameter = <see cref="Path"/>).
    /// </summary>
    public ICommand Command { get; }
}
