using Groundwork.Sdk;

namespace Groundwork.App;

/// <summary>
/// Running the extensions' hooks (one that throws turns its extension off), and the editor host they are given.
/// </summary>
public static partial class ModExtensions
{
    /// <summary>The editor as extensions ask it (IEditorHost).</summary>
    public static IEditorHost Host { get; } = new EditorHost();

    /// <summary>An area with nothing in it (flat at 0): what an object's shapes see where no area is meant (a ghost).</summary>
    public static IAreaView NoArea { get; } = new PlainArea(0, 0, 0, 0, 0);

    /// <summary>
    /// Runs one hook of an extension; when it throws, the extension is turned off (until the editor starts again) and
    /// <paramref name="fallback"/> is the result.
    /// </summary>
    private static T Guard<T>(Loaded l, Func<T> hook, T fallback)
    {
        if (l.Fault != null)
        {
            return fallback;
        }
        try
        {
            return hook();
        }
        catch (Exception e)
        {
            Log.Caught(e, $"running a hook of the extension of {l.Mod} (turned off)");
            TurnOff(l, $"{e.GetType().Name}: {e.Message}");
            return fallback;
        }
    }

    /// <summary>Turns an extension off after a hook threw: its hooks are no longer used.</summary>
    private static void TurnOff(Loaded l, string why)
    {
        lock (Lock)
        {
            if (l.Fault != null)
            {
                return;
            }
            l.Fault = why;
            Console.WriteLine($"extension {l.Mod}: turned off ({why}).");
            var s = _state;
            // Every path it runs from (the same file offered twice).
            var dlls = s.Running.Where(r => r.Value == l).Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
            _state = new State(
                [.. s.Offered.Select(o => dlls.Contains(o.Dll) ? o with { State = Status.Failed, Error = why } : o)],
                s.Running.Where(r => r.Value != l).ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal));
            Interlocked.Increment(ref _generation);
        }
    }
}
