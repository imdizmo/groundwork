namespace Groundwork.Desktop;

public partial class MainWindow
{
    /// <summary>The documentation's place for what the window shows (F1): the start page's, else the tool in use.</summary>
    internal HelpTarget HelpTopic() => StartPage != null && _pages.Content == StartPage.View ? HelpTopics.StartPage : HelpTopics.Tool(Tools.Mode, Tools.Tool);

    /// <summary>F1, on any page and while typing: the Help window at what the window shows (HelpTopic).</summary>
    /// <returns>Whether the key was F1.</returns>
    private bool HelpKey(Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key != Avalonia.Input.Key.F1 || e.KeyModifiers != Avalonia.Input.KeyModifiers.None)
        {
            return false;
        }
        HelpWindow.Open(HelpTopic());
        e.Handled = true;
        return true;
    }
}
