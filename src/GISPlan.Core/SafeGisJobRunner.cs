using System.Text.Json;
using System.Text.RegularExpressions;

namespace GISPlan.Core;

/// <summary>
/// Wraps the basic job runner with safety steps that are required for user-facing workflows.
/// In particular, metre buffers are never sent directly to a geographic layer.
/// </summary>
public sealed class SafeGisJobRunner
{
    private readonly GisJobRunner _runner = new();

    public async Task<JobRunResult> RunAsync(
        GisJob job,
        RuntimeInfo runtime,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (job.Operation != GisOperation.BufferVector)
            return await _runner.RunAsync(job, runtime, progress, cancellationToken);

        if (!IsEpsg(job.TargetCrs))
            return Failure(job, "Buffer ต้องระบุ Working CRS แบบ Projected เช่น EPSG ของ UTM Zone ที่ถูกต้อง");

        if (await IsGeographicAsync(job.TargetCrs!, runtime, cancellationToken))
            return Failure(job, $"{job.TargetCrs} เป็น CRS แบบองศา จึงใช้ Buffer เป็นเมตรไม่ได้ กรุณาเลือก Projected CRS");

        AppPaths.EnsureCreated();
        var temporaryProjectedPath = Path.Combine(
            AppPaths.CacheRoot,
            $"buffer-source-{Guid.NewGuid():N}.gpkg");

        try
        {
            progress?.Report("เตรียม Buffer: แปลงข้อมูลชั่วคราวเป็น CRS หน่วยเมตร");
            var preparation = Clone(job);
            preparation.JobId = job.JobId + "-BUFFER-PREP";
            preparation.Objective = "Prepare projected source for safe metre buffer";
            preparation.Operation = GisOperation.ReprojectVector;
            preparation.OutputPath = temporaryProjectedPath;
            preparation.Overwrite = true;

            var preparedResult = await _runner.RunAsync(preparation, runtime, progress, cancellationToken);
            if (!preparedResult.Success)
            {
                preparedResult.Message = "ไม่สามารถเตรียม CRS หน่วยเมตรก่อน Buffer ได้: " + preparedResult.Message;
                return preparedResult;
            }

            progress?.Report("Buffer: ใช้ระยะเมตรกับข้อมูลที่แปลง CRS แล้ว");
            var bufferJob = Clone(job);
            bufferJob.InputPath = temporaryProjectedPath;
            bufferJob.Overwrite = job.Overwrite;
            return await _runner.RunAsync(bufferJob, runtime, progress, cancellationToken);
        }
        finally
        {
            TryDelete(temporaryProjectedPath);
            TryDelete(Path.ChangeExtension(temporaryProjectedPath, ".gpkg-shm"));
            TryDelete(Path.ChangeExtension(temporaryProjectedPath, ".gpkg-wal"));
        }
    }

    private static JobRunResult Failure(GisJob job, string message) => new()
    {
        Success = false,
        Status = "rework",
        Message = message,
        JobDirectory = AppPaths.GetJobDirectory(job.JobId)
    };

    private static bool IsEpsg(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        Regex.IsMatch(value.Trim(), "^EPSG:[0-9]+$", RegexOptions.IgnoreCase);

    private static async Task<bool> IsGeographicAsync(
        string targetCrs,
        RuntimeInfo runtime,
        CancellationToken cancellationToken)
    {
        var knownGeographic = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EPSG:4326", "EPSG:4269", "EPSG:4258", "EPSG:4236", "EPSG:4674"
        };
        if (knownGeographic.Contains(targetCrs.Trim())) return true;

        if (string.IsNullOrWhiteSpace(runtime.GdalSrsInfoPath)) return false;
        var result = await ProcessRunner.RunAsync(
            runtime.GdalSrsInfoPath,
            ["-o", "proj4", targetCrs],
            AppPaths.CacheRoot,
            cancellationToken);
        if (!result.Success) return false;

        var text = (result.StandardOutput + " " + result.StandardError).ToLowerInvariant();
        return text.Contains("+proj=longlat", StringComparison.Ordinal) ||
               text.Contains("+units=degree", StringComparison.Ordinal) ||
               text.Contains("geogcrs", StringComparison.Ordinal);
    }

    private static GisJob Clone(GisJob job) =>
        JsonSerializer.Deserialize<GisJob>(JsonSerializer.Serialize(job, JsonDefaults.Options), JsonDefaults.Options)
        ?? throw new InvalidOperationException("Unable to clone GIS job");

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Cache cleanup failure must not hide the processing result.
        }
    }
}
