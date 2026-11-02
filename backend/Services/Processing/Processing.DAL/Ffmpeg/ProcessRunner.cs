using System.Diagnostics;

namespace Processing.DAL.Ffmpeg;

/// <summary>Runs an external program (ffmpeg / ffprobe), streaming its stdout line by line.</summary>
internal static class ProcessRunner
{
    public sealed record Outcome(int ExitCode, string StdOut, string StdErrTail);

    public static async Task<Outcome> RunAsync(string executable, IEnumerable<string> arguments,
        Func<string, Task>? onStdOutLine, CancellationToken ct, bool captureStdOut = false)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        // ArgumentList quotes each argument for us — paths with spaces just work.
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException($"Could not start '{executable}'. Is FFmpeg installed and on PATH?", ex);
        }

        var stdout = new System.Text.StringBuilder();
        var stderrTail = new Queue<string>();

        var readOut = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
            {
                if (captureStdOut) stdout.AppendLine(line);
                if (onStdOutLine is not null) await onStdOutLine(line);
            }
        }, ct);

        // stderr must be drained too, or ffmpeg blocks once the pipe buffer fills up.
        var readErr = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync(ct) is { } line)
            {
                stderrTail.Enqueue(line);
                if (stderrTail.Count > 15) stderrTail.Dequeue();
            }
        }, ct);

        try
        {
            await process.WaitForExitAsync(ct);
            await Task.WhenAll(readOut, readErr);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            throw;
        }

        return new Outcome(process.ExitCode, stdout.ToString(), string.Join(Environment.NewLine, stderrTail));
    }
}
