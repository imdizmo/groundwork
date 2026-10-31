using System;

namespace Groundwork.Sdk;

/// <summary>
/// Marks an assembly as a Groundwork plugin and names its entry type, which the editor makes (it needs a
/// public constructor without parameters) and asks to register its hooks.
/// </summary>
/// <example><code>[assembly: GroundworkPlugin(typeof(MyPlugin.Entry))]</code></example>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class GroundworkPluginAttribute : Attribute
{
    /// <summary>Names the entry type.</summary>
    /// <param name="entry">A public class implementing <see cref="IGroundworkPlugin"/>.</param>
    public GroundworkPluginAttribute(Type entry)
    {
        Entry = entry;
    }

    /// <summary>The entry type: a public class implementing <see cref="IGroundworkPlugin"/>.</summary>
    public Type Entry { get; }

    /// <summary>The API version the plugin was built against (<see cref="SdkInfo.ApiVersion"/>, the default).</summary>
    public int ApiVersion { get; set; } = SdkInfo.ApiVersion;
}
