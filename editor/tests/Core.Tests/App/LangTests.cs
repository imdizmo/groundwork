using System.Text.Json;
using Groundwork.App;
using Xunit;

namespace Groundwork.Tests;

/// <summary>
/// The app's texts by language (Lang): the English catalogue carried inside, a text with values put in, a language
/// taken from the user's lang folder over English (its broken texts left out), Claude's texts kept in English, a shown
/// text's English found, and the computer's language picked. It changes the language in use, so it runs alone.
/// </summary>
[Collection("DataDir")]
public sealed class LangTests : LangTestBase
{
    [Fact]
    public void EnglishIsCarriedAndAnUnknownKeyIsAMistake()
    {
        Assert.Equal("English", Lang.English[Lang.NameKey]);
        Assert.Equal("Cancel", Lang.T("common.cancel"));
        Assert.Equal("The settings could not be saved: /x", Lang.T("settings.notSaved", "/x"));
        Assert.Throws<KeyNotFoundException>(() => Lang.T("no.such.text"));
    }

    [Fact]
    public void AUsersLanguageIsTakenWithEnglishWhereItHasNoText()
    {
        Write("fr", new()
        {
            [Lang.NameKey] = "Français",
            ["common.cancel"] = "Annuler",
            ["settings.notSaved"] = "Dossier de mondes introuvable : {0}",
            // Other places for values than the English: left out, English shown.
            ["settings.addressLoopback"] = "Cet ordinateur seulement",
            // No English text has that key: left out.
            ["no.such.text"] = "Rien",
        });
        Assert.Equal("fr", Lang.Use("fr"));
        Assert.Equal("fr", Lang.Current);
        Assert.Equal("Annuler", Lang.T("common.cancel"));
        Assert.Equal("Dossier de mondes introuvable : /x", Lang.T("settings.notSaved", "/x"));
        Assert.Equal("/x (this computer only)", Lang.T("settings.addressLoopback", "/x"));
        Assert.Equal("Copy", Lang.T("common.copy"));
        Assert.Throws<KeyNotFoundException>(() => Lang.T("no.such.text"));
        Assert.Contains(new Lang.Language("fr", "Français"), Lang.Available());
        Assert.Equal(Lang.Source, Lang.Available()[0].Code);
    }

    [Fact]
    public void WhatClaudeIsToldStaysInEnglish()
    {
        Write("fr", new() { [Lang.NameKey] = "Français", ["history.change.new"] = "{0} nouveaux" });
        Lang.Use("fr");
        Assert.Equal("2 nouveaux", Lang.T("history.change.new", 2));
        Assert.Equal("{0} new", Lang.En("history.change.new"));
        Assert.Equal("2 new", Said.Of("history.change.new", 2).English);
    }

    // EnglishOf: a text shown in the language in use back to its English; one no catalogue has stays as it is, and
    // a language taken again is looked up afresh.
    [Fact]
    public void AShownTextsEnglishIsFound()
    {
        Write("fr", new() { [Lang.NameKey] = "Français", ["common.cancel"] = "Annuler" });
        Lang.Use("fr");
        Assert.Equal("Cancel", Lang.EnglishOf("Annuler"));
        Assert.Equal("Made in code", Lang.EnglishOf("Made in code"));
        Lang.Use(Lang.Source);
        Assert.Equal("Annuler", Lang.EnglishOf("Annuler"));
        Assert.Equal("Cancel", Lang.EnglishOf("Cancel"));
    }

    [Fact]
    public void AnUnknownOrUnreadableLanguageIsEnglish()
    {
        Assert.Equal(Lang.Source, Lang.Use("xx"));
        Assert.Equal("Cancel", Lang.T("common.cancel"));
        File.WriteAllText(Path.Combine(Folder, "de.json"), "{ not json");
        Assert.Equal(Lang.Source, Lang.Use("de"));
        Assert.Contains(new Lang.Language("de", "de"), Lang.Available());
    }

