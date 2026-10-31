using System.Numerics;
using Groundwork.App;
using Groundwork.Sdk;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The SDK's sample plugin (sdk/samples/Fences) in the editor: its tool's clicks become posts with their data, one step
/// each, and the editor draws a rail from a post to the next one of its fence, none after the last.
/// </summary>
[Collection("DataDir")]
public sealed class FencesSampleTests : IDisposable
{
    public void Dispose() => ModExtensions.Forget();

    [Fact]
    public void ClicksMakePostsAndTheEditorDrawsTheRailsBetweenThem()
    {
        ModExtensions.UseForTests(("Fences", new Fences.Plugin()));
        var s = TestScenes.Flat(1);
        var ctx = new Context(s);
        var tool = new Fences.FenceTool();
        tool.OnChosen(ctx);
        foreach (var at in new[] { new Vector3(5, 30, 5), new Vector3(9, 30, 5) })
        {
            tool.OnPointer(ctx, new PointerInput(PointerAction.Press, KeyModifiers.None, at + Vector3.UnitY, -Vector3.UnitY) { Target = PointerTarget.Ground, Point = at });
        }

        var posts = s.Scene.Things.Where(t => !t.Gone && AssetIds.NameOf(t.Prefab) == Fences.Plugin.Post).Select(s.ViewOf).ToList();
        Assert.Equal(2, posts.Count);
        Assert.Equal(posts[0].Data["fence"], posts[1].Data["fence"]);
        Assert.True(s.Undo());

        Assert.True(s.Redo());
        var area = new Area(posts);
        var first = posts.Single(p => p.Data["order"] == "0");
        var last = posts.Single(p => p.Data["order"] == "1");
        Assert.Contains(ModExtensions.ShapesOf(AssetIds.Of(Fences.Plugin.Post), first, area), sh => sh.WorldSpace);
        Assert.DoesNotContain(ModExtensions.ShapesOf(AssetIds.Of(Fences.Plugin.Post), last, area), sh => sh.WorldSpace);
    }

    /// <summary>The editor for the tool: its commits made steps of the session's history.</summary>
    private sealed class Context(EditSession s) : IToolContext
    {
        public IAreaView Area => new PlainArea(0, 0, 63, 63, 30);
        public float Number(string id) => 1;
        public int Choice(string id) => 0;
        public bool IsOn(string id) => false;
        public void SetText(string id, string text) { }
        public void SetNumber(string id, float value) { }
        public void SetList(string id, IReadOnlyList<(string Title, string Sub, string Tag)> items, int picked) { }
        public void Preview(IReadOnlyList<Shape> shapes) { }
        public void Status(string text) { }
        public void Commit(string label, ToolEdit edit) => ToolCommit.Apply(s, label, edit);
        public IReadOnlyList<int> Picked(string id) => [];
    }

    /// <summary>An area holding the posts.</summary>
    private sealed class Area(IReadOnlyList<IObjectView> posts) : IAreaView
    {
        public float MinX => 0;
        public float MinZ => 0;
        public float MaxX => 63;
        public float MaxZ => 63;
        public IReadOnlyList<IObjectView> ObjectsOf(string asset) => asset == Fences.Plugin.Post ? posts : [];
        public float GroundHeight(float x, float z) => 30;
        public int Layer(float x, float z) => 0;
        public Vector3 GroundColor(int x, int z) => Vector3.One;
        public uint GroundMask(int x, int z) => 0;
    }
}
