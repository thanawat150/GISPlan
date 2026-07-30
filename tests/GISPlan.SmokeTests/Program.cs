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
