namespace DaisiBot.LocalTools.Tests.Helpers
{
    internal static class GitTestHelper
    {
        internal static void RunGit(string args, string workDir)
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", args)
            {
                WorkingDirectory = workDir,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            System.Diagnostics.Process.Start(psi)!.WaitForExit();
        }

        internal static void ForceDeleteDirectory(string path)
        {
            if (!Directory.Exists(path)) return;

            // Remove read-only attributes (Git marks .git files as read-only on Windows)
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                var attrs = File.GetAttributes(file);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(path, true);
        }
    }
}
