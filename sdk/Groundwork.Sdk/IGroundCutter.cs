using System.Collections.Generic;

namespace Groundwork.Sdk;

/// <summary>Gives where the ground is not drawn for an object (see <see cref="IPluginRegistry.AddGroundCuts"/>).</summary>
public interface IGroundCutter
{
    /// <summary>The object's cuts (none when the ground stays whole).</summary>
    /// <param name="obj">The object, as drawn.</param>
    /// <param name="area">The area around it (its ground, its other objects).</param>
    IEnumerable<GroundCut> CutsOf(IObjectView obj, IAreaView area);
}
