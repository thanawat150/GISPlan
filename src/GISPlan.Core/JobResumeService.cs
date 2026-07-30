using System.Text.Json;

namespace GISPlan.Core;

public sealed class JobResumeService
{
    public async Task<JobRunResult> ResumeAsync(
        string jobFilePath,
        RuntimeInfo runtime,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobFilePath) || !File.Exists(jobFilePath))
        {
            return new JobRunResult
            {
                Success = false,
                Status = "rework",
                Message = $"ไม่พบ Job Config: {jobFilePath}"
            };
        }

        GisJob? job;
        try
        {
            await using var stream = File.OpenRead(jobFilePath);
            job = await JsonSerializer.DeserializeAsync<GisJob>(stream, JsonDefaults.Options, cancellationToken);
        }
        catch (Exception ex)
        {
            return new JobRunResult
            {
                Success = false,
                Status = "rework",
                Message = "อ่าน Job Config ไม่สำเร็จ: " + ex.Message
            };
        }

        if (job is null)
        {
            return new JobRunResult
            {
                Success = false,
                Status = "rework",
                Message = "Job Config ไม่มีข้อมูลที่ใช้งานได้"
            };
        }

        progress?.Report($"โหลด Job เดิม: {job.JobId}");

        if (job.Operation != GisOperation.Inspect && File.Exists(job.OutputPath))
        {
            progress?.Report("พบ Output เดิม กำลังตรวจ QA ก่อน Resume");
            var qa = await new QaService().CheckAsync(job, runtime, cancellationToken);
            if (qa.Status is "passed" or "passed_with_warnings")
            {
                return new JobRunResult
                {
                    Success = true,
                    Status = qa.Status,
                    Message = "งานเดิมมี Output ที่ผ่าน QA แล้ว จึงไม่ประมวลผลซ้ำ",
                    JobDirectory = Path.GetDirectoryName(jobFilePath) ?? string.Empty,
                    OutputPath = job.OutputPath,
                    Tool = "resume-check"
                };
            }

            progress?.Report("Output เดิมยังไม่ผ่าน QA ระบบจะสร้าง Output เวอร์ชันใหม่");
        }

        job.JobId = $"{job.JobId}-resume-{DateTime.Now:yyyyMMdd-HHmmss}";
        job.Overwrite = false;
        job.CreatedAt = DateTime.Now;

        return await new GisJobRunner().RunAsync(job, runtime, progress, cancellationToken);
    }
}
