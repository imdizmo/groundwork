using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Load stamp…: a grayscale picture chosen from the computer's files becomes a brush shape.
    /// </summary>
    private async Task LoadStampAsync()
    {
        if (await Pickers.File(StorageProvider, Lang.T("brush.pickStamp"),
            new Avalonia.Platform.Storage.FilePickerFileType(Lang.T("brush.pictures")) { Patterns = PicturePatterns }) is not string path)
        {
            return;
        }
        LoadStamp(path);
    }

    /// <summary>
    /// The picture at <paramref name="path"/> added to the brush's stamps and chosen, or said in the status bar that it
    /// could not be read.
    /// </summary>
    /// <param name="path">The picture file.</param>
    internal void LoadStamp(string path)
    {
        if (ReadStampFile(path) is { } bytes && Stamps.FromPicture(bytes) is { } data)
        {
            Tools.AddStamp(Path.GetFileNameWithoutExtension(path), data);
        }
        else
        {
            Msg("brush.msg.unreadable");
        }
    }

    /// <summary>
    /// The stamp picture's contents, or null, logged without its path, when it cannot be opened (gone since it was
    /// chosen, not allowed, a folder, in use) or is larger than <see cref="Stamps.MostPictureBytes"/>.
    /// </summary>
    /// <param name="path">The picture file.</param>
    private static byte[]? ReadStampFile(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            if (file.Length > Stamps.MostPictureBytes)
            {
                Console.Error.WriteLine($"Load stamp: the stamp picture is not read: {file.Length} bytes, more than {Stamps.MostPictureBytes}.");
                return null;
            }
            byte[] bytes = new byte[file.Length];
            file.ReadExactly(bytes);
            return bytes;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // The path may hold the user's name: Log takes it out of the message.
            Log.Caught(ex, "reading the stamp picture to load (not loaded)");
            return null;
        }
    }

    /// <summary>
    /// Alt + click with a brush: the ground's height there becomes Flatten's target, or with Shift the mask's band.
    /// </summary>
    /// <param name="h">The ground's height clicked.</param>
    /// <param name="shift">Shift held: the mask's band instead.</param>
    private void OnBrushAltClick(float h, bool shift)
    {
        if (shift)
        {
            Mask.PickHeight(h);
            Msg("brush.msg.maskBand", h - 2, h + 2);
        }
        else
        {
            Tools.FlattenTo(h);
            Msg("brush.msg.flattenTo", h);
        }
    }

    /// <summary>
    /// The Area tool's heightmap picker's title and file type, in the language in use (asked for each time it opens).
    /// </summary>
    internal static (string Title, Avalonia.Platform.Storage.FilePickerFileType Type) HeightmapPicker()
    {
        var type = new Avalonia.Platform.Storage.FilePickerFileType(Lang.T("area.pick.png")) { Patterns = PngPatterns };
        return (Lang.T("area.pick.picture"), type);
    }
}
