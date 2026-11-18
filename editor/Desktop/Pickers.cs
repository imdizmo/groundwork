using Avalonia.Platform.Storage;

namespace Groundwork.Desktop;

/// <summary>The system's file and folder pickers, giving a local path.</summary>
internal static class Pickers
{
    /// <summary>A file chosen with the system's picker.</summary>
    /// <param name="storage">The window's storage provider.</param>
    /// <param name="title">The picker's title.</param>
    /// <param name="type">The kind of file offered (null: any).</param>
    /// <returns>Its local path; null when cancelled or not a local file.</returns>
    internal static async Task<string?> File(IStorageProvider storage, string title, FilePickerFileType? type = null) =>
        LocalPath(await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, FileTypeFilter = type == null ? null : [type] }));

    /// <summary>A file to write, chosen with the system's save picker.</summary>
    /// <param name="storage">The window's storage provider.</param>
    /// <param name="title">The picker's title.</param>
    /// <param name="name">The name offered.</param>
    /// <param name="types">The kinds of file offered.</param>
    /// <returns>Its local path; null when cancelled or not a local file.</returns>
    internal static async Task<string?> SaveFile(IStorageProvider storage, string title, string name, IReadOnlyList<FilePickerFileType> types) =>
        await storage.SaveFilePickerAsync(new FilePickerSaveOptions { Title = title, SuggestedFileName = name, FileTypeChoices = types }) is { Path.IsFile: true } f ? f.Path.LocalPath : null;

    /// <summary>Files chosen with the system's picker, several at once (none: cancelled; only files on this machine).</summary>
    /// <param name="storage">The window's storage provider.</param>
    /// <param name="title">The picker's title.</param>
    /// <param name="type">The kind of file offered.</param>
    internal static async Task<List<string>> Files(IStorageProvider storage, string title, FilePickerFileType type) =>
        [.. (await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = true, FileTypeFilter = [type] })).Where(f => f.Path.IsFile).Select(f => f.Path.LocalPath)];

    /// <summary>A folder chosen with the system's picker.</summary>
    /// <param name="storage">The window's storage provider.</param>
    /// <param name="title">The picker's title.</param>
    /// <returns>Its local path; null when cancelled or not a local folder.</returns>
    internal static async Task<string?> Folder(IStorageProvider storage, string title) =>
        LocalPath(await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title }));

    /// <summary>The first item's local path (null: none, or not on this machine).</summary>
    /// <param name="picked">What the picker gave.</param>
    private static string? LocalPath(IReadOnlyList<IStorageItem> picked) =>
        picked.Count > 0 && picked[0].Path.IsFile ? picked[0].Path.LocalPath : null;
}
