namespace KsaDevPanel;

internal static class IsolatedProfile
{
    public static bool TryGetSettingsPath(out string settingsPath)
    {
        settingsPath = "";
        var requested = Environment.GetEnvironmentVariable("KSA_DEV_PANEL_PROFILE");
        var loader = Environment.GetEnvironmentVariable("STARMAP_INSTANCE_PATH");

        if (!Path.IsPathFullyQualified(requested ?? "") ||
            !Path.IsPathFullyQualified(loader ?? ""))
            return false;

        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(requested!));
            if (!SamePath(root, loader!) || !SamePath(root, KSA.Constants.DocumentsFolderPath))
                return false;

            var normal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games", "Kitten Space Agency");
            var relative = Path.GetRelativePath(normal, root);
            if (!Path.IsPathRooted(relative) && relative != ".." &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                return false;

            for (var folder = new DirectoryInfo(root); folder is not null; folder = folder.Parent)
            {
                if (!folder.Exists || (folder.Attributes & FileAttributes.ReparsePoint) != 0)
                    return false;
            }

            var sentinel = new FileInfo(Path.Combine(root, ".ksa-dev-panel-isolated"));
            if (!sentinel.Exists || sentinel.Length > 128 ||
                (sentinel.Attributes & FileAttributes.ReparsePoint) != 0 ||
                File.ReadAllText(sentinel.FullName).Trim() != $"KsaDevPanel {DevPanelMod.GameBuild}")
                return false;

            settingsPath = Path.Combine(root, "KsaDevPanel.settings.json");
            return !Path.Exists(settingsPath) ||
                (File.GetAttributes(settingsPath) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static bool SamePath(string first, string second)
    {
        return string.Equals(first, Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            StringComparison.OrdinalIgnoreCase);
    }
}
