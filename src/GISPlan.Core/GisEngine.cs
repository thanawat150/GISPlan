using System.Text.Json;
using System.Text.RegularExpressions;

namespace GISPlan.Core;

public sealed class PreflightService
{
    public async Task<PreflightReport> CheckAsync(
        GisJob job,
        RuntimeInfo runtime,
        CancellationToken cancellationToken = default)
    {
        var report = new PreflightReport { JobId = job.JobId };

        if (string.IsNullOrWhiteSpace(job.InputPath) || !File.Exists(job.InputPath))
            AddError(report, "input_missing", $"ไม่พบ Input: {job.InputPath}");
        else
        {
            var info = new FileInfo(job.InputPath);
            report.InputSizeBytes = info.Length;
            report.Messages.Add(new ValidationMessage
            {
                Code = "input_readable",
                Message = $"พบ Input ขนาด {info.Length:N0} bytes"
            });
        }

        if (job.Operation == GisOperation.ClipVector &&
            (string.IsNullOrWhiteSpace(job.SecondaryInputPath) || !File.Exists(job.SecondaryInputPath)))
            AddError(report, "overlay_missing", "งาน Clip ต้องมีไฟล์ขอบเขต Overlay/Mask");

        if (job.Operation == GisOperation.ReprojectVector && !IsEpsg(job.TargetCrs))
            AddError(report, "target_crs_invalid", "งาน Reproject ต้องระบุ Target CRS เช่น EPSG:32647");

        if (job.Operation == GisOperation.BufferVector && job.BufferDistanceMetres <= 0)
            AddError(report, "buffer_invalid", "ระยะ Buffer ต้องมากกว่า 0 เมตร");

        if (job.Operation != GisOperation.Inspect)
        {
            if (string.IsNullOrWhiteSpace(job.OutputPath))
                AddError(report, "output_missing", "ต้องระบุ Output Path");
            else
            {
                var outputDirectory = Path.GetDirectoryName(job.OutputPath);
                if (string.IsNullOrWhiteSpace(outputDirectory))
                    AddError(report, "output_directory_invalid", "Output Path ไม่มีโฟลเดอร์ปลายทาง");
                else
                {
                    try
                    {
                        Directory.CreateDirectory(outputDirectory);
                        var probe = Path.Combine(outputDirectory, $".gisplan-write-test-{Guid.NewGuid():N}");
                        await File.WriteAllTextAsync(probe, "test", cancellationToken);
                        File.Delete(probe);
                    }
                    catch (Exception ex)
                    {
                        AddError(report, "output_not_writable", ex.Message);
                    }
                }
            }
        }

        if (!ToolSelector.HasCompatibleTool(job, runtime))
            AddError(report, "missing_runtime", "ไม่พบ QGIS, ArcGIS Pro หรือ GDAL ที่รองรับงานนี้");

        if (File.Exists(job.InputPath) && !string.IsNullOrWhiteSpace(runtime.OgrInfoPath))
        {
            var metadata = await ProcessRunner.RunAsync(
                runtime.OgrInfoPath,
                ["-ro", "-so", "-al", job.InputPath],
                Path.GetDirectoryName(job.InputPath),
                cancellationToken);

            report.MetadataText = metadata.StandardOutput;
            if (!metadata.Success)
            {
                report.Messages.Add(new ValidationMessage
                {
                    Level = "warning",
                    Code = "metadata_warning",
                    Message = string.IsNullOrWhiteSpace(metadata.StandardError)
                        ? "OGR ไม่สามารถอ่าน Metadata ได้"
                        : metadata.StandardError.Trim()
                });
            }
        }
        else if (File.Exists(job.InputPath))
        {
            report.Messages.Add(new ValidationMessage
            {
                Level = "warning",
                Code = "metadata_not_inspected",
                Message = "ยังไม่ได้ตรวจ Geometry/CRS ภายใน เพราะไม่พบ ogrinfo"
            });
        }

        report.Passed = report.Messages.All(m => m.Level != "error");
        report.Status = report.Passed
            ? report.Messages.Any(m => m.Level == "warning") ? "passed_with_warnings" : "passed"
            : report.Messages.Any(m => m.Code == "missing_runtime") ? "missing_runtime" : "rework";
        return report;
    }

    private static bool IsEpsg(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value.Trim(), "^EPSG:[0-9]+$", RegexOptions.IgnoreCase);

