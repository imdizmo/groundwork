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

    /// <summary>
    /// A zone's numbers for a new map so many metres across (null: cancelled): its hills, rim, lakes and woods, the
    /// defaults scaled to its size.
    /// </summary>
    /// <param name="owner">The window it is modal to.</param>
    /// <param name="metres">How wide the map will be.</param>
    public static async Task<ZoneGenerator.Spec?> AskZone(Window owner, int metres)
    {
        ZoneGenerator.Spec? result = null;
        var dialog = Modal("New zone", 460);
        NumericUpDown Box(double value, double min, double max, double step) =>
            new() { Value = (decimal)value, Minimum = (decimal)min, Maximum = (decimal)max, Increment = (decimal)step, FormatString = "0.##", FontSize = 13, MinWidth = 130 };
        var low = Box(20, -100, 2000, 5);
        var high = Box(70, -100, 2000, 5);
        var hills = Box(300, 30, 3000, 25);
        var rimHeight = Box(200, 0, 3000, 10);
        var rimWidth = Box(Math.Round(metres * 0.12 / 10) * 10, 0, metres / 2.0, 10);
        var lakes = new TextBox { Text = metres >= 800 ? $"{Math.Round(metres * metres / 1e6 * 4)}" : "", PlaceholderText = "none", FontSize = 13, MinWidth = 130 };
        var woods = Box(35, 0, 100, 5);
        var density = Box(20, 0, 500, 1);
        var treeScale = Box(1, 0.2, 10, 0.1);
        var seed = Box(Random.Shared.Next(1, 10000), 0, 1e9, 1);
        Control Row(string label, Control box, string unit) => new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("190,*,60"),
            ColumnSpacing = 8,
            Children =
            {
                new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center },
                Ui.Col(box, 1),
                Ui.Col(new TextBlock { Text = unit, FontSize = 12, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center }, 2),
            },
        };
        var ok = new Button { Content = "Make it", IsDefault = true };
        ok.Click += (_, _) =>
        {
            float F(NumericUpDown b) => (float)(b.Value ?? 0);
            float[] ha = [.. (lakes.Text ?? "").Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(t => float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0).Where(v => v > 0)];
            result = new ZoneGenerator.Spec(Low: F(low), High: MathF.Max(F(low), F(high)), HillSize: F(hills), RimHeight: F(rimHeight), RimWidth: F(rimWidth),
                LakeHectares: ha, Woods: F(woods) / 100, WoodDensity: F(density), TreeScale: F(treeScale), Seed: (int)F(seed));
            dialog.Close();
        };
        var cancel = new Button { Content = Lang.T("common.cancel"), IsCancel = true }.Tip("dialog.cancel");
        cancel.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "Rolling hills, a mountain rim round the edge, lakes in the low ground and woods, the ground painted by slope and height. The same numbers with another seed make another zone.", TextWrapping = TextWrapping.Wrap, FontSize = 13 },
                Row("Hills from", low, "m"),
                Row("Hills up to", high, "m"),
                Row("Hills apart", hills, "m"),
                Row("Mountain rim height", rimHeight, "m (0: none)"),
                Row("Mountain rim width", rimWidth, "m"),
                Row("Lakes", lakes, "ha each"),
                Row("Wooded", woods, "%"),
                Row("Trees in the woods", density, "per ha"),
                Row("Tree size", treeScale, "×"),
                Row("Seed", seed, ""),
                ButtonRow(ok, cancel),
            },
        };
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
