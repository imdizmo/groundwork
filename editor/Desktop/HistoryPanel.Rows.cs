using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class HistoryPanel
{
    /// <summary>
    /// Where a change was (the map): the middle of the ground it changed and of the objects it brought or took, and
    /// how wide and deep that is, in metres; null when it touched neither (rivers, settings).
    /// </summary>
    private static (float X, float Z, float W, float H)? Where(EditSession s, EditSession.Change c)
    {
        var g = s.Ground;
        float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
        void Add(float x, float z) => (x0, z0, x1, z1) = (MathF.Min(x0, x), MathF.Min(z0, z), MathF.Max(x1, x), MathF.Max(z1, z));
        foreach (int p in c.Points)
        {
            Add(g.OriginX + (p % g.W), g.OriginZ + (p / g.W));
        }
        foreach (var (i, _, _) in c.Things.Where(t => t.Index < s.Scene.Things.Count))
        {
            Add(s.Scene.Things[i].Position.X, s.Scene.Things[i].Position.Z);
        }
        return x0 > x1 ? null : ((x0 + x1) / 2, (z0 + z1) / 2, x1 - x0, z1 - z0);
    }

    /// <summary>One change's row: its label and tags, its time and description, and the actions that fit it.</summary>
    /// <param name="s">The session the change belongs to.</param>
    /// <param name="c">The change.</param>
    /// <param name="undone">On the redo list (shown faded, with Redo to here).</param>
    /// <param name="current">The newest change still in (outlined, no Back to here).</param>
    private Border Row(EditSession s, EditSession.Change c, bool undone, bool current)
    {
        var acts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right };
        // The steps an action changes, asked about first.
        Button Act(string text, string tip, Func<IReadOnlyList<EditSession.Change>> steps, Action a)
        {
            var b = new Button { Content = text, FontSize = 11, Padding = new Thickness(6, 1) };
            ToolTip.SetTip(b, tip);
            b.Click += async (_, _) =>
            {
                if (await Allow(steps()))
                {
                    a();
                }
                Refresh();
            };
            acts.Children.Add(b);
            return b;
        }
        if (undone)
        {
            Act(Lang.T("history.label.redoTo"), Lang.T("history.tip.redoTo"), () => [.. s.RedoList.SkipWhile(r => r != c)], () => s.ForwardTo(c));
        }
        else
        {
            if (!current)
            {
                Act(Lang.T("history.label.backTo"), Lang.T("history.tip.backTo"), () => [.. s.UndoList.SkipWhile(u => u != c).Skip(1)], () => s.BackTo(c));
            }
            if (!c.Removed && c.RevertOf == null)
            {
                Act(Lang.T("history.label.remove"), Lang.T("history.tip.remove"), () => [c], () => Message?.Invoke(s.RemoveChange(c)));
            }
        }
        // Steps from an earlier session of the editor show their day too; on the map, where and how big.
        string meta = c.Time.ToString(c.Time.Date == DateTime.Today ? "T" : "g") + (c.Describe() is { Length: > 0 } d ? " · " + d : "");
        if (s.Scene.IsMap && Where(s, c) is var (wx, wz, ww, wh))
        {
            meta += ww < 2 && wh < 2 ? $" · at x {wx:0}, z {wz:0}" : $" · {ww:0} × {wh:0} m at x {wx:0}, z {wz:0}";
            var show = new Button { Content = "Show", FontSize = 11, Padding = new Thickness(6, 1) };
            ToolTip.SetTip(show, "Turn the camera to it");
            show.Click += (_, _) => ShowAsked?.Invoke(wx, wz);
            acts.Children.Insert(0, show);
        }
        var label = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { new TextBlock { Text = c.Label, FontSize = 12.5, FontWeight = current ? FontWeight.SemiBold : FontWeight.Normal, TextDecorations = c.Removed ? TextDecorations.Strikethrough : null, Foreground = c.Removed ? Ui.Muted : Ui.Text } },
        };
        if (ByClaude(c))
        {
            label.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(999),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.Parse("#7a5a2a")),
                Padding = new Thickness(5, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = Lang.T("settings.claude"), FontSize = 10, Foreground = Ui.Accent },
            }.Tip("history.byClaude"));
        }
        // The current change outlined in amber, like the web editor's.
        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = current ? Ui.Accent : Brushes.Transparent,
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(6),
            Opacity = undone ? 0.5 : 1,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                Children =
                {
                    label,
                    Col(acts, 1),
                    RowOf(new TextBlock { Text = meta, FontSize = 11, Foreground = Ui.Muted }, 1),
                },
            },
        };
    }
}