    private static void AddError(PreflightReport report, string code, string message) =>
        report.Messages.Add(new ValidationMessage { Level = "error", Code = code, Message = message });
}

public interface IGisAdapter
{
    string Name { get; }
    bool CanRun(GisJob job, RuntimeInfo runtime);
    Task<AdapterResult> RunAsync(
        GisJob job,
        RuntimeInfo runtime,
        string jobDirectory,
        IProgress<string>? progress,
        CancellationToken cancellationToken);
}

public sealed class GdalAdapter : IGisAdapter
{
    public string Name => "GDAL/OGR";

    public bool CanRun(GisJob job, RuntimeInfo runtime) => job.Operation switch
    {
        GisOperation.Inspect => !string.IsNullOrWhiteSpace(runtime.OgrInfoPath),
        GisOperation.ReprojectVector or GisOperation.ClipVector or GisOperation.ConvertVector =>
            !string.IsNullOrWhiteSpace(runtime.Ogr2OgrPath),
        _ => false
    };

    public async Task<AdapterResult> RunAsync(
        GisJob job,
        RuntimeInfo runtime,
        string jobDirectory,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("กำลังเรียก GDAL/OGR");
        string executable;
        List<string> args;

        if (job.Operation == GisOperation.Inspect)
        {
            executable = runtime.OgrInfoPath!;
            args = ["-ro", "-so", "-al", job.InputPath];
        }
        else
        {
            executable = runtime.Ogr2OgrPath!;
            var driver = DriverFromPath(job.OutputPath);
            args = ["-f", driver, job.OutputPath, job.InputPath];

            if (job.Overwrite)
                args.Insert(0, "-overwrite");

            if (job.Operation == GisOperation.ReprojectVector)
            {
                args.Add("-t_srs");
                args.Add(job.TargetCrs!);
            }
            else if (job.Operation == GisOperation.ClipVector)
            {
                args.Add("-clipsrc");
                args.Add(job.SecondaryInputPath!);
            }
        }

        var result = await ProcessRunner.RunAsync(executable, args, jobDirectory, cancellationToken);
        return new AdapterResult
        {
            Success = result.Success,
            Tool = Name,
            Status = result.Success ? "passed" : result.ExitCode == -2 ? "cancelled" : "rework",
            Message = result.Success ? "ประมวลผลด้วย GDAL/OGR สำเร็จ" : "GDAL/OGR ทำงานไม่สำเร็จ",
            StandardOutput = result.StandardOutput,
            StandardError = result.StandardError
        };
    }

    private static string DriverFromPath(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".gpkg" => "GPKG",
        ".shp" => "ESRI Shapefile",
        ".geojson" or ".json" => "GeoJSON",
        ".kml" => "KML",
        _ => "GPKG"
    };
}

public sealed class QgisAdapter : IGisAdapter
{
    public string Name => "QGIS Processing";

    public bool CanRun(GisJob job, RuntimeInfo runtime) =>
        runtime.HasQgis && job.Operation != GisOperation.Inspect;

    public async Task<AdapterResult> RunAsync(
        GisJob job,
        RuntimeInfo runtime,
        string jobDirectory,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("กำลังเรียก QGIS Processing");
        var args = job.Operation switch
        {
            GisOperation.ReprojectVector => new List<string>
            {
                "run", "native:reprojectlayer",
                $"--INPUT={job.InputPath}",
                $"--TARGET_CRS={job.TargetCrs}",
                $"--OUTPUT={job.OutputPath}"
            },
            GisOperation.ClipVector => new List<string>
            {
                "run", "native:clip",
                $"--INPUT={job.InputPath}",
                $"--OVERLAY={job.SecondaryInputPath}",
                $"--OUTPUT={job.OutputPath}"
            },
            GisOperation.BufferVector => new List<string>
            {
                "run", "native:buffer",
                $"--INPUT={job.InputPath}",
                $"--DISTANCE={job.BufferDistanceMetres.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                "--SEGMENTS=5",
                "--END_CAP_STYLE=0",
                "--JOIN_STYLE=0",
                "--MITER_LIMIT=2",
                $"--DISSOLVE={job.Dissolve.ToString().ToLowerInvariant()}",
                $"--OUTPUT={job.OutputPath}"
            },
            GisOperation.ConvertVector => new List<string>
            {
                "run", "native:savefeatures",
                $"--INPUT={job.InputPath}",
                $"--OUTPUT={job.OutputPath}"
            },
            _ => []
        };

        if (args.Count == 0)
            return new AdapterResult { Success = false, Tool = Name, Status = "rework", Message = "QGIS Adapter ไม่รองรับ Operation นี้" };

        var result = await ProcessRunner.RunAsync(runtime.QgisProcessPath!, args, jobDirectory, cancellationToken);
        return new AdapterResult
        {
            Success = result.Success,
            Tool = Name,
            Status = result.Success ? "passed" : result.ExitCode == -2 ? "cancelled" : "rework",
            Message = result.Success ? "ประมวลผลด้วย QGIS สำเร็จ" : "QGIS Processing ทำงานไม่สำเร็จ",
            StandardOutput = result.StandardOutput,
            StandardError = result.StandardError
        };
    }
}

