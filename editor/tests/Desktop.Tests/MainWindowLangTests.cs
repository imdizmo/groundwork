using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Groundwork.App;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The status bar in another language (the made-up one: PseudoLang): a message is shown in it while its English is
/// kept for Claude, who reads it back in editor_state; a message added to keeps each side's language; a text put
/// there directly is the same for both. The questions about saving, discarding, leaving and quitting are catalogue
/// texts, titles and buttons too. The Script tool's question and its run's messages are in the language, what
/// Claude's run_script is told in English. The brush's messages from the window are in the language and read in
/// English.
/// </summary>
[Collection("DataDir")]
public sealed class MainWindowLangTests
{
    /// <summary>
    /// A window without a world, shown, with what it posts when it opens done (it says nothing over the messages tested).
    /// </summary>
    private static MainWindow ShownWindow()
    {
        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        Dispatcher.UIThread.RunJobs();
        return w;
    }

    [AvaloniaFact]
    public async Task TheStatusBarShowsTheLanguageAndClaudeReadsEnglish()
    {
        var w = ShownWindow();
        var tools = new ClaudeTools(w);
        using var pseudo = new PseudoLang();

        w.Msg("settings.notSaved", "/x");
        Assert.Equal("⟦The settings could not be saved: /x⟧", w.MessageText.Text);
        Assert.Equal("The settings could not be saved: /x", w.LastMessageEn);
        Assert.Equal("The settings could not be saved: /x", System.Text.Json.JsonElement.Parse(await tools.EditorState()).GetProperty("message").GetString());

        w.Msg(w.LastSaid + Said.Of("common.save"));
        Assert.Equal("⟦The settings could not be saved: /x⟧ ⟦Save⟧", w.MessageText.Text);
        Assert.Equal("The settings could not be saved: /x Save", w.LastMessageEn);

        // An exception's message, put there as it is.
        w.MessageText.Text = "disk full";
        Assert.Equal("disk full", w.LastMessageEn);
        Assert.Equal(new Said("disk full", "disk full"), w.LastSaid);
    }

    // A file chosen as the brush's stamp that is not a picture: said in the language in use, read in English, and no
    // stamp added.
    [AvaloniaFact]
    public void AStampPictureThatCannotBeReadIsSaidInTheLanguage()
    {
        string file = Path.Combine(Path.GetTempPath(), "gw-stamp-" + Guid.NewGuid().ToString("N")[..8] + ".png");
        File.WriteAllText(file, "not a picture");
        try
        {
            using var pseudo = new PseudoLang();
            var w = ShownWindow();
            int stamps = w.Tools.StampList.Count;

            w.LoadStamp(file);

            Assert.Equal(PseudoLang.Wrap("That picture could not be read."), w.MessageText.Text);
            Assert.Equal("That picture could not be read.", w.LastMessageEn);
            Assert.Equal(stamps, w.Tools.StampList.Count);
        }
        finally
        {
            File.Delete(file);
        }
    }

    // A stamp picture that cannot be opened (gone since it was chosen, a folder, in use) or is too large to read: said like one
    // that is not a picture, no stamp added, and logged with what failed but not where the file is.
    [AvaloniaFact]
    public void AStampPictureThatCannotBeOpenedIsSaidAndLoggedWithoutItsPath()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gw-stamp-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        // Sparse: no disk space taken.
        string large = Path.Combine(dir, "large.png");
        using (var f = File.Create(large))
        {
            f.SetLength(Stamps.MostPictureBytes + 1);
        }
        // In use by another program: opened without sharing until the end.
        string locked = Path.Combine(dir, "locked.png");
        var holder = new FileStream(locked, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        var stderr = Console.Error;
        using var log = new StringWriter();
        try
        {
            using var pseudo = new PseudoLang();
            var w = ShownWindow();
            int stamps = w.Tools.StampList.Count;
            Console.SetError(TextWriter.Synchronized(log));

            foreach (string path in new[] { Path.Combine(dir, "gone.png"), dir, large, locked })
            {
                w.MessageText.Text = "";
                w.LoadStamp(path);

                Assert.Equal(PseudoLang.Wrap("That picture could not be read."), w.MessageText.Text);
                Assert.Equal("That picture could not be read.", w.LastMessageEn);
                Assert.Equal(stamps, w.Tools.StampList.Count);
            }
        }
        finally
        {
            Console.SetError(stderr);
            holder.Dispose();
            Directory.Delete(dir, recursive: true);
        }
        string logged = log.ToString();
        // (The exceptions' own messages may be in the system's language.)
        Assert.Contains("while reading the stamp picture to load (not loaded): System.IO.FileNotFoundException: ", logged);
        Assert.Contains("while reading the stamp picture to load (not loaded): System.UnauthorizedAccessException: ", logged);
        Assert.Contains("while reading the stamp picture to load (not loaded): System.IO.IOException: ", logged);
        Assert.Contains($"Load stamp: the stamp picture is not read: {Stamps.MostPictureBytes + 1} bytes, more than {Stamps.MostPictureBytes}.", logged);
        Assert.DoesNotContain(dir, logged);
    }
}
