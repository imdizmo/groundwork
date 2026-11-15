// Shared UI tokens, design v2 (2026-11-15). Keep the values identical in every copy; only the namespace differs.
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Groundwork.Desktop;

/// <summary>The design's tokens: surfaces, lines, text, accent, states, type sizes, space, radii, shadows and motion.</summary>
public static class Tokens
{
    private static ImmutableSolidColorBrush B(string hex) => new(Color.Parse(hex));
    private static ImmutableSolidColorBrush A(byte r, byte g, byte b, double a) => new(Color.FromArgb((byte)Math.Round(a * 255), r, g, b));

    /// <summary>Surfaces.</summary>
    public static readonly IBrush Bg = B("#0F0F10"), Panel = B("#151518"), Raised = B("#1A1A1E"), Raised2 = B("#232328");
    /// <summary>Chrome over a 3D view, and behind a modal dialog.</summary>
    public static readonly IBrush Overlay = A(20, 20, 22, .88), Scrim = A(0, 0, 0, .55);
    /// <summary>Washes.</summary>
    public static readonly IBrush Hover = A(255, 255, 255, .08), Active = A(255, 255, 255, .11), Selected = A(236, 130, 25, .17);
    /// <summary>Lines.</summary>
    public static readonly IBrush Line = B("#2A2A30"), LineStrong = B("#3A3A42"), LineOverlay = A(255, 255, 255, .11);
    /// <summary>Text.</summary>
    public static readonly IBrush Text = B("#FFFFFF"), Dim = B("#A7A7AD"), Mute = B("#86868D"), Faint = B("#6B6B72");
    /// <summary>The accent as a colour.</summary>
    public static readonly Color AccentColor = Color.Parse("#EC8219");
    /// <summary>The accent.</summary>
    public static readonly IBrush Accent = B("#EC8219"), AccentHover = B("#F0953E"), AccentPressed = B("#D27214"), AccentInk = B("#1A0E03");
    /// <summary>The accent's washes.</summary>
    public static readonly IBrush AccentSoft = A(236, 130, 25, .14), AccentLine = A(236, 130, 25, .40);
    /// <summary>Opaque equivalents of AccentSoft.</summary>
    public static readonly IBrush AccentSoftOnRaised = B("#37291D"), AccentSoftOnPanel = B("#332418");
    /// <summary>Success.</summary>
    public static readonly IBrush Success = B("#5BC07A"), SuccessSoft = A(91, 192, 122, .14);
    /// <summary>Warning.</summary>
    public static readonly IBrush Warning = B("#E8B96A"), WarningSoft = A(232, 185, 106, .14);
    /// <summary>Danger.</summary>
    public static readonly IBrush Danger = B("#F2786E"), DangerSoft = A(242, 120, 110, .14), DangerFill = B("#C9372C"), DangerFillHover = B("#D9483D");
    /// <summary>Info.</summary>
    public static readonly IBrush Info = B("#5AA9FF"), InfoSoft = A(90, 169, 255, .14);
    /// <summary>Focus and scroll bars.</summary>
    public static readonly IBrush Focus = Accent, ScrollThumb = B("#3A3A42"), ScrollThumbHover = B("#4E4E56");

    /// <summary>Type sizes (DIPs = CSS px).</summary>
    public const double FsCaps = 11, FsCaption = 11.5, FsLabel = 12, FsBody = 13, FsTitle = 15, FsHeading = 18, FsDisplay = 24;
    /// <summary>Prose (roomy tier).</summary>
    public const double FsProse = 14;
    /// <summary>0.08em at 11 px.</summary>
    public const double CapsLetterSpacing = 0.9;
    /// <summary>Titles.</summary>
    public const double TitleLetterSpacing = -0.15;

    /// <summary>Space.</summary>
    public const double S1 = 4, S2 = 8, S3 = 12, S4 = 16, S5 = 24, S6 = 32, S7 = 48;
    /// <summary>Radii.</summary>
    public const double RXs = 4, RSm = 6, RMd = 8, RLg = 12, RXl = 16, RPill = 999;
    /// <summary>Sizes.</summary>
    public const double TopBar = 40, StatusBar = 24, Control = 28, ControlSm = 24, Row = 28, Tab = 32;
    /// <summary>Sizes (roomy tier).</summary>
    public const double ControlLg = 36, ControlLgSearch = 34;
    /// <summary>Panel widths.</summary>
    public const double Rail = 64, Sidebar = 248, Inspector = 320, PanelMin = 240, PanelMax = 520, LabelMin = 88;

    /// <summary>Floating cards over a 3D view.</summary>
    public static readonly BoxShadows Shadow1 = BoxShadows.Parse("0 6 24 0 #59000000");
    /// <summary>Menus, popovers, tooltips, toasts.</summary>
    public static readonly BoxShadows Shadow2 = BoxShadows.Parse("0 4 12 0 #73000000, 0 12 32 0 #66000000");
    /// <summary>Dialogs.</summary>
    public static readonly BoxShadows Shadow3 = BoxShadows.Parse("0 20 50 0 #73000000");

    /// <summary>Motion.</summary>
    public static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(120), Normal = TimeSpan.FromMilliseconds(150), Slow = TimeSpan.FromMilliseconds(180);
    /// <summary>Before a tooltip shows.</summary>
    public const int TooltipDelayMs = 450;
}