    // A hand-made file whose root is not an object, or that gives a key twice, must not throw out of Use (which
    // Program calls before the window exists), not even for a language the user never chose, since Available()
    // reads every file in the folder.
    [Fact]
    public void AFileThatIsNotOneObjectOrRepeatsAKeyDoesNotCrashTheStart()
    {
        File.WriteAllText(Path.Combine(Folder, "it.json"), "[]");
        File.WriteAllText(Path.Combine(Folder, "fr.json"), """{ "lang.name": "Français", "common.cancel": "Annuler", "common.cancel": "Abandonner" }""");
        Assert.Equal(Lang.Source, Lang.Use("it"));
        Assert.Contains(new Lang.Language("it", "it"), Lang.Available());
        Assert.Equal("fr", Lang.Use("fr"));
        Assert.Equal("Abandonner", Lang.T("common.cancel"));
        Assert.Throws<JsonException>(() => Lang.Parse(new MemoryStream("\"text\""u8.ToArray())));
    }

    [Fact]
    public void TheUsersEnglishFileCorrectsTheCarriedOne()
    {
        Write(Lang.Source, new() { ["common.cancel"] = "Never mind" });
        Lang.Use(Lang.Source);
        Assert.Equal("Never mind", Lang.T("common.cancel"));
        Assert.Equal("Copy", Lang.T("common.copy"));
    }

    [Fact]
    public void ATextTheValuesCannotGoInIsLeftOutEnglishCorrectionsToo()
    {
        Write("fr", new() { [Lang.NameKey] = "Français", ["settings.notSaved"] = "Introuvable {0} dans {chemin}" });
        Lang.Use("fr");
        Assert.Equal("The settings could not be saved: /x", Lang.T("settings.notSaved", "/x"));
        Write(Lang.Source, new() { ["settings.notSaved"] = "Missing {0} {", ["common.cancel"] = "Stop {" });
        Lang.Use(Lang.Source);
        Assert.Equal("The settings could not be saved: /x", Lang.T("settings.notSaved", "/x"));
        // A text never formatted may hold braces.
        Assert.Equal("Stop {", Lang.T("common.cancel"));
        Assert.True(Lang.Fits("{0} of {1:0.0} m", "{1:0.##} m sur {0}"));
        Assert.False(Lang.Fits("{0} m", "{0} m {"));
    }

    // A text without places is never formatted, so a translation of it may hold braces (Fits lets them through)
    // even when the code asks for it with an empty list of values, as PieceCost.Describe does.
    [Fact]
    public void ATextWithoutPlacesIsNotFormattedEvenWhenAskedWithNoValues()
    {
        Write("fr", new() { [Lang.NameKey] = "Français", ["save.pending.allSaved"] = "Tout {est} enregistré" });
        Lang.Use("fr");
        Assert.Equal("Tout {est} enregistré", Lang.T("save.pending.allSaved", []));
        Assert.Equal("All saved", Lang.En("save.pending.allSaved", []));
        Assert.Equal("The settings could not be saved: /x", Lang.T("settings.notSaved", "/x"));
    }

    [Fact]
    public void TheComputersLanguageIsPickedFromItsLocales()
    {
        Assert.Equal(["fr-FR", "de", "en-GB"], Lang.FromLocales("fr_FR.UTF-8:de:en_GB@euro"));
        Assert.Empty(Lang.FromLocales("C"));
        Assert.Empty(Lang.FromLocales(null));
        Assert.Equal(["en-GB", "fr-FR"], Lang.FromMultiString("en-GB\0fr-FR\0\0"));
        string[] have = ["en", "fr", "pt-PT"];
        Assert.Equal("fr", Lang.Pick(["fr-CA"], have));
        Assert.Equal("pt-PT", Lang.Pick(["pt-BR"], have));
        Assert.Equal("fr", Lang.Pick(["ja", "fr"], have));
        Assert.Null(Lang.Pick(["ja"], have));
    }

    [Fact]
    public void PlacesForValuesAreComparedWhateverTheirFormatOrOrder()
    {
        Assert.True(Lang.SamePlaces("{0} of {1:0.0} m", "{1:0.##} m sur {0}"));
        Assert.False(Lang.SamePlaces("{0} of {1}", "{0}"));
        Assert.True(Lang.SamePlaces("a {{literal}}", "un {{littéral}}"));
    }
}
