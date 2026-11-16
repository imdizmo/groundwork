using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Groundwork.Desktop;

/// <summary>What the log says of memory: the limit .NET sees and whether a job holds the process, and the use now.</summary>
internal static partial class Memory
{
    private static string Gb(long bytes) => $"{bytes / (double)(1L << 30):0.0} GB";

    /// <summary>The memory .NET may use (a job's or container's limit if it saw one, else the computer's), and on Windows whether the process is in a job.</summary>
    public static string Limits()
    {
        string job = "";
        if (OperatingSystem.IsWindows())
        {
            job = IsProcessInJob(Process.GetCurrentProcess().Handle, IntPtr.Zero, out bool inJob) ? $"; in a job: {(inJob ? "yes" : "no")}" : "; in a job: unknown";
        }
        return $"memory: {Gb(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes)} available to .NET{job}";
    }

    /// <summary>The process's memory now and at its peak, and the .NET heap's share.</summary>
    public static string Now()
    {
        using var p = Process.GetCurrentProcess();
        return $"memory: {Gb(p.WorkingSet64)} in use (peak {Gb(p.PeakWorkingSet64)}), {Gb(p.PrivateMemorySize64)} committed, .NET heap {Gb(GC.GetTotalMemory(false))} ({Gb(GC.GetGCMemoryInfo().TotalCommittedBytes)} committed by it)";
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsProcessInJob(IntPtr process, IntPtr job, [MarshalAs(UnmanagedType.Bool)] out bool result);
}
