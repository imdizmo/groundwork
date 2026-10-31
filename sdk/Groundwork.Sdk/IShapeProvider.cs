using System.Collections.Generic;

namespace Groundwork.Sdk;

/// <summary>Gives the shapes the editor draws for an object besides its model (see <see cref="IPluginRegistry.AddShapes"/>).</summary>
public interface IShapeProvider
{
    /// <summary>The object's shapes, in its own frame unless a shape says otherwise (none when there is nothing to draw).</summary>
    /// <param name="obj">The object, as drawn.</param>
    /// <param name="area">The area around it (its ground, its other objects).</param>
    IEnumerable<Shape> ShapesOf(IObjectView obj, IAreaView area);
}
