namespace Groundwork.Sdk;

/// <summary>
/// A plugin's entry: made once when the editor loads the plugin, then asked to register its hooks.
/// </summary>
public interface IGroundworkPlugin
{
    /// <summary>Registers the plugin's hooks. Called once, before any hook is used.</summary>
    /// <param name="registry">Where hooks are registered.</param>
    void Register(IPluginRegistry registry);
}
