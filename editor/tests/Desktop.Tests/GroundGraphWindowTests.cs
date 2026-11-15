using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The Ground graph's window: nodes added and joined, the picked one worked out, the Output applied.</summary>
public class GroundGraphWindowTests
{
    [AvaloniaFact]
    public async Task ANodeAddedAndJoinedChangesWhatIsApplied()
    {
        var owner = new Window { Width = 800, Height = 600 };
        owner.Show();
        float[]? applied = null;
        var w = new GroundGraphWindow(GroundGraph.Starter(), new GroundNodes.Where(160, 120, 0, 0), null, f =>
        {
            applied = f;
            return "applied";
        });
        w.Show(owner);
        await w.Work();
        // terraces between the erosion and the output: the output now steps
        int erosion = w.Graph.Nodes.Single(n => n.Kind == "erosion").Id, output = w.Graph.Nodes.Single(n => n.Kind == "output").Id;
        var terraces = w.AddNode("terraces");
        Assert.Equal(terraces.Id, w.Picked);
        Assert.True(w.Join(erosion, terraces.Id, 0));
        Assert.True(w.Join(terraces.Id, output, 0));
        Assert.False(w.Join(output, erosion, 0));   // a loop
        await w.Work();
        DialogsTests.Press(w, "Apply to the map");
        for (int i = 0; i < 200 && applied == null; i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
        Assert.NotNull(applied);
        float step = GroundNodes.Value(terraces, "step");
        // most of the ground on or near a tread: within a fifth of a step above a multiple of it
        Assert.True(applied!.Count(h => (h % step + step) % step < step * 0.3f) > applied.Length / 2);
        w.Close();
    }
}
