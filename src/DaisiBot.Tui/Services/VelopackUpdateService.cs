using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace DaisiBot.Tui.Services;

/// <summary>
/// Checks for and applies self-updates via Velopack for the TUI bot.
/// </summary>
public static class VelopackUpdateService
{
    /// <summary>
    /// Checks for a newer version on Azure Blob Storage and applies it with restart.
    /// </summary>
    public static async Task<bool> CheckAndApplyUpdateAsync(string channel = "production")
    {
        try
        {
            var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
            var url = $"https://daisi.blob.core.windows.net/releases/velopack-bot/tui/{channel}/{rid}";
            var source = new SimpleWebSource(url);
            var mgr = new UpdateManager(source);

            if (!mgr.IsInstalled)
            {
                Console.WriteLine("Velopack: App is not Velopack-installed. Skipping update.");
                return false;
            }

            var updateInfo = await mgr.CheckForUpdatesAsync();
            if (updateInfo == null)
            {
                Console.WriteLine("Velopack: No updates available.");
                return false;
            }

            Console.WriteLine($"Velopack: Update available — {updateInfo.TargetFullRelease.Version}");
            await mgr.DownloadUpdatesAsync(updateInfo);
            mgr.ApplyUpdatesAndRestart(updateInfo);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Velopack update error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Prints assembly version and Velopack version banner to console.
    /// </summary>
    public static void ShowVersionNumber()
    {
        var assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

        Console.WriteLine("==================================");
        Console.WriteLine($"DAISI BOT v{assemblyVersion}");

        try
        {
            var mgr = new UpdateManager("https://localhost");
            if (mgr.CurrentVersion is { } veloVersion)
                Console.WriteLine($"Velopack Version: {veloVersion}");
        }
        catch
        {
            // Not a Velopack-installed app, skip
        }

        Console.WriteLine($"Copyright (c) {DateTime.UtcNow.Year} Distributed AI Systems, Inc. All Rights Reserved.");
        Console.WriteLine("==================================");
    }
}
