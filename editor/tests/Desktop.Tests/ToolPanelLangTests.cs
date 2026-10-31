using Avalonia.Headless.XUnit;
using Groundwork.App;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The stamps in another language (the made-up one: PseudoLang): a built-in stamp chosen as the shape comes back in
/// another language; a loaded picture's entry carries its name, so its English cannot be found from it.
/// </summary>
[Collection("DataDir")]
public sealed class ToolPanelLangTests
{

    [AvaloniaFact]
    public void ABuiltInStampChosenComesBackInAnotherLanguage()
    {
        var p = Prefs.InMemory();
        var english = new ToolPanel();
        p.Bind(english.ShapeBox, "brush.shape");
        english.ShapeBox.SelectedIndex = 5;
        Assert.Equal("Stamp: Mesa (flat top)", p.Get("brush.shape", ""));
        using (new PseudoLang())
        {
            var pseudo = new ToolPanel();
            p.Bind(pseudo.ShapeBox, "brush.shape");
            Assert.Equal(5, pseudo.ShapeBox.SelectedIndex);
            pseudo.ShapeBox.SelectedIndex = 6;
        }
        var back = new ToolPanel();
        p.Bind(back.ShapeBox, "brush.shape");
        Assert.Equal(6, back.ShapeBox.SelectedIndex);
        Assert.Equal("Stamp: Crater rim", back.ShapeBox.SelectedItem);
    }

    [AvaloniaFact]
    public void ALoadedStampIsNamedInTheLanguageInUse()
    {
        using var kept = new KeptStamps();
        using var pseudo = new PseudoLang();
        var t = new ToolPanel();
        Said? said = null;
        t.Message += m => said = m;
        t.AddStamp("pic", new float[Stamps.Size * Stamps.Size]);
        Assert.Equal(PseudoLang.Wrap("Stamp: pic"), t.ShapeBox.SelectedItem);
        Assert.Equal("Loaded the stamp “pic”: white parts work fully, black parts not at all.", said!.English);
        Assert.True(PseudoLang.Translated(said.Shown, []), said.Shown);
        Assert.Equal("pic", t.Brush.StampLabel);
        // Its entry holds its name as a value: no catalogue text gives its English back (the known limit of a
        // remembered loaded stamp across languages).
        Assert.Equal(PseudoLang.Wrap("Stamp: pic"), Lang.EnglishOf(PseudoLang.Wrap("Stamp: pic")));
    }
}
