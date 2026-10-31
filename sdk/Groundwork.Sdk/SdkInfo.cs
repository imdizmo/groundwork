namespace Groundwork.Sdk;

/// <summary>The plugin API's version.</summary>
public static class SdkInfo
{
    /// <summary>
    /// The API version this SDK is: a plugin built against it runs in an editor whose
    /// <see cref="IPluginRegistry.ApiVersion"/> is this or later. Raised only when the API changes in a way that
    /// breaks plugins; additions keep it.
    /// </summary>
    public const int ApiVersion = 1;
}
