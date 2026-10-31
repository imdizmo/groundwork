
namespace Groundwork.Desktop;

/// <summary>
/// The rest of what the driver's state answers with: the models.
/// </summary>
public static partial class Driver
{
    /// <summary>
    /// Adds the models' fields to what State answers with.
    /// </summary>
    /// <param name="w">The window.</param>
    /// <param name="fields">The fields so far.</param>
    private static void AddMapFields(MainWindow w, Dictionary<string, object?> fields)
    {
        fields["models"] = w.View.Models != null;
        fields["modelParts"] = w.View.LastStats?.Instances ?? 0;
        fields["modelsLoading"] = w.View.LastStats?.PendingModels ?? 0;
    }
}
