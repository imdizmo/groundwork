using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>Small modal questions and messages (Avalonia has no message box of its own).</summary>
public static class Dialogs
{
    /// <summary>A question with one or two buttons. True when the first button is chosen.</summary>
    /// <param name="owner">The window it is modal to.</param>
    /// <param name="title">The dialog's title.</param>
    /// <param name="text">The question (selectable, to copy).</param>
    /// <param name="yes">The first button (Enter).</param>
    /// <param name="no">The second button (Esc); null: Cancel.</param>
    /// <param name="alone">No second button: Esc presses the first.</param>
    public static async Task<bool> Ask(Window owner, string title, string text, string yes, string? no = null, bool alone = false)
    {
        bool result = false;
        var dialog = Modal(title, 560);
        // A lone button answers Esc too, so a message closes with it.
        var ok = new Button { Content = yes, IsDefault = true, IsCancel = alone };
        ToolTip.SetTip(ok, Lang.T("dialog.okEnter", yes));
        ok.Click += (_, _) => { result = true; dialog.Close(); };
        var buttons = ButtonRow(ok);
        if (!alone)
        {
            var cancel = new Button { Content = no ?? Lang.T("common.cancel"), IsCancel = true }.Tip("dialog.cancel");
            cancel.Click += (_, _) => dialog.Close();
            buttons.Children.Add(cancel);
        }
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 16,
            Children = { new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13 }, buttons },
        };
        await dialog.ShowDialog(owner);
        return result;
    }

    /// <summary>A message with an OK button.</summary>
    public static Task Tell(Window owner, string title, string text) => Ask(owner, title, text, Lang.T("common.ok"), alone: true);

    /// <summary>A line of text (null: cancelled).</summary>
    /// <param name="owner">The window it is modal to.</param>
    /// <param name="title">The dialog's title.</param>
    /// <param name="text">The question.</param>
    /// <param name="initial">The text to start from.</param>
    public static async Task<string?> AskText(Window owner, string title, string text, string initial = "")
    {
        string? result = null;
        var dialog = Modal(title, 420);
        var box = new TextBox { Text = initial }.Tip("dialog.text");
        var ok = new Button { Content = Lang.T("common.ok"), IsDefault = true };
        ToolTip.SetTip(ok, Lang.T("dialog.useAnswer"));
        ok.Click += (_, _) => { result = box.Text; dialog.Close(); };
        var cancel = new Button { Content = Lang.T("common.cancel"), IsCancel = true }.Tip("dialog.cancel");
        cancel.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 12,
            Children = { new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13 }, box,
                ButtonRow(ok, cancel) },
        };
        dialog.Opened += (_, _) => box.Focus();
        await dialog.ShowDialog(owner);
        return result;
    }

    /// <summary>A dark modal window as tall as its content.</summary>
    /// <param name="title">Its title.</param>
    /// <param name="width">Its width.</param>
    private static Window Modal(string title, double width) => new()
    {
        Title = title,
        Width = width,
        SizeToContent = SizeToContent.Height,
        CanResize = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Background = new SolidColorBrush(Color.FromRgb(24, 28, 34)),
    };

    /// <summary>A dialog's buttons in a row on the right.</summary>
    /// <param name="buttons">The buttons, in order.</param>
    private static StackPanel ButtonRow(params Button[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.AddRange(buttons);
        return row;
    }
}