public sealed class ArcGisAdapter : IGisAdapter
{
    public string Name => "ArcGIS Pro / ArcPy";

    public bool CanRun(GisJob job, RuntimeInfo runtime) => runtime.HasArcGis;

    public async Task<AdapterResult> RunAsync(
        GisJob job,
        RuntimeInfo runtime,
        string jobDirectory,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("กำลังเรียก ArcGIS Pro / ArcPy");
        var jobPath = Path.Combine(jobDirectory, "arcgis_job.json");
        var scriptPath = Path.Combine(jobDirectory, "arcgis_job.py");
        await File.WriteAllTextAsync(jobPath, JsonSerializer.Serialize(job, JsonDefaults.Options), cancellationToken);
        await File.WriteAllTextAsync(scriptPath, ArcPyScript, cancellationToken);

        var result = await ProcessRunner.RunAsync(
            runtime.ArcGisPropyPath!,
            [scriptPath, jobPath],
            jobDirectory,
            cancellationToken);

        return new AdapterResult
        {
            Success = result.Success,
            Tool = Name,
            Status = result.Success ? "passed" : result.ExitCode == -2 ? "cancelled" : "rework",
            Message = result.Success ? "ประมวลผลด้วย ArcPy สำเร็จ" : "ArcPy ทำงานไม่สำเร็จ โปรดตรวจ License และ Log",
            StandardOutput = result.StandardOutput,
            StandardError = result.StandardError
        };
    }

    private const string ArcPyScript = """
import arcpy
import json
import os
import sys

job_path = sys.argv[1]
with open(job_path, "r", encoding="utf-8") as f:
    job = json.load(f)

op = job["operation"]
inp = job["inputPath"]
out = job.get("outputPath")
secondary = job.get("secondaryInputPath")

if out:
    os.makedirs(os.path.dirname(out), exist_ok=True)

if op == "Inspect":
    d = arcpy.Describe(inp)
    print(json.dumps({
        "catalogPath": d.catalogPath,
        "dataType": d.dataType,
        "shapeType": getattr(d, "shapeType", None),
        "spatialReference": getattr(getattr(d, "spatialReference", None), "name", None)
    }, ensure_ascii=False))
elif op == "ReprojectVector":
    arcpy.management.Project(inp, out, job["targetCrs"])
elif op == "ClipVector":
    arcpy.analysis.Clip(inp, secondary, out)
elif op == "BufferVector":
    dissolve = "ALL" if job.get("dissolve") else "NONE"
    arcpy.analysis.Buffer(inp, out, f"{job['bufferDistanceMetres']} Meters", dissolve_option=dissolve)
elif op == "ConvertVector":
    arcpy.conversion.ExportFeatures(inp, out)
else:
    raise ValueError(f"Unsupported operation: {op}")

print("GISPLAN_ARCPY_OK")
""";
}

public static class ToolSelector
{
    private static readonly IGisAdapter[] Adapters =
    [
        new GdalAdapter(),
        new QgisAdapter(),
        new ArcGisAdapter()
    ];

    public static bool HasCompatibleTool(GisJob job, RuntimeInfo runtime) =>
        Adapters.Any(a => a.CanRun(job, runtime));

