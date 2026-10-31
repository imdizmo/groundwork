
namespace Groundwork.Desktop;

public sealed partial class ToolPanel
{
    /// <summary>The rail's keys (Avalonia key names, digits as "0".."9"); true when the key was one.</summary>
    public bool Key(string key)
    {
        if (key is "Escape" or "H")
        {
            Choose(null);
            return true;
        }
        if (key == "E")
        {
            ChooseSelect();
            return true;
        }
        if (key == "M")
        {
            ChooseMode(ToolMode.Measure);
            return true;
        }
        foreach (var (t, k) in Keys)
        {
            if (k == key)
            {
                Choose(t);
                return true;
            }
        }
        return false;
    }
}
