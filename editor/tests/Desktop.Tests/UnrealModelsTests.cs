using System.Text.Json.Nodes;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Bringing a map's glTF models into Unreal: only the ones names.json has no mesh for are sent, and Unreal's answer is added to it.</summary>
public sealed class UnrealModelsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gw-umodels-").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void OnlyModelsWithoutAMeshAreSentAndTheAnswerIsKept()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "pack"));
        File.WriteAllText(Path.Combine(_dir, "Oak.gltf"), "{}");
        File.WriteAllText(Path.Combine(_dir, "pack", "Hut.glb"), "");
        File.WriteAllText(Path.Combine(_dir, "names.json"), "{\"Oak\": \"/Game/Kit/Oak.Oak\", \"Rock\": \"/Game/Rock.Rock\"}");

        var send = UnrealLive.ModelsToImport(_dir);
        Assert.Equal([Path.Combine(_dir, "pack", "Hut.glb")], send);

        UnrealLive.KeepMeshes(_dir, new JsonObject { ["Hut"] = "/Game/Groundwork/Models/Hut/Hut.Hut" });
        var names = JsonNode.Parse(File.ReadAllText(Path.Combine(_dir, "names.json")))!.AsObject();
        Assert.Equal(("/Game/Kit/Oak.Oak", "/Game/Rock.Rock", "/Game/Groundwork/Models/Hut/Hut.Hut"), (names["Oak"]!.GetValue<string>(), names["Rock"]!.GetValue<string>(), names["Hut"]!.GetValue<string>()));
        Assert.Empty(UnrealLive.ModelsToImport(_dir));
    }
}
