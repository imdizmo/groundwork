using System.Reflection;
using Groundwork.App;
using Groundwork.Sdk;
using Xunit;

namespace Groundwork.Tests;

/// <summary>
/// The load context an extension DLL is loaded in: the SDK is the editor's own assembly, so the extension and the
/// editor share one set of its types, and any other assembly is left to the framework (not loaded from the folder).
/// </summary>
public sealed class ModExtensionLoadContextTests
{
    /// <summary>The SDK's assembly name resolves to the editor's own loaded SDK.</summary>
    [Fact]
    public void TheSdkResolvesToTheEditorsOwnAssembly()
    {
        object context = PrivateMember.New(PrivateMember.Nested(typeof(ModExtensions), "ExtensionContext"), "Mod.dll");
        string? sdk = typeof(IGroundworkPlugin).Assembly.GetName().Name;

        object? loaded = PrivateMember.Call(context.GetType(), "Load", context, new AssemblyName(sdk!));

        Assert.Same(typeof(IGroundworkPlugin).Assembly, loaded);
    }

    /// <summary>Any other assembly is not resolved by the context, so it is not loaded from the extension's folder.</summary>
    [Fact]
    public void AnotherAssemblyIsNotResolvedByTheContext()
    {
        object context = PrivateMember.New(PrivateMember.Nested(typeof(ModExtensions), "ExtensionContext"), "Mod.dll");

        object? loaded = PrivateMember.Call(context.GetType(), "Load", context, new AssemblyName("Helper.Other"));

        Assert.Null(loaded);
    }
}
