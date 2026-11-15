using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The window is closing: unsaved changes keep it open and ask first; quitting anyway closes it again.
    /// </summary>
    /// <param name="e">The closing, cancelled to ask.</param>
    private async Task OnClosingAsync(WindowClosingEventArgs e)
    {
        if (InMap && MapDirty && !_closeAnyway)
        {
            e.Cancel = true;
            if (await Ask("Quit?", "Your changes to the map are not saved.", Lang.T("save.ask.quit.yes"), Lang.T("common.stay")))
            {
                _closeAnyway = true;
                Close();
            }
        }
    }

    /// <summary>
    /// The panels on the right: under the top bar, one at a time, the View panel shown first.
    /// </summary>
    private void LayOutRightPanels()
    {
        foreach (var right in new[] { ViewPanelCard, History.Card, HelpCard })
        {
            right.Margin = new Thickness(Edge, PanelsTop, Edge, 44);
            right.HorizontalAlignment = HorizontalAlignment.Right;
            right.VerticalAlignment = VerticalAlignment.Top;
        }
        ViewButton.Classes.Add("on");
    }
}
