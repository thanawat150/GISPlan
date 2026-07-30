namespace GISPlan.Core;

public static class AppPaths
{
    public static string RuntimeRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GISPlan");

    public static string JobsRoot => Path.Combine(RuntimeRoot, "jobs");
    public static string LogsRoot => Path.Combine(RuntimeRoot, "logs");
    public static string CacheRoot => Path.Combine(RuntimeRoot, "cache");
    public static string SettingsRoot => Path.Combine(RuntimeRoot, "settings");
    public static string RuntimeConfigPath => Path.Combine(SettingsRoot, "local_runtime.json");

    public static string DesktopRoot => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public static string DefaultOutputRoot => Path.Combine(DesktopRoot, "GISPlan_Output");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(RuntimeRoot);
        Directory.CreateDirectory(JobsRoot);
        Directory.CreateDirectory(LogsRoot);
        Directory.CreateDirectory(CacheRoot);
        Directory.CreateDirectory(SettingsRoot);
        Directory.CreateDirectory(DefaultOutputRoot);
    }

    public static string GetJobDirectory(string jobId)
    {
        var safe = string.Concat(jobId.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = Path.Combine(JobsRoot, safe);
        Directory.CreateDirectory(path);
        return path;
    }
}

public static class OutputPathResolver
{
    public static string Resolve(string requestedPath, bool overwrite)
    {
        if (overwrite || !File.Exists(requestedPath))
            return requestedPath;

        var directory = Path.GetDirectoryName(requestedPath) ?? Directory.GetCurrentDirectory();
        var name = Path.GetFileNameWithoutExtension(requestedPath);
        var extension = Path.GetExtension(requestedPath);

        for (var version = 2; version < 10_000; version++)
        {
            var candidate = Path.Combine(directory, $"{name}_v{version}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }

        throw new IOException("ไม่สามารถสร้างชื่อ Output แบบ Versioned ได้");
    }
}
