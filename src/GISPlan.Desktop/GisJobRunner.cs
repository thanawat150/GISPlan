using GISPlan.Core;

namespace GISPlan.Desktop;

/// <summary>
/// Desktop-facing runner. Keeping this adapter in the UI namespace ensures every job started
/// from MainForm uses the safety workflow, including projected-CRS preparation for metre buffers.
/// </summary>
public sealed class GisJobRunner
{
    private readonly SafeGisJobRunner _safeRunner = new();

    public Task<JobRunResult> RunAsync(
        GisJob job,
        RuntimeInfo runtime,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default) =>
        _safeRunner.RunAsync(job, runtime, progress, cancellationToken);
}