    public static IGisAdapter? Select(GisJob job, RuntimeInfo runtime)
    {
        var requestedName = job.PreferredTool switch
        {
            ToolPreference.Qgis => "QGIS Processing",
            ToolPreference.ArcGis => "ArcGIS Pro / ArcPy",
            ToolPreference.Gdal => "GDAL/OGR",
            _ => null
        };

        if (requestedName is not null)
            return Adapters.FirstOrDefault(a => a.Name == requestedName && a.CanRun(job, runtime));

        if (job.Operation == GisOperation.BufferVector)
            return Adapters.FirstOrDefault(a => a is QgisAdapter && a.CanRun(job, runtime))
                   ?? Adapters.FirstOrDefault(a => a is ArcGisAdapter && a.CanRun(job, runtime));

        return Adapters.FirstOrDefault(a => a is GdalAdapter && a.CanRun(job, runtime))
               ?? Adapters.FirstOrDefault(a => a is QgisAdapter && a.CanRun(job, runtime))
               ?? Adapters.FirstOrDefault(a => a is ArcGisAdapter && a.CanRun(job, runtime));
    }
}

public sealed class QaService
{
    public async Task<QaReport> CheckAsync(
        GisJob job,
        RuntimeInfo runtime,
        CancellationToken cancellationToken = default)
    {
        var report = new QaReport { JobId = job.JobId };

        if (job.Operation == GisOperation.Inspect)
        {
            report.OutputExists = true;
            report.Status = "passed";
            report.Messages.Add(new ValidationMessage { Code = "inspect_complete", Message = "ตรวจ Metadata สำเร็จ" });
            return report;
        }

        report.OutputExists = File.Exists(job.OutputPath);
        if (!report.OutputExists)
        {
            report.Status = "rework";
            report.Messages.Add(new ValidationMessage { Level = "error", Code = "output_missing", Message = "ไม่พบ Output หลังประมวลผล" });
            return report;
        }

        report.OutputSizeBytes = new FileInfo(job.OutputPath).Length;
        if (report.OutputSizeBytes == 0)
            report.Messages.Add(new ValidationMessage { Level = "error", Code = "output_empty", Message = "Output มีขนาด 0 bytes" });

        if (!string.IsNullOrWhiteSpace(job.TargetCrs) && !string.IsNullOrWhiteSpace(runtime.GdalSrsInfoPath))
        {
            var srs = await ProcessRunner.RunAsync(
                runtime.GdalSrsInfoPath,
                ["-o", "epsg", job.OutputPath],
                Path.GetDirectoryName(job.OutputPath),
                cancellationToken);
            report.DetectedCrsText = srs.StandardOutput.Trim();
            var expectedCode = job.TargetCrs.Split(':').Last();
            if (!srs.Success || !report.DetectedCrsText.Contains(expectedCode, StringComparison.OrdinalIgnoreCase))
                report.Messages.Add(new ValidationMessage { Level = "warning", Code = "crs_not_verified", Message = $"ยังยืนยัน CRS {job.TargetCrs} ไม่ได้" });
        }
        else if (!string.IsNullOrWhiteSpace(job.TargetCrs))
        {
            report.Messages.Add(new ValidationMessage { Level = "warning", Code = "crs_tool_missing", Message = "ไม่มี gdalsrsinfo สำหรับตรวจ CRS หลังประมวลผล" });
        }

        report.Status = report.Messages.Any(m => m.Level == "error")
            ? "rework"
            : report.Messages.Any(m => m.Level == "warning") ? "passed_with_warnings" : "passed";
        return report;
    }
}

public sealed class GisJobRunner
{
    private readonly PreflightService _preflight = new();
    private readonly QaService _qa = new();

