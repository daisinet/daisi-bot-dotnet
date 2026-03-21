using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DaisiBot.Agent.Minion;

/// <summary>
/// Tracks spawned headless minion processes and their status.
/// </summary>
public sealed class MinionProcessManager : IDisposable
{
    private readonly ConcurrentDictionary<string, MinionProcessInfo> _minions = new();
    private readonly ILogger<MinionProcessManager> _logger;

    public IReadOnlyDictionary<string, MinionProcessInfo> Minions => _minions;

    public MinionProcessManager(ILogger<MinionProcessManager> logger)
    {
        _logger = logger;
    }

    public MinionProcessInfo SpawnMinion(string role, string goal, int serverPort, string? workingDirectory = null)
    {
        var id = $"{role}-{_minions.Count + 1}";
        var outputDir = Path.Combine(workingDirectory ?? Directory.GetCurrentDirectory(), ".minion", id);
        Directory.CreateDirectory(outputDir);

        // Find the executable
        var exePath = Process.GetCurrentProcess().MainModule?.FileName
            ?? Path.Combine(AppContext.BaseDirectory, "DaisiBot.Tui.exe");

        var arguments = $"--server localhost:{serverPort} --headless --role {role} --goal \"{goal.Replace("\"", "\\\"")}\" --id {id}";

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var info = new MinionProcessInfo
        {
            Id = id,
            Role = role,
            Goal = goal,
            OutputDir = outputDir,
            Process = process,
            Status = MinionStatus.Starting,
            StartedAt = DateTime.UtcNow
        };

        process.Exited += (_, _) =>
        {
            info.Status = process.ExitCode == 0 ? MinionStatus.Complete : MinionStatus.Failed;
            info.ExitCode = process.ExitCode;
            info.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Minion {Id} exited with code {ExitCode}", id, process.ExitCode);
        };

        try
        {
            process.Start();
            info.Status = MinionStatus.Running;
            _logger.LogInformation("Spawned minion {Id}: role={Role}, goal={Goal}", id, role, goal);

            // Capture stdout/stderr to output files
            _ = CaptureOutputAsync(process, outputDir);
        }
        catch (Exception ex)
        {
            info.Status = MinionStatus.Failed;
            _logger.LogError(ex, "Failed to spawn minion {Id}", id);
        }

        _minions[id] = info;
        return info;
    }

    public string? GetMinionOutput(string id)
    {
        if (!_minions.TryGetValue(id, out var info))
            return null;

        var outputPath = Path.Combine(info.OutputDir, "output.log");
        return File.Exists(outputPath) ? File.ReadAllText(outputPath) : "(no output yet)";
    }

    public string? GetMinionStatus(string id)
    {
        if (!_minions.TryGetValue(id, out var info))
            return null;

        var statusPath = Path.Combine(info.OutputDir, "status");
        if (File.Exists(statusPath))
            return File.ReadAllText(statusPath).Trim();

        return info.Status.ToString().ToLower();
    }

    public bool StopMinion(string id)
    {
        if (!_minions.TryGetValue(id, out var info))
            return false;

        if (info.Process is { HasExited: false })
        {
            try
            {
                info.Process.Kill(entireProcessTree: true);
                info.Status = MinionStatus.Stopped;
                info.CompletedAt = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error killing minion {Id}", id);
            }
        }
        return false;
    }

    public bool SendMessage(string fromId, string toId, string content)
    {
        if (!_minions.TryGetValue(toId, out var info))
            return false;

        var inboxPath = Path.Combine(info.OutputDir, "inbox.json");
        var message = new { from = fromId, content, timestamp = DateTime.UtcNow.ToString("O") };
        var json = System.Text.Json.JsonSerializer.Serialize(message);

        // Append to inbox file with locking
        using var fs = new FileStream(inboxPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var sw = new StreamWriter(fs);
        sw.WriteLine(json);

        return true;
    }

    public void StopAll()
    {
        foreach (var id in _minions.Keys.ToList())
        {
            StopMinion(id);
        }
    }

    private static async Task CaptureOutputAsync(Process process, string outputDir)
    {
        var stdoutPath = Path.Combine(outputDir, "stdout.log");
        var stderrPath = Path.Combine(outputDir, "stderr.log");

        var stdoutTask = Task.Run(async () =>
        {
            try
            {
                using var writer = new StreamWriter(stdoutPath);
                while (await process.StandardOutput.ReadLineAsync() is { } line)
                    await writer.WriteLineAsync(line);
            }
            catch { }
        });

        var stderrTask = Task.Run(async () =>
        {
            try
            {
                using var writer = new StreamWriter(stderrPath);
                while (await process.StandardError.ReadLineAsync() is { } line)
                    await writer.WriteLineAsync(line);
            }
            catch { }
        });

        await Task.WhenAll(stdoutTask, stderrTask);
    }

    public void Dispose()
    {
        StopAll();
        foreach (var info in _minions.Values)
        {
            info.Process?.Dispose();
        }
    }
}

public sealed class MinionProcessInfo
{
    public required string Id { get; init; }
    public required string Role { get; init; }
    public required string Goal { get; init; }
    public required string OutputDir { get; init; }
    public Process? Process { get; init; }
    public MinionStatus Status { get; set; }
    public int? ExitCode { get; set; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; set; }
}

public enum MinionStatus
{
    Starting,
    Running,
    Complete,
    Failed,
    Stopped
}
