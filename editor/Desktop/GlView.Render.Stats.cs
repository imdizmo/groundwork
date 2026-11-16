using Avalonia.Threading;
using Silk.NET.OpenGL;

namespace Groundwork.Desktop;

// The frame-rate report: the frames over the last second, the log line per frame, and the OpenGL errors the test driver checks.
public sealed partial class GlView
{
    /// <summary>Counts a frame for the frame rate and the log, and makes a report twice a second.</summary>
    private void Measure(long now, double work, bool camMoved, int pw, int ph)
    {
        Interlocked.Exchange(ref _lastFrameAt, now);
        _frames.Add((now, work));
        while (_frames.Count > 0 && now - _frames[0].At > 1000)
        {
            _frames.RemoveAt(0);
        }
        Perf?.Frame(now, work, camMoved, busy: now - _wokeAt < 1000, $"view {pw}×{ph} px · {_gl.GetStringS(StringName.Renderer)} · {Scene?.Name} · {Scene?.Things.Count} objects · area {Scene?.ZonesX}×{Scene?.ZonesZ} zones");
        if (now - _statsAt >= 500)
        {
            _statsAt = now;
            var st = new Stats(_frames.Count, _frames.Average(f => f.Work), Scene?.Things.Count ?? 0, _batches.Count, _batches.Sum(b => b.Instances), _pending);
            LastStats = st;
            // the models all read: how much memory that took (the peak is the process's whole life's)
            if (_pending == 0 && _pendingWas > 0)
            {
                Options.Say($"models read; {Memory.Now()}");
                if (Interlocked.Exchange(ref _boxed, 0) is int boxed and > 0)
                {
                    string why = _boxedWhy;
                    Dispatcher.UIThread.Post(() => Boxed?.Invoke(boxed, why));
                }
            }
            _pendingWas = _pending;
            Dispatcher.UIThread.Post(() => StatsChanged?.Invoke(st));
        }
    }

    /// <summary>The models still being read at the last stats.</summary>
    private int _pendingWas;

    /// <summary>Frames drawn over the last second.</summary>
    internal int FramesDrawn => _frames.Count;

    /// <summary>The test driver checks drawing raised no OpenGL error (counted only when driven).</summary>
    internal int GlErrors { get; private set; }

    /// <summary>Counts (and says) the OpenGL errors of the frame, only when the test driver runs.</summary>
    private void CountGlErrors()
    {
        if (!Options.Driver)
        {
            return;
        }
        for (var e = _gl.GetError(); e != GLEnum.NoError; e = _gl.GetError())
        {
            GlErrors++;
            Options.Say($"OpenGL error {e}");
        }
    }
}