    public async Task<JobRunResult> RunAsync(
        GisJob job,
        RuntimeInfo runtime,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureCreated();
        var startedAt = DateTime.Now;
        var jobDirectory = AppPaths.GetJobDirectory(job.JobId);
        progress?.Report("Preflight: ตรวจ Input, CRS และ Runtime");

        var preflight = await _preflight.CheckAsync(job, runtime, cancellationToken);
        await WriteJsonAsync(Path.Combine(jobDirectory, "preflight_report.json"), preflight, cancellationToken);
        if (!preflight.Passed)
        {
            await WriteManifestAsync(job, jobDirectory, startedAt, preflight.Status, string.Empty, null,
                string.Join(Environment.NewLine, preflight.Messages.Where(m => m.Level == "error").Select(m => m.Message)), cancellationToken);
            return new JobRunResult
            {
                Success = false,
                Status = preflight.Status,
                Message = "Preflight ไม่ผ่าน",
                JobDirectory = jobDirectory
            };
        }

        var effectiveJob = Clone(job);
        if (effectiveJob.Operation != GisOperation.Inspect)
        {
            effectiveJob.OutputPath = OutputPathResolver.Resolve(effectiveJob.OutputPath, effectiveJob.Overwrite);
            Directory.CreateDirectory(Path.GetDirectoryName(effectiveJob.OutputPath)!);
        }
        await WriteJsonAsync(Path.Combine(jobDirectory, "gis_job.json"), effectiveJob, cancellationToken);

        var adapter = ToolSelector.Select(effectiveJob, runtime);
        if (adapter is null)
        {
            await WriteManifestAsync(effectiveJob, jobDirectory, startedAt, "missing_runtime", string.Empty, null,
                "ไม่พบเครื่องมือที่รองรับ", cancellationToken);
            return new JobRunResult
            {
                Success = false,
                Status = "missing_runtime",
                Message = "ไม่พบเครื่องมือ GIS ที่รองรับงานนี้",
                JobDirectory = jobDirectory
            };
        }

        progress?.Report($"Process: {adapter.Name}");
        var adapterResult = await adapter.RunAsync(effectiveJob, runtime, jobDirectory, progress, cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(jobDirectory, "run.log"),
            $"TOOL: {adapter.Name}{Environment.NewLine}STATUS: {adapterResult.Status}{Environment.NewLine}{adapterResult.StandardOutput}{Environment.NewLine}{adapterResult.StandardError}",
            cancellationToken);

        if (!adapterResult.Success)
        {
            await WriteManifestAsync(effectiveJob, jobDirectory, startedAt, adapterResult.Status, adapter.Name,
                effectiveJob.Operation == GisOperation.Inspect ? null : effectiveJob.OutputPath,
                adapterResult.StandardError, cancellationToken);
            return new JobRunResult
            {
                Success = false,
                Status = adapterResult.Status,
                Message = adapterResult.Message,
                Tool = adapter.Name,
                JobDirectory = jobDirectory,
                OutputPath = effectiveJob.Operation == GisOperation.Inspect ? null : effectiveJob.OutputPath
            };
        }

        progress?.Report("QA/QC: ตรวจ Output");
        var qa = await _qa.CheckAsync(effectiveJob, runtime, cancellationToken);
        await WriteJsonAsync(Path.Combine(jobDirectory, "qa_report.json"), qa, cancellationToken);

        var success = qa.Status is "passed" or "passed_with_warnings";
        await WriteManifestAsync(effectiveJob, jobDirectory, startedAt, qa.Status, adapter.Name,
            effectiveJob.Operation == GisOperation.Inspect ? null : effectiveJob.OutputPath,
            success ? null : "QA/QC ไม่ผ่าน", cancellationToken);

        progress?.Report(success ? "เสร็จแล้ว" : "QA/QC ไม่ผ่าน");
        return new JobRunResult
        {
            Success = success,
            Status = qa.Status,
            Message = success ? "งานเสร็จและผ่าน QA/QC" : "งานประมวลผลเสร็จแต่ QA/QC ไม่ผ่าน",
            Tool = adapter.Name,
            JobDirectory = jobDirectory,
            OutputPath = effectiveJob.Operation == GisOperation.Inspect ? null : effectiveJob.OutputPath
        };
    }

    private static GisJob Clone(GisJob job) =>
        JsonSerializer.Deserialize<GisJob>(JsonSerializer.Serialize(job, JsonDefaults.Options), JsonDefaults.Options)
        ?? throw new InvalidOperationException("ไม่สามารถ Clone Job ได้");

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonDefaults.Options, cancellationToken);
    }

    private static Task WriteManifestAsync(
        GisJob job,
        string jobDirectory,
        DateTime startedAt,
        string status,
        string tool,
        string? outputPath,
        string? error,
        CancellationToken cancellationToken) =>
        WriteJsonAsync(Path.Combine(jobDirectory, "run_manifest.json"), new RunManifest
        {
            JobId = job.JobId,
            StartedAt = startedAt,
            FinishedAt = DateTime.Now,
            Status = status,
            Tool = tool,
            JobDirectory = jobDirectory,
            OutputPath = outputPath,
            Error = error
        }, cancellationToken);
}
