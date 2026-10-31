using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>An object kind's name: its model's (the map's models), else the catalogue's.</summary>
    internal string? NameOfPrefab(int prefab) => _models?.NameOf(prefab) ?? AssetIds.NameOf(prefab);

    /// <summary>After a change Claude made (ClaudeTools): the save bar and the message (in the user's language, its English kept for Claude).</summary>
    /// <param name="message">What the status bar says.</param>
    internal void AfterClaudeEdit(Said message)
    {
        Msg(message);
        UpdateSaveBar();
    }
}
