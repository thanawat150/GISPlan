using System.Diagnostics;
using System.Text;

namespace GISPlan.Core;

public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            return new ProcessResult { ExitCode = -1, StandardError = $"ไม่พบโปรแกรม: {executable}" };

        var psi = CreateStartInfo(executable, arguments, workingDirectory);
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        try
        {
            if (!process.Start())
                return new ProcessResult { ExitCode = -1, StandardError = "ไม่สามารถเริ่มโปรแกรมภายนอกได้" };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort cancellation.
                }
            });

            await process.WaitForExitAsync(cancellationToken);
            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdout.ToString(),
                StandardError = stderr.ToString()
            };
        }
        catch (OperationCanceledException)
        {
            return new ProcessResult { ExitCode = -2, StandardOutput = stdout.ToString(), StandardError = "cancelled" };
        }
        catch (Exception ex)
        {
            return new ProcessResult { ExitCode = -1, StandardOutput = stdout.ToString(), StandardError = ex.Message };
        }
    }

    private static ProcessStartInfo CreateStartInfo(
        string executable,
        IEnumerable<string> arguments,
        string? workingDirectory)
    {
        var extension = Path.GetExtension(executable);
        if (extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            var command = QuoteForCmd(executable) + " " + string.Join(" ", arguments.Select(QuoteForCmd));
            return new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe",
                Arguments = $"/d /s /c \"{command}\"",
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        }

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        return psi;
    }

    private static string QuoteForCmd(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
