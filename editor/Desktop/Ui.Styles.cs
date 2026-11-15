using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Groundwork.Desktop;

public static partial class Ui
{
    /// <summary>The design's typeface, Space Grotesk (SIL Open Font License), shipped in Assets/Fonts.</summary>
    public static readonly FontFamily Font = new("avares://Groundwork/Assets/Fonts#Space Grotesk");

    /// <summary>
    /// Applies the look to the application, once at start: Fluent's colour resources set to the palette, and the
    /// styles for windows, buttons and their classes, lists, number boxes and switches.
    /// </summary>
    public static void Apply(Application app)
    {
        var r = app.Resources;
        // Fluent's own colours, the web editor's.
        r["SystemAccentColor"] = Tokens.AccentColor;
        r["SystemAccentColorLight1"] = Color.Parse("#f0953e");
        r["SystemAccentColorLight2"] = Color.Parse("#f4a85c");
        r["SystemAccentColorLight3"] = Color.Parse("#f8c08a");
        r["SystemAccentColorDark1"] = Color.Parse("#d27214");
        r["SystemAccentColorDark2"] = Color.Parse("#b05f10");
        r["SystemAccentColorDark3"] = Color.Parse("#7a4410");
        r["ControlCornerRadius"] = new CornerRadius(6);
        r["OverlayCornerRadius"] = new CornerRadius(8);
        // Buttons.
        r["ButtonBackground"] = Tokens.Raised;
        r["ButtonBackgroundPointerOver"] = Tokens.Raised2;
        r["ButtonBackgroundPressed"] = new SolidColorBrush(Color.Parse("#3b3b40"));
        r["ButtonBackgroundDisabled"] = Tokens.Raised;
        r["ButtonForeground"] = Tokens.Text;
        r["ButtonForegroundPointerOver"] = Tokens.Text;
        r["ButtonForegroundPressed"] = Tokens.Text;
        r["ButtonForegroundDisabled"] = Tokens.Text;
        r["ButtonBorderBrush"] = Tokens.Line;
        r["ButtonBorderBrushPointerOver"] = Tokens.LineStrong;
        r["ButtonBorderBrushPressed"] = Tokens.LineStrong;
        r["ButtonBorderBrushDisabled"] = Tokens.Line;
        // Toggle buttons (chips: biomes, Thermal / Water...): chosen = amber outline, like the web's .on.
        r["ToggleButtonBackground"] = Tokens.Raised;
        r["ToggleButtonBackgroundPointerOver"] = Tokens.Raised2;
        r["ToggleButtonBackgroundPressed"] = Tokens.Raised2;
        r["ToggleButtonBorderBrush"] = Tokens.Line;
        r["ToggleButtonBorderBrushPointerOver"] = Tokens.LineStrong;
        r["ToggleButtonForeground"] = Tokens.Text;
        r["ToggleButtonForegroundPointerOver"] = Tokens.Text;
        // Chosen: the selected wash and an accent line (chips, segments).
        foreach (string? state in new[] { "", "PointerOver", "Pressed" })
        {
            r[$"ToggleButtonBackgroundChecked{state}"] = Tokens.Selected;
            r[$"ToggleButtonForegroundChecked{state}"] = Tokens.Accent;
            r[$"ToggleButtonBorderBrushChecked{state}"] = Tokens.AccentLine;
        }
        // Text boxes, number boxes and lists.
        foreach (string? state in new[] { "", "PointerOver", "Focused" })
        {
            r[$"TextControlBackground{state}"] = Tokens.Bg;
            r[$"TextControlForeground{state}"] = Tokens.Text;
        }
        r["TextControlBorderBrush"] = Tokens.Line;
        r["TextControlBorderBrushPointerOver"] = Tokens.LineStrong;
        r["TextControlBorderBrushFocused"] = Tokens.Accent;
        r["TextControlPlaceholderForeground"] = Tokens.Mute;
        r["TextControlPlaceholderForegroundPointerOver"] = Tokens.Mute;
        r["TextControlPlaceholderForegroundFocused"] = Tokens.Mute;
        r["ComboBoxBackground"] = Tokens.Raised;
        r["ComboBoxBackgroundPointerOver"] = Tokens.Raised2;
        r["ComboBoxBackgroundPressed"] = Tokens.Raised2;
        r["ComboBoxBackgroundFocused"] = Tokens.Raised;
        r["ComboBoxBorderBrush"] = Tokens.Line;
        r["ComboBoxBorderBrushPointerOver"] = Tokens.LineStrong;
        r["ComboBoxBorderBrushPressed"] = Tokens.Accent;
        r["ComboBoxDropDownBackground"] = Tokens.Raised;
        r["ComboBoxDropDownBorderBrush"] = Tokens.LineStrong;
        r["ComboBoxItemBackgroundSelected"] = new SolidColorBrush(Color.Parse("#3a2616"));
        r["ComboBoxItemBackgroundSelectedPointerOver"] = new SolidColorBrush(Color.Parse("#4a301a"));
        r["ComboBoxItemBackgroundPointerOver"] = Tokens.Hover;
        r["ListBoxItemBackgroundSelected"] = new SolidColorBrush(Color.Parse("#3a2616"));
        r["ListBoxItemBackgroundSelectedPointerOver"] = new SolidColorBrush(Color.Parse("#4a301a"));
        r["ListBoxItemBackgroundPointerOver"] = Tokens.Hover;
        // Scroll bars: thin, a quiet thumb on no track.
        r["ScrollBarSize"] = 10.0;
        r["ScrollBarTrackFill"] = Brushes.Transparent;
        r["ScrollBarTrackFillPointerOver"] = Brushes.Transparent;
        r["ScrollBarTrackStroke"] = Brushes.Transparent;
        r["ScrollBarTrackStrokePointerOver"] = Brushes.Transparent;
        r["ScrollBarThumbFill"] = Tokens.LineStrong;
        r["ScrollBarThumbFillPointerOver"] = Tokens.ScrollThumbHover;
        r["ScrollBarThumbFillPressed"] = Tokens.Dim;
        r["ScrollBarButtonArrowForeground"] = Brushes.Transparent;
        r["ScrollBarButtonArrowForegroundPointerOver"] = Tokens.Dim;
        r["ScrollBarButtonBackgroundPointerOver"] = Brushes.Transparent;
        r["ScrollBarBackground"] = Brushes.Transparent;
        r["ScrollBarBackgroundPointerOver"] = Brushes.Transparent;
        r["ScrollBarThumbBackgroundColor"] = ((ISolidColorBrush)Tokens.LineStrong).Color;
        r["CheckBoxMinHeight"] = 26.0;
        // Chosen list rows: a quiet amber, not the full accent.
        r["SystemControlHighlightListAccentLowBrush"] = new SolidColorBrush(Color.Parse("#3a2616"));
        r["SystemControlHighlightListAccentMediumBrush"] = new SolidColorBrush(Color.Parse("#4a301a"));
        r["SystemControlHighlightListAccentHighBrush"] = new SolidColorBrush(Color.Parse("#58381e"));
        r["SystemControlHighlightListLowBrush"] = Tokens.Hover;
        r["SystemControlHighlightListMediumBrush"] = new SolidColorBrush(Color.Parse("#26262c"));
        // Check boxes: filled accent with an ink tick.
        foreach (string? state in new[] { "", "PointerOver", "Pressed" })
        {
            r[$"CheckBoxCheckBackgroundFillChecked{state}"] = Tokens.Accent;
            r[$"CheckBoxCheckBackgroundStrokeChecked{state}"] = Tokens.Accent;
            r[$"CheckBoxCheckGlyphForegroundChecked{state}"] = Tokens.AccentInk;
            r[$"CheckBoxCheckBackgroundStrokeUnchecked{state}"] = Tokens.LineStrong;
            r[$"CheckBoxCheckBackgroundFillUnchecked{state}"] = Tokens.Bg;
        }
        // Menus and sliders.
        r["MenuFlyoutPresenterBackground"] = Tokens.Raised;
        r["MenuFlyoutPresenterBorderBrush"] = Tokens.LineStrong;
        r["MenuFlyoutItemBackgroundPointerOver"] = Tokens.Hover;
        r["MenuFlyoutItemBackgroundPressed"] = Tokens.Active;
        foreach (string? state in new[] { "", "PointerOver", "Pressed" })
        {
            r[$"SliderTrackFill{state}"] = Tokens.LineStrong;
            r[$"SliderTrackValueFill{state}"] = Tokens.Accent;
            r[$"SliderThumbBackground{state}"] = Tokens.Text;
        }
        r["ToolTipBackground"] = Tokens.Raised;
        r["ToolTipBorderBrush"] = Tokens.LineStrong;
        r["ToolTipForeground"] = Tokens.Text;
        r["ExpanderHeaderBackground"] = Brushes.Transparent;
        r["ExpanderHeaderBackgroundPointerOver"] = Tokens.Raised2;
        r["ExpanderHeaderBackgroundPressed"] = Tokens.Raised2;
        r["ExpanderHeaderBorderBrush"] = Brushes.Transparent;
        r["ExpanderHeaderBorderBrushPointerOver"] = Tokens.Line;
        r["ExpanderContentBackground"] = Brushes.Transparent;
        r["ExpanderContentBorderBrush"] = Tokens.Line;
        // Folds inside cards: no padding or border of their own (the card's padding is the only one).
        r["ExpanderHeaderPadding"] = new Thickness(0);
        r["ExpanderContentPadding"] = new Thickness(0, 6, 0, 0);
        r["ExpanderHeaderBorderThickness"] = new Thickness(0);
        r["ExpanderContentDownBorderThickness"] = new Thickness(0);
        r["ExpanderContentUpBorderThickness"] = new Thickness(0);
        r["ExpanderContentLeftBorderThickness"] = new Thickness(0);
        r["ExpanderContentRightBorderThickness"] = new Thickness(0);
        r["ExpanderMinHeight"] = 32.0;

        var s = app.Styles;
        s.Add(new Style(x => x.OfType<Window>()) { Setters = { new Setter(TemplatedControl.BackgroundProperty, Tokens.Bg), new Setter(TemplatedControl.FontSizeProperty, 13.0), new Setter(TemplatedControl.FontFamilyProperty, Font) } });
        s.Add(new Style(x => x.OfType<Button>()) { Setters = { new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(Tokens.RSm)), new Setter(TemplatedControl.PaddingProperty, new Thickness(10, 5)) } });
        // Scroll bars beside the content when they show (not drawn over it), and only then.
        s.Add(new Style(x => x.OfType<ScrollViewer>()) { Setters = { new Setter(ScrollViewer.AllowAutoHideProperty, false), new Setter(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto) } });
        // List rows as compact as the web editor's.
        s.Add(new Style(x => x.OfType<ListBoxItem>()) { Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(8, 4)), new Setter(Layoutable.MinHeightProperty, 0.0) } });
        // Check boxes as compact as the web editor's.
        s.Add(new Style(x => x.OfType<CheckBox>()) { Setters = { new Setter(Layoutable.MinHeightProperty, 26.0) } });
        s.Add(new Style(x => x.OfType<ToggleButton>()) { Setters = { new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(Tokens.RSm)) } });
        // Disabled buttons fade to 40%.
        s.Add(new Style(x => x.OfType<Button>().Class(":disabled")) { Setters = { new Setter(Visual.OpacityProperty, 0.4) } });
        // Tooltips after 450 ms.
        s.Add(new Style(x => x.OfType<Control>()) { Setters = { new Setter(ToolTip.ShowDelayProperty, Tokens.TooltipDelayMs) } });
        // Number boxes without the theme's big up/down buttons (they squeezed the numbers out of narrow
        // panels); the arrow keys and the mouse wheel still step them.
        s.Add(new Style(x => x.OfType<NumericUpDown>())
        {
            Setters = { new Setter(NumericUpDown.ShowButtonSpinnerProperty, false), new Setter(Layoutable.MinWidthProperty, 64.0) },
        });

        // A style for the button's inner presenter (what the theme's hover and press states change).
        Style Presenter(Func<Selector?, Selector> button, params Setter[] setters)
        {
            var st = new Style(x => button(x).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"));
            foreach (var set in setters)
            {
                st.Setters.Add(set);
            }
            return st;
        }
        Setter Bgs(IBrush b) => new(ContentPresenter.BackgroundProperty, b);
        Setter Border(IBrush b) => new(ContentPresenter.BorderBrushProperty, b);
        Setter Fore(IBrush b) => new(ContentPresenter.ForegroundProperty, b);

        // Primary: the amber button (Save).
        var hoverOrange = Tokens.AccentHover;
        s.Add(new Style(x => x.OfType<Button>().Class("primary")) { Setters = { new Setter(TemplatedControl.FontWeightProperty, FontWeight.SemiBold) } });
        s.Add(Presenter(x => x.OfType<Button>().Class("primary"), Bgs(Tokens.Accent), Border(Tokens.Accent), Fore(Tokens.AccentInk)));
        s.Add(Presenter(x => x.OfType<Button>().Class("primary").Class(":pointerover"), Bgs(hoverOrange), Border(hoverOrange), Fore(Tokens.AccentInk)));
        s.Add(Presenter(x => x.OfType<Button>().Class("primary").Class(":pressed"), Bgs(Tokens.AccentPressed), Border(Tokens.AccentPressed), Fore(Tokens.AccentInk)));
        s.Add(Presenter(x => x.OfType<Button>().Class("primary").Class(":disabled"), Bgs(Tokens.Accent), Border(Tokens.Accent), Fore(Tokens.AccentInk)));
        // Ghost: flat, a wash under the pointer (top bar buttons).
        s.Add(Presenter(x => x.OfType<Button>().Class("ghost"), Bgs(Brushes.Transparent), Border(Brushes.Transparent)));
        s.Add(Presenter(x => x.OfType<Button>().Class("ghost").Class(":pointerover"), Bgs(Tokens.Hover), Border(Brushes.Transparent)));
        s.Add(Presenter(x => x.OfType<Button>().Class("ghost").Class(":pressed"), Bgs(Tokens.Active), Border(Brushes.Transparent)));
        s.Add(Presenter(x => x.OfType<Button>().Class("ghost").Class(":disabled"), Bgs(Brushes.Transparent), Border(Brushes.Transparent)));
        // On: the chosen one of a set (the selected wash, accent text).
        s.Add(Presenter(x => x.OfType<Button>().Class("on"), Bgs(Tokens.Selected), Border(Tokens.AccentLine), Fore(Tokens.Accent)));
        s.Add(Presenter(x => x.OfType<Button>().Class("on").Class(":pointerover"), Bgs(Tokens.Selected), Border(Tokens.AccentLine), Fore(Tokens.Accent)));
        s.Add(Presenter(x => x.OfType<Button>().Class("ghost").Class("on"), Bgs(Tokens.Selected), Border(Brushes.Transparent), Fore(Tokens.Accent)));
        s.Add(Presenter(x => x.OfType<Button>().Class("ghost").Class("on").Class(":pointerover"), Bgs(Tokens.Selected), Border(Brushes.Transparent), Fore(Tokens.Accent)));
        // The tool rail: flat buttons, the chosen tool a selected wash with its icon and name in the accent (Save stays the
        // only filled orange).
        s.Add(Presenter(x => x.OfType<Button>().Class("rail"), Bgs(Brushes.Transparent), Border(Brushes.Transparent)));
        s.Add(Presenter(x => x.OfType<Button>().Class("rail").Class(":pointerover"), Bgs(Tokens.Hover), Border(Brushes.Transparent)));
        s.Add(Presenter(x => x.OfType<Button>().Class("rail").Class("on"), Bgs(Tokens.Selected), Border(Brushes.Transparent), Fore(Tokens.Accent)));
        s.Add(Presenter(x => x.OfType<Button>().Class("rail").Class("on").Class(":pointerover"), Bgs(Tokens.Selected), Border(Brushes.Transparent), Fore(Tokens.Accent)));
        s.Add(new Style(x => x.OfType<Button>().Class("rail").Class("on")) { Setters = { new Setter(TemplatedControl.FontWeightProperty, FontWeight.SemiBold) } });

        // Switches (the web's View panel toggles): a CheckBox drawn as a small track and knob.
        s.Add(new Style(x => x.OfType<CheckBox>().Class("switch")) { Setters = { new Setter(TemplatedControl.TemplateProperty, SwitchTemplate()) } });
    }

    /// <summary>
    /// The "switch" class's template: a check box drawn as a small track and knob (amber, knob right, when on),
    /// with its content to the right.
    /// </summary>
    private static FuncControlTemplate<CheckBox> SwitchTemplate() => new((cb, scope) =>
    {
        var knob = new Border { Width = 13, Height = 13, CornerRadius = new CornerRadius(7), Margin = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        var track = new Border { Width = 30, Height = 17, CornerRadius = new CornerRadius(9), Child = knob, VerticalAlignment = VerticalAlignment.Center };
        var content = new ContentPresenter { Name = "PART_ContentPresenter", VerticalAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(10, 0, 0, 0) };
        content.Bind(ContentPresenter.ContentProperty, cb.GetObservable(ContentControl.ContentProperty));
        void Sync()
        {
            bool on = cb.IsChecked == true;
            track.Background = on ? Tokens.Accent : Tokens.LineStrong;
            knob.Background = on ? Tokens.AccentInk : Tokens.Dim;
            knob.HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        cb.PropertyChanged += (_, e) => { if (e.Property == ToggleButton.IsCheckedProperty) { Sync(); } };
        Sync();
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Background = Brushes.Transparent, Margin = new Thickness(0, 3) };
        Grid.SetColumn(content, 1);
        root.Children.Add(track);
        root.Children.Add(content);
        content.RegisterInNameScope(scope);
        return root;
    });
}
