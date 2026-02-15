using NuGet.Versioning;

namespace DaisiBot.Tui.Services;

/// <summary>
/// Velopack install/update/uninstall lifecycle hooks for the TUI bot.
/// Kept minimal — bot doesn't need firewall rules or startup registry entries.
/// </summary>
public static class VelopackInstallHooks
{
    /// <summary>
    /// Called after first Velopack install.
    /// </summary>
    public static void OnAfterInstall(SemanticVersion v)
    {
    }

    /// <summary>
    /// Called after each Velopack update.
    /// </summary>
    public static void OnAfterUpdate(SemanticVersion v)
    {
    }

    /// <summary>
    /// Called before Velopack uninstall.
    /// </summary>
    public static void OnBeforeUninstall(SemanticVersion v)
    {
    }
}
