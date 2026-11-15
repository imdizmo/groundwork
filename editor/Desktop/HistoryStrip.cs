using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Groundwork.Desktop;

/// <summary>
/// The history as a strip along the top of the view: a box for the map as opened, then one per change (undone ones
/// dark, the current one tall and orange). A click or a drag along it steps back and forward through them; the arrow
/// keys step one at a time.
/// </summary>
public sealed class HistoryStrip
{
    /// <summary>The steps' boxes.</summary>
    private sealed class Boxes : Control
    {
        /// <summary>How many boxes, and which is current.</summary>
        public int Count, At;

        /// <summary>The first box drawn (the newest ones show when they do not all fit).</summary>
        public int First;

        public const double Gap = 2, Min = 3, Max = 11;

        public Boxes()
        {
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.SizeWestEast);
            ClipToBounds = true;
        }

        /// <summary>One box's width for the space there is.</summary>
        private double Width1 => Count == 0 ? Max : Math.Clamp((Bounds.Width + Gap) / Count - Gap, Min, Max);

        /// <summary>The box under x (clamped to the ones drawn).</summary>
        public int IndexAt(double x) => Math.Clamp(First + (int)(x / (Width1 + Gap)), 0, Math.Max(0, Count - 1));

        public override void Render(DrawingContext context)
        {
            // ponytail: draws every box each time; a few thousand steps at most (the history's own limit)
            context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
            double w = Width1, h = Bounds.Height;
            int fit = Math.Max(1, (int)((Bounds.Width + Gap) / (w + Gap)));
            First = Math.Clamp(At - fit + 1 + Math.Min(fit / 4, Count - 1 - At), 0, Math.Max(0, Count - fit));
            for (int i = First; i < Count && i < First + fit; i++)
            {
                bool cur = i == At;
                double bh = cur ? 16 : 10;
                var brush = cur ? Tokens.Accent : i > At ? Tokens.LineStrong : Tokens.Dim;
                context.FillRectangle(brush, new Rect((i - First) * (w + Gap), (h - bh) / 2, w, bh), 2);
            }
            if (IsFocused)
            {
                context.DrawRectangle(new Pen(Tokens.Focus, 2), new Rect(Bounds.Size).Deflate(1), 4);
            }
        }
    }

    private readonly Boxes _boxes = new() { Height = 22, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _label = new() { FontSize = Tokens.FsCaption, Foreground = Tokens.Dim, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 320 };
    private readonly Func<EditSession?> _session;
    private readonly Func<IReadOnlyList<EditSession.Change>, Task<bool>> _allow;
    private bool _jumping, _dragging;

    /// <summary>Raised after a step back or forward (the window updates its save bar and panels).</summary>
    public event Action? Moved;

    /// <summary>Raised by its List button: the History panel, with each change's name and Remove.</summary>
    public event Action? ListAsked;

    /// <summary>The strip, hidden until shown.</summary>
    public Border Card { get; }

    /// <summary>A strip over the session's history.</summary>
    /// <param name="session">The session edited (null when none).</param>
    /// <param name="allow">Asked before steps are undone or redone (steps from an earlier session).</param>
    public HistoryStrip(Func<EditSession?> session, Func<IReadOnlyList<EditSession.Change>, Task<bool>> allow)
    {
        _session = session;
        _allow = allow;
        var caps = Ui.Heading("History", 0);
        caps.Margin = default;
        caps.VerticalAlignment = VerticalAlignment.Center;
        var list = new Button { Content = "List", FontSize = Tokens.FsLabel, Padding = new Thickness(8, 1), VerticalAlignment = VerticalAlignment.Center }.Classed("ghost");
        ToolTip.SetTip(list, "The History panel: each change by name, Back to here, Remove");
        list.Click += (_, _) => ListAsked?.Invoke();
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 10, Children = { caps, Ui.Col(_boxes, 1), Ui.Col(_label, 2), Ui.Col(list, 3) } };
        Card = Ui.AsCard(new Border { Child = row, IsVisible = false, VerticalAlignment = VerticalAlignment.Top });
        Card.CornerRadius = new CornerRadius(Tokens.RMd);
        Card.Padding = new Thickness(10, 0);
        Card.Height = 28;
        ToolTip.SetTip(_boxes, "Click or drag along it to step back and forward through the changes (← and → step one)");
        _boxes.PointerPressed += (_, e) =>
        {
            _dragging = true;
            e.Pointer.Capture(_boxes);
            _boxes.Focus();
            _ = Jump(_boxes.IndexAt(e.GetPosition(_boxes).X));
        };
        _boxes.PointerMoved += (_, e) =>
        {
            if (_dragging)
            {
                _ = Jump(_boxes.IndexAt(e.GetPosition(_boxes).X));
            }
        };
        _boxes.PointerReleased += (_, e) => { _dragging = false; e.Pointer.Capture(null); };
        _boxes.KeyDown += (_, e) =>
        {
            int d = e.Key switch { Key.Left => -1, Key.Right => 1, _ => 0 };
            if (d != 0)
            {
                e.Handled = true;
                _ = Jump(_boxes.At + d);
            }
        };
        _boxes.GotFocus += (_, _) => _boxes.InvalidateVisual();
        _boxes.LostFocus += (_, _) => _boxes.InvalidateVisual();
    }

    /// <summary>The steps in order: the changes in, oldest first, then the undone ones, the next to redo first.</summary>
    private static (List<EditSession.Change> Steps, int In) Steps(EditSession s)
    {
        var undo = s.UndoList;
        return ([.. undo, .. Enumerable.Reverse(s.RedoList)], undo.Count);
    }

    /// <summary>Draws the session's history again (shown only).</summary>
    public void Refresh()
    {
        if (!Card.IsVisible)
        {
            return;
        }
        var (steps, done) = _session() is { } s ? Steps(s) : ([], 0);
        _boxes.Count = steps.Count + 1;
        _boxes.At = done;
        string name = done == 0 ? "As opened" : steps[done - 1].Label;
        _label.Text = $"{done} / {steps.Count} · {name}";
        ToolTip.SetTip(_label, name);
        _boxes.InvalidateVisual();
    }

    /// <summary>Shows or hides it.</summary>
    public void Toggle()
    {
        Card.IsVisible = !Card.IsVisible;
        Refresh();
    }

    /// <summary>Steps back or forward to box <paramref name="i"/> (0: the map as opened).</summary>
    internal async Task Jump(int i)
    {
        if (_jumping || _session() is not { } s)
        {
            return;
        }
        var (steps, done) = Steps(s);
        i = Math.Clamp(i, 0, steps.Count);
        if (i == done)
        {
            return;
        }
        // Back: the changes after box i come out; forward: the undone ones up to it go back in.
        var moving = i < done ? steps[i..done] : steps[done..i];
        _jumping = true;
        try
        {
            if (!await _allow(moving))
            {
                return;
            }
            if (i > done)
            {
                s.ForwardTo(steps[i - 1]);
            }
            else if (i > 0)
            {
                s.BackTo(steps[i - 1]);
            }
            else
            {
                while (s.Undo())
                {
                }
            }
        }
        finally
        {
            _jumping = false;
        }
        Moved?.Invoke();
        Refresh();
    }
}
