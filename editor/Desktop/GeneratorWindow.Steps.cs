using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using static Groundwork.Desktop.ZoneGenerator;

namespace Groundwork.Desktop;

// New map's shaping steps: the order the ground is worn in, each on or off and at a strength, added and taken away.
public sealed partial class GeneratorWindow
{
    private readonly StackPanel _stepRows = new() { Spacing = 3 };

    /// <summary>The steps as the numbers have them (the usual order when they have none).</summary>
    private ShapeStep[] Steps => _spec.Steps ?? DefaultSteps;

    /// <summary>The shaping steps' section: a row a step, then Add a step and Usual order.</summary>
    private void AddSteps()
    {
        _knobs.Children.Add(Ui.Heading("Shaping steps", 14));
        _knobs.Children.Add(new TextBlock
        {
            Text = "The order the ground is shaped in, after its hills, rim and coast and before the water and planting. Each step follows its slider above; × runs it stronger or weaker. Add one again to run it twice.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Foreground = Ui.Muted,
        });
        _knobs.Children.Add(_stepRows);
        var add = new ComboBox { ItemsSource = StepKinds.Select(k => k.Name).ToList(), PlaceholderText = "Add a step…", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        add.SelectionChanged += (_, _) =>
        {
            if (!_setting && add.SelectedIndex >= 0)
            {
                Set([.. Steps, new ShapeStep(StepKinds[add.SelectedIndex].Kind)]);
                add.SelectedIndex = -1;
            }
        };
        var usual = new Button { Content = "Usual order", FontSize = 12 };
        ToolTip.SetTip(usual, "Every step once, on, in the usual order, at its slider's strength.");
        usual.Click += (_, _) => Set(DefaultSteps);
        _knobs.Children.Add(Ui.Row("", add, usual, "0,*,110"));
        _show.Add(FillSteps);
    }

    /// <summary>New steps: kept (none kept when they are the usual order, so the map's numbers stay as they were), shown, previewed.</summary>
    private void Set(ShapeStep[] steps)
    {
        _spec = _spec with { Steps = steps.SequenceEqual(DefaultSteps) ? null : steps };
        _setting = true;
        FillSteps();
        _setting = false;
        Changed();
    }

    /// <summary>A row a step: on or off with its name, its strength, and up, down, again and remove.</summary>
    private void FillSteps()
    {
        _stepRows.Children.Clear();
        var steps = Steps;
        for (int i = 0; i < steps.Length; i++)
        {
            int at = i;
            var step = steps[i];
            string name = StepKinds.FirstOrDefault(k => k.Kind == step.Kind).Name ?? step.Kind;
            string slider = StepKinds.FirstOrDefault(k => k.Kind == step.Kind).Slider ?? "";
            var on = new CheckBox { Content = $"{i + 1}. {name}", IsChecked = step.On, FontSize = 12 };
            ToolTip.SetTip(on, $"Runs with the {slider} slider's strength" + (step.Kind == "scree" ? " (× does not change it: it rests at its angle)" : ""));
            on.IsCheckedChanged += (_, _) => { if (!_setting) { Set(With(at, steps[at] with { On = on.IsChecked == true })); } };
            var times = new TextBox { Text = "×" + step.Times.ToString("0.##", CultureInfo.InvariantCulture), FontSize = 12, MinWidth = 0, MinHeight = 0, Height = 24, Padding = new Thickness(4, 1), HorizontalContentAlignment = HorizontalAlignment.Right };
            ToolTip.SetTip(times, "How strongly it runs: its slider's strength times this (Enter to use it)");
            void Typed()
            {
                if (!_setting && float.TryParse((times.Text ?? "").TrimStart('×', 'x', ' '), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && v >= 0 && MathF.Abs(v - steps[at].Times) > 1e-4f)
                {
                    Set(With(at, steps[at] with { Times = MathF.Min(v, 10) }));
                }
            }
            times.LostFocus += (_, _) => Typed();
            times.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Typed(); } };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,52,26,26,26,26"), ColumnSpacing = 3 };
            row.Children.Add(on);
            row.Children.Add(Ui.Col(times, 1));
            row.Children.Add(Ui.Col(Small("↑", "Earlier", at > 0, () => Set(Swapped(at, at - 1))), 2));
            row.Children.Add(Ui.Col(Small("↓", "Later", at < steps.Length - 1, () => Set(Swapped(at, at + 1))), 3));
            row.Children.Add(Ui.Col(Small("+", "Run it again, just after", true, () => Set([.. steps[..(at + 1)], steps[at], .. steps[(at + 1)..]])), 4));
            row.Children.Add(Ui.Col(Small("✕", "Take it out", true, () => Set([.. steps[..at], .. steps[(at + 1)..]])), 5));
            _stepRows.Children.Add(row);
        }
        if (steps.Length == 0)
        {
            _stepRows.Children.Add(new TextBlock { Text = "No steps: the ground as its hills, rim and coast make it.", FontSize = 11, Foreground = Ui.Muted });
        }
        ShapeStep[] With(int i, ShapeStep s) => [.. steps.Select((x, j) => j == i ? s : x)];
        ShapeStep[] Swapped(int i, int j)
        {
            var copy = steps.ToArray();
            (copy[i], copy[j]) = (copy[j], copy[i]);
            return copy;
        }
    }

    private static Button Small(string glyph, string tip, bool enabled, Action click)
    {
        var b = new Button { Content = glyph, FontSize = 11, Padding = new Thickness(0), MinWidth = 0, Width = 26, Height = 24, IsEnabled = enabled, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(b, tip);
        b.Click += (_, _) => click();
        return b;
    }
}
