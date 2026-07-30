using System.Text.Json;

namespace GISPlan.Core;

public sealed class RuntimeDetector
{
    public async Task<RuntimeInfo> DetectAsync(CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureCreated();

        var runtime = new RuntimeInfo
        {
            QgisProcessPath = FindExecutableOnPath("qgis_process.exe", "qgis_process-qgis.bat", "qgis_process-qgis-ltr.bat"),
            QgisGuiPath = FindExecutableOnPath("qgis-bin.exe", "qgis.exe", "qgis-ltr-bin.exe", "qgis-ltr.exe"),
            OgrInfoPath = FindExecutableOnPath("ogrinfo.exe"),
            Ogr2OgrPath = FindExecutableOnPath("ogr2ogr.exe"),
            GdalSrsInfoPath = FindExecutableOnPath("gdalsrsinfo.exe"),
            ArcGisPropyPath = FindExecutableOnPath("propy.bat"),
            PythonPath = FindExecutableOnPath("python.exe", "py.exe")
        };

        foreach (var qgisRoot in GetKnownQgisRoots())
        {
            cancellationToken.ThrowIfCancellationRequested();
            runtime.QgisProcessPath ??= FirstExisting(
                Path.Combine(qgisRoot, "bin", "qgis_process.exe"),
                Path.Combine(qgisRoot, "bin", "qgis_process-qgis.bat"),
                Path.Combine(qgisRoot, "bin", "qgis_process-qgis-ltr.bat"));
            runtime.QgisGuiPath ??= FirstExisting(
                Path.Combine(qgisRoot, "bin", "qgis-bin.exe"),
                Path.Combine(qgisRoot, "bin", "qgis.exe"),
                Path.Combine(qgisRoot, "bin", "qgis-ltr-bin.exe"),
                Path.Combine(qgisRoot, "bin", "qgis-ltr.exe"));
            runtime.OgrInfoPath ??= FirstExisting(Path.Combine(qgisRoot, "bin", "ogrinfo.exe"));
            runtime.Ogr2OgrPath ??= FirstExisting(Path.Combine(qgisRoot, "bin", "ogr2ogr.exe"));
            runtime.GdalSrsInfoPath ??= FirstExisting(Path.Combine(qgisRoot, "bin", "gdalsrsinfo.exe"));
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        runtime.ArcGisPropyPath ??= FirstExisting(
            Path.Combine(programFiles, "ArcGIS", "Pro", "bin", "Python", "Scripts", "propy.bat"));

        if (!runtime.HasQgis) runtime.Warnings.Add("ไม่พบ QGIS Processing");
        if (!runtime.HasQgisGui) runtime.Warnings.Add("ไม่พบ QGIS Desktop สำหรับเปิดไฟล์บนแผนที่");
        if (!runtime.HasGdal) runtime.Warnings.Add("ไม่พบ GDAL/OGR ครบชุด");
        if (!runtime.HasArcGis) runtime.Warnings.Add("ไม่พบ ArcGIS Pro Python หรือ License runtime");
        if (string.IsNullOrWhiteSpace(runtime.PythonPath)) runtime.Warnings.Add("ไม่พบ Python แบบ User/System PATH");

        await SaveAsync(runtime, cancellationToken);
        return runtime;
    }

    public async Task<RuntimeInfo?> LoadCachedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(AppPaths.RuntimeConfigPath)) return null;
            await using var stream = File.OpenRead(AppPaths.RuntimeConfigPath);
            var runtime = await JsonSerializer.DeserializeAsync<RuntimeInfo>(stream, JsonDefaults.Options, cancellationToken);
            if (runtime is null) return null;

            runtime.QgisProcessPath = ValidOrNull(runtime.QgisProcessPath);
            runtime.QgisGuiPath = ValidOrNull(runtime.QgisGuiPath);
            runtime.OgrInfoPath = ValidOrNull(runtime.OgrInfoPath);
            runtime.Ogr2OgrPath = ValidOrNull(runtime.Ogr2OgrPath);
            runtime.GdalSrsInfoPath = ValidOrNull(runtime.GdalSrsInfoPath);
            runtime.ArcGisPropyPath = ValidOrNull(runtime.ArcGisPropyPath);
            runtime.PythonPath = ValidOrNull(runtime.PythonPath);
            return runtime;
        }
        catch
        {
            return null;
        }
    }

    private static async Task SaveAsync(RuntimeInfo runtime, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.RuntimeConfigPath)!);
        await using var stream = File.Create(AppPaths.RuntimeConfigPath);
        await JsonSerializer.SerializeAsync(stream, runtime, JsonDefaults.Options, cancellationToken);
    }

    private static IEnumerable<string> GetKnownQgisRoots()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")
        };

        foreach (var root in roots.Where(Directory.Exists))
        {
            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(root, "QGIS*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories)
                yield return directory;
        }
    }

    private static string? FindExecutableOnPath(params string[] names)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in names)
            {
                try
                {
                    var candidate = Path.Combine(directory.Trim('"'), name);
                    if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                }
                catch
                {
                    // Ignore malformed PATH entries.
                }
            }
        }

        return null;
    }

    private static string? FirstExisting(params string[] candidates) =>
        candidates.FirstOrDefault(File.Exists);

    private static string? ValidOrNull(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
}