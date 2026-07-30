using System.Diagnostics;
using GISPlan.Core;

namespace GISPlan.Desktop;

public sealed class StartupForm : Form
{
    private readonly TextBox _log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill
    };

    private readonly Button _newJobButton = new() { Text = "สร้างงาน GIS ใหม่", Width = 180, Height = 42 };
    private readonly Button _resumeButton = new() { Text = "ทำงานต่อจาก Job เดิม", Width = 180, Height = 42 };
    private readonly Button _detectButton = new() { Text = "ตรวจโปรแกรม GIS", Width = 160, Height = 42 };
    private readonly Button _cancelButton = new() { Text = "ยกเลิกงาน", Width = 120, Height = 42, Enabled = false };
    private readonly Label _status = new() { Text = "พร้อมใช้งาน", AutoSize = true };

    private CancellationTokenSource? _cts;
    private RuntimeInfo? _runtime;

    public StartupForm()
    {
        Text = "GISPlan";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 520);
        Size = new Size(900, 620);
        Font = new Font("Segoe UI", 10F);

        Controls.Add(BuildLayout());
        Shown += async (_, _) => await DetectRuntimeAsync();
        _newJobButton.Click += (_, _) => OpenNewJob();
        _resumeButton.Click += async (_, _) => await ResumeJobAsync();
        _detectButton.Click += async (_, _) => await DetectRuntimeAsync();
        _cancelButton.Click += (_, _) => _cts?.Cancel();
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        header.Controls.Add(new Label
        {
            Text = "GISPlan",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 24F, FontStyle.Bold)
        });
        header.Controls.Add(new Label
        {
            Text = "Portable GIS Workspace — สร้างงานใหม่หรือทำงานต่อจาก Job ที่ล้มเหลว",
            AutoSize = true,
            ForeColor = Color.DimGray
        });
        root.Controls.Add(header, 0, 0);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 18, 0, 12)
        };
        actions.Controls.AddRange([_newJobButton, _resumeButton, _detectButton, _cancelButton]);
        root.Controls.Add(actions, 0, 1);

        var group = new GroupBox { Text = "Runtime และสถานะ", Dock = DockStyle.Fill, Padding = new Padding(10) };
        group.Controls.Add(_log);
        root.Controls.Add(group, 0, 2);
        root.Controls.Add(_status, 0, 3);
        return root;
    }

    private void OpenNewJob()
    {
        Hide();
        try
        {
            using var form = new MainForm();
            form.ShowDialog(this);
        }
        finally
        {
            Show();
        }
    }

    private async Task DetectRuntimeAsync()
    {
        try
        {
            SetBusy(true, "กำลังตรวจ QGIS, ArcGIS Pro และ GDAL");
            _runtime = await new RuntimeDetector().DetectAsync();
            Append($"QGIS: {ShowPath(_runtime.QgisProcessPath)}");
            Append($"GDAL/OGR: {ShowPath(_runtime.Ogr2OgrPath)}");
            Append($"ArcGIS Pro / ArcPy: {ShowPath(_runtime.ArcGisPropyPath)}");
            foreach (var warning in _runtime.Warnings)
                Append("คำเตือน: " + warning);
            _status.Text = _runtime.HasQgis || _runtime.HasGdal || _runtime.HasArcGis
                ? "ตรวจ Runtime แล้ว"
                : "missing_runtime";
        }
        catch (Exception ex)
        {
            Append("Runtime Error: " + ex.Message);
            _status.Text = "rework";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ResumeJobAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "เลือก gis_job.json ที่ต้องการทำต่อ",
            Filter = "GISPlan Job|gis_job.json|JSON files|*.json|All files|*.*",
            CheckFileExists = true,
            InitialDirectory = AppPaths.JobsRoot
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        if (_runtime is null)
            await DetectRuntimeAsync();
        if (_runtime is null) return;

        _cts = new CancellationTokenSource();
        try
        {
            SetBusy(true, "กำลัง Resume Job");
            var progress = new Progress<string>(text =>
            {
                Append(text);
                _status.Text = text;
            });
            var result = await new JobResumeService().ResumeAsync(dialog.FileName, _runtime, progress, _cts.Token);
            Append($"Resume result: {result.Status} — {result.Message}");
            Append($"Job folder: {result.JobDirectory}");
            if (!string.IsNullOrWhiteSpace(result.OutputPath))
                Append($"Output: {result.OutputPath}");
            _status.Text = result.Status;

            var openPath = !string.IsNullOrWhiteSpace(result.OutputPath)
                ? Path.GetDirectoryName(result.OutputPath)
                : result.JobDirectory;
            if (!string.IsNullOrWhiteSpace(openPath) && Directory.Exists(openPath))
            {
                var answer = MessageBox.Show(this, "เปิดโฟลเดอร์ผลลัพธ์หรือไม่", "GISPlan", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (answer == DialogResult.Yes)
                    Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { openPath } });
            }
        }
        catch (OperationCanceledException)
        {
            Append("ยกเลิก Resume แล้ว");
            _status.Text = "cancelled";
        }
        catch (Exception ex)
        {
            Append("Resume Error: " + ex.Message);
            _status.Text = "rework";
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _newJobButton.Enabled = !busy;
        _resumeButton.Enabled = !busy;
        _detectButton.Enabled = !busy;
        _cancelButton.Enabled = busy;
        if (status is not null) _status.Text = status;
    }

    private void Append(string text) =>
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");

    private static string ShowPath(string? path) => string.IsNullOrWhiteSpace(path) ? "ไม่พบ" : path;
}
