using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// --driver's loop: one answer per line read on its input, and the window closed after quit.
/// </summary>
[Collection("World files")]
public class DriverTests
{

    /// <summary>
    /// The console's lines as they are written, for the driver's answers.
    /// </summary>
    private sealed class Lines : TextWriter
    {
        private readonly StringBuilder _line = new();

        public ConcurrentQueue<string> All { get; } = new();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            lock (_line)
            {
                if (value == '\n')
                {
                    All.Enqueue(_line.ToString().TrimEnd('\r'));
                    _line.Clear();
                }
                else
                {
                    _line.Append(value);
                }
            }
        }
    }

    [AvaloniaFact]
    public async Task TheLoopAnswersEachLineAndClosesTheWindowAfterQuit()
    {
        var w = new MainWindow(load: false) { Width = 1200, Height = 800 };
        w.Show();
        var input = Console.In;
        var output = Console.Out;
        var lines = new Lines();
        try
        {
            Console.SetIn(new StringReader("state\n\nfly\nquit\n"));
            Console.SetOut(lines);
            bool closed = false;
            w.Closed += (_, _) => closed = true;
            Driver.Start(w);
            await TestWindows.Until(() => closed);
            var answers = lines.All.Where(l => l.StartsWith("@@", StringComparison.Ordinal)).ToList();
            Assert.Equal("@@ ready", answers[0]);
            // The empty line is skipped: state, the error, quit.
            Assert.StartsWith("@@ ok {", answers[1]);
            Assert.Equal("start", JsonElement.Parse(answers[1]["@@ ok ".Length..]).GetProperty("page").GetString());
            Assert.Equal("@@ error unknown command fly", answers[2]);
            Assert.StartsWith("@@ ok {", answers[3]);
            Assert.Equal(4, answers.Count);
        }
        finally
        {
            Console.SetIn(input);
            Console.SetOut(output);
        }
    }
}
