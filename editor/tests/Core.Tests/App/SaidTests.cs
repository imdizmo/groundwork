using System.Text.Json;
using Groundwork.App;
using Xunit;

namespace Groundwork.Tests;

/// <summary>
/// A message in two languages (Said): the user's language and English side by side, from a key with values, a text
/// the same in both, names already translated put in as values, and messages joined. It changes the language in use,
/// so it runs alone.
/// </summary>
[Collection("DataDir")]
public sealed class SaidTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "gw-said-" + Guid.NewGuid().ToString("N")[..8]);

    public SaidTests()
    {
        Directory.CreateDirectory(_folder);
        Lang.FolderOverride = _folder;
        File.WriteAllText(Path.Combine(_folder, "fr.json"), JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [Lang.NameKey] = "Français",
            ["common.cancel"] = "Annuler",
            ["common.save"] = "Enregistrer",
            ["settings.notSaved"] = "Dossier de mondes introuvable : {0}",
        }));
        Lang.Use("fr");
    }

    public void Dispose()
    {
        Lang.FolderOverride = null;
        Lang.Use(Lang.Source);
        Directory.Delete(_folder, true);
    }

    [Fact]
    public void AKeysTextIsShownInTheLanguageAndKeptInEnglish()
    {
        Assert.Equal(new Said("Annuler", "Cancel"), Said.Of("common.cancel"));
        Assert.Equal(new Said("Dossier de mondes introuvable : /x", "The settings could not be saved: /x"), Said.Of("settings.notSaved", "/x"));
        Assert.Equal(new Said("disk full", "disk full"), Said.Raw("disk full"));
    }

    // A value that is itself a message goes in as each side's own text, so the English holds no French word.
    [Fact]
    public void ATranslatedValueGoesInOnEachSide()
    {
        var said = Said.Of("settings.notSaved", Said.Of("common.save"));
        Assert.Equal("Dossier de mondes introuvable : Enregistrer", said.Shown);
        Assert.Equal("The settings could not be saved: Save", said.English);
    }

    [Fact]
    public void MessagesAreJoinedSideBySideLeavingOutEmptyOnes()
    {
        Assert.Equal(new Said("Annuler Enregistrer", "Cancel Save"), Said.Of("common.cancel") + Said.Of("common.save"));
        Assert.Equal(new Said("Annuler", "Cancel"), Said.Empty + Said.Of("common.cancel") + Said.Empty);
        Assert.Equal(new Said("Annuler, x, Enregistrer", "Cancel, x, Save"), Said.Join(", ", [Said.Of("common.cancel"), Said.Raw("x"), Said.Empty, Said.Of("common.save")]));
        Assert.Equal(Said.Empty, Said.Join(", ", []));
    }
}
