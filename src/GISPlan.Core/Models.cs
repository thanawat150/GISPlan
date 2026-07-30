using System.Text.Json;
using System.Text.Json.Serialization;

namespace GISPlan.Core;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GisOperation
{
    Inspect,
    ReprojectVector,
    ClipVector,
    BufferVector,
    ConvertVector
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ToolPreference
{
    Auto,
    Qgis,
    ArcGis,
    Gdal
}

public sealed class GisJob
{
    public string JobId { get; set; } = $"GIS-{DateTime.Now:yyyyMMdd-HHmmss}";
    public string Objective { get; set; } = string.Empty;
    public GisOperation Operation { get; set; } = GisOperation.Inspect;
    public ToolPreference PreferredTool { get; set; } = ToolPreference.Auto;
    public string InputPath { get; set; } = string.Empty;
    public string? SecondaryInputPath { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public string? TargetCrs { get; set; }
    public double BufferDistanceMetres { get; set; } = 100;
    public bool Dissolve { get; set; }
    public bool Overwrite { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public sealed class RuntimeInfo
{
    public DateTime DetectedAt { get; set; } = DateTime.Now;
    public string? QgisProcessPath { get; set; }
    public string? OgrInfoPath { get; set; }
    public string? Ogr2OgrPath { get; set; }
    public string? GdalSrsInfoPath { get; set; }
    public string? ArcGisPropyPath { get; set; }
    public string? PythonPath { get; set; }
    public List<string> Warnings { get; set; } = [];

    [JsonIgnore] public bool HasQgis => !string.IsNullOrWhiteSpace(QgisProcessPath);
    [JsonIgnore] public bool HasGdal => !string.IsNullOrWhiteSpace(OgrInfoPath) && !string.IsNullOrWhiteSpace(Ogr2OgrPath);
    [JsonIgnore] public bool HasArcGis => !string.IsNullOrWhiteSpace(ArcGisPropyPath);
}

public sealed class ValidationMessage
{
    public string Level { get; set; } = "info";
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class PreflightReport
{
    public string JobId { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; } = DateTime.Now;
    public bool Passed { get; set; }
    public string Status { get; set; } = "draft";
    public long? InputSizeBytes { get; set; }
    public string? MetadataText { get; set; }
    public List<ValidationMessage> Messages { get; set; } = [];
}

public sealed class ProcessResult
{
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public bool Success => ExitCode == 0;
}

public sealed class AdapterResult
{
    public bool Success { get; set; }
    public string Tool { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    public string Message { get; set; } = string.Empty;
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
}

public sealed class QaReport
{
    public string JobId { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; } = DateTime.Now;
    public string Status { get; set; } = "draft";
    public bool OutputExists { get; set; }
    public long OutputSizeBytes { get; set; }
    public string? DetectedCrsText { get; set; }
    public List<ValidationMessage> Messages { get; set; } = [];
}

public sealed class RunManifest
{
    public string JobId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public string Status { get; set; } = "draft";
    public string Tool { get; set; } = string.Empty;
    public string JobDirectory { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public string? Error { get; set; }
}

public sealed class JobRunResult
{
    public bool Success { get; set; }
    public string Status { get; set; } = "draft";
    public string Message { get; set; } = string.Empty;
    public string JobDirectory { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public string Tool { get; set; } = string.Empty;
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
