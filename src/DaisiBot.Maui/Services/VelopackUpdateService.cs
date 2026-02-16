#if WINDOWS
using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace DaisiBot.Maui.Services;

/// <summary>
/// Checks for and applies self-updates via Velopack for the MAUI bot (Windows only).
/// </summary>
public class VelopackUpdateService
{
    /// <summary>
    /// Checks for a newer version on Azure Blob Storage and applies it with restart.
    /// </summary>
    public async Task<bool> CheckAndApplyUpdateAsync(string channel = "production")
    {
        try
        {
            var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
            var url = $"https://daisi.blob.core.windows.net/releases/velopack-bot/maui/{channel}/{rid}";
            var source = new SimpleWebSource(url);
            var mgr = new UpdateManager(source);

            if (!mgr.IsInstalled)
                return false;

            var updateInfo = await mgr.CheckForUpdatesAsync();
            if (updateInfo == null)
                return false;

            await mgr.DownloadUpdatesAsync(updateInfo);
            mgr.ApplyUpdatesAndRestart(updateInfo);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Velopack update error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Returns the current assembly version string.
    /// </summary>
    public string GetVersionString()
    {
        var assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        try
        {
            var mgr = new UpdateManager("https://localhost");
            if (mgr.CurrentVersion is { } veloVersion)
                return $"{assemblyVersion} (Velopack: {veloVersion})";
        }
        catch { }
        return assemblyVersion;
    }
}
#endif
