using System.Text.Json;
using GISPlan.Core;

var failures = new List<string>();

void Check(bool condition, string message)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")}: {message}");
    if (!condition) failures.Add(message);
}

var job = new GisJob
{
    JobId = "TEST-001",
    Objective = "smoke test",
    Operation = GisOperation.ReprojectVector,
    PreferredTool = ToolPreference.Auto,
    InputPath = "input.gpkg",
    OutputPath = "output.gpkg",
    TargetCrs = "EPSG:32647"
};

var json = JsonSerializer.Serialize(job, JsonDefaults.Options);
var restored = JsonSerializer.Deserialize<GisJob>(json, JsonDefaults.Options);
Check(restored?.Operation == GisOperation.ReprojectVector, "JSON enum round-trip");
Check(restored?.TargetCrs == "EPSG:32647", "CRS round-trip");

var runtimeJson = JsonSerializer.Serialize(new RuntimeInfo { QgisGuiPath = @"C:\QGIS\bin\qgis-bin.exe" }, JsonDefaults.Options);
var runtimeRestored = JsonSerializer.Deserialize<RuntimeInfo>(runtimeJson, JsonDefaults.Options);
Check(runtimeRestored?.QgisGuiPath?.EndsWith("qgis-bin.exe", StringComparison.OrdinalIgnoreCase) == true, "QGIS GUI path round-trip");

var thai = new LocalizationService("th-TH");
var english = new LocalizationService("en-US");
Check(thai.Text("assistant.guide") == "สอนว่าไปตรงไหน", "Thai localization");
Check(english.Text("assistant.guide") == "Show me where", "English localization");

var guide = new GuidedAssistantService(thai);
var bufferGuide = guide.Find("อยากทำ buffer 100 เมตร");
Check(bufferGuide.SuggestedOperation == GisOperation.BufferVector, "Assistant routes buffer request");
Check(bufferGuide.CanPrepareAutomatically, "Assistant marks supported automation");
var mapGuide = guide.Find("อยากปรับสีขอบเขตและทำแผนที่ PDF");
Check(mapGuide.Id == "map", "Assistant routes map styling request");
Check(!mapGuide.CanPrepareAutomatically, "Assistant does not claim unsupported map automation");

var unsafeBuffer = new GisJob
{
    JobId = "TEST-BUFFER-GEOGRAPHIC",
    Operation = GisOperation.BufferVector,
    InputPath = "input.gpkg",
    OutputPath = "buffer.gpkg",
    TargetCrs = "EPSG:4326",
    BufferDistanceMetres = 100
};
var unsafeResult = await new SafeGisJobRunner().RunAsync(unsafeBuffer, new RuntimeInfo());
Check(!unsafeResult.Success && unsafeResult.Status == "rework", "Geographic CRS buffer is blocked before processing");

var registry = new ExternalDataSourceRegistry();
Check(registry.Sources.Count >= 8, "External data registry includes core official/open providers");
Check(registry.Sources.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == registry.Sources.Count,
    "External data provider IDs are unique");
Check(registry.Sources.All(x => x.PortalUri.Scheme == Uri.UriSchemeHttps),
    "External data portals use HTTPS");
Check(registry.Sources.All(x => !string.IsNullOrWhiteSpace(x.LicenseNote) && !string.IsNullOrWhiteSpace(x.Attribution)),
    "Every external source records license and attribution guidance");
Check(registry.Sources.Any(x => x.Keywords.Contains("dem", StringComparer.OrdinalIgnoreCase)),
    "External data registry includes elevation/DEM sources");
Check(registry.Filter("ป่าชายเลน").Any(x => x.Id == "dmcr-change"),
    "External data filtering finds official mangrove source");
Check(registry.Filter("dem").Any(x => x.Id is "copernicus-stac" or "nasa-earthdata" or "jaxa-aw3d30"),
    "External data filtering finds DEM sources");
Check(registry.Sources.Single(x => x.Id == "nasa-earthdata").AccessKind == DataAccessKind.Cmr,
    "NASA provider uses the live CMR API");

Check(ExternalCatalogSearchService.TryParseBoundingBox("97.3,5.6,105.7,20.5", out var thailandBbox) && thailandBbox is { Length: 4 },
    "Valid Thailand BBOX is accepted");
Check(!ExternalCatalogSearchService.TryParseBoundingBox("105,20,97,5", out _),
    "Reversed BBOX is rejected");
Check(!ExternalCatalogSearchService.TryParseBoundingBox("not-a-bbox", out _),
    "Malformed BBOX is rejected");

var directResult = new ExternalDataResult(
    "test", "Example", "", "GEOTIFF", new Uri("https://example.com/item"),
    new Uri("https://example.com/data/example.tif"), null, "license", "source", false);
Check(ExternalCatalogSearchService.SuggestFileName(directResult) == "example.tif", "Download filename is derived safely");

var temp = Path.Combine(Path.GetTempPath(), "GISPlanSmoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var existing = Path.Combine(temp, "result.gpkg");
    File.WriteAllText(existing, "x");
    var versioned = OutputPathResolver.Resolve(existing, overwrite: false);
    Check(versioned.EndsWith("result_v2.gpkg", StringComparison.OrdinalIgnoreCase), "Versioned output naming");
    Check(OutputPathResolver.Resolve(existing, overwrite: true) == existing, "Overwrite policy");

    var runtime = new RuntimeInfo();
    var preflight = await new PreflightService().CheckAsync(job, runtime);
    Check(!preflight.Passed, "Missing input/runtime is rejected");
    Check(preflight.Messages.Any(m => m.Code == "input_missing"), "Missing input is reported");
    Check(preflight.Messages.Any(m => m.Code == "missing_runtime"), "Missing runtime is reported");
}
finally
{
    Directory.Delete(temp, recursive: true);
}

AppPaths.EnsureCreated();
Check(Directory.Exists(AppPaths.RuntimeRoot), "User-space runtime directory can be created");
Check(!AppPaths.RuntimeRoot.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase),
    "Runtime does not use Program Files");

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Smoke tests failed: {failures.Count}");
    Environment.Exit(1);
}

Console.WriteLine("All GISPlan smoke tests passed.");