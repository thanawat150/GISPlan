using System.Diagnostics;
using GISPlan.Core;

namespace GISPlan.Desktop;

public sealed class MainForm : Form
{
    private readonly ComboBox _operation = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _tool = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _input = new() { PlaceholderText = "เลือกไฟล์ Vector" };
    private readonly TextBox _secondary = new() { PlaceholderText = "ใช้สำหรับ Clip Overlay/Mask" };
    private readonly TextBox _output = new();
    private readonly TextBox _crs = new() { Text = "EPSG:32647" };
    private readonly NumericUpDown _bufferDistance = new() { Minimum = 0.01M, Maximum = 1_000_000M, Value = 100M, DecimalPlaces = 2 };
    private readonly CheckBox _dissolve = new() { Text = "Dissolve Buffer" };
    private readonly TextBox _objective = new() { Text = "งาน GIS จาก GISPlan" };
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly Label _status = new() { Text = "พร้อมใช้งาน", AutoSize = true };
    private readonly ProgressBar _progress = new() { Style = ProgressBarStyle.Blocks, Dock = DockStyle.Fill };
    private readonly Button _detectButton = new() { Text = "ตรวจโปรแกรม GIS" };
    private readonly Button _preflightButton = new() { Text = "ตรวจข้อมูล" };
    private readonly Button _runButton = new() { Text = "เริ่มประมวลผล" };
    private readonly Button _cancelButton = new() { Text = "ยกเลิก", Enabled = false };
    private readonly Button _openButton = new() { Text = "เปิดผลลัพธ์", Enabled = false };

    private RuntimeInfo? _runtime;
    private CancellationTokenSource? _cts;
    private string? _lastPath;

    public MainForm()
    {
        Text = "GISPlan — Portable GIS Workspace";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(920, 680);
        Size = new Size(1080, 760);
        Font = new Font("Segoe UI", 10F);

        _operation.DataSource = Enum.GetValues<GisOperation>();
        _tool.DataSource = Enum.GetValues<ToolPreference>();
        _output.Text = Path.Combine(AppPaths.DefaultOutputRoot, "gis_output.gpkg");

        Controls.Add(BuildLayout());
        Shown += async (_, _) => await DetectRuntimeAsync();
        _operation.SelectedIndexChanged += (_, _) => UpdateOperationUi();
        UpdateOperationUi();
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "GISPlan",
            Font = new Font(Font.FontFamily, 22F, FontStyle.Bold),
            AutoSize = true
        };
        var subtitle = new Label
        {
            Text = "ตรวจข้อมูล • Reproject • Clip • Buffer • Convert โดยเลือกใช้ QGIS, ArcGIS Pro หรือ GDAL อัตโนมัติ",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 0, 0, 12)
        };
        var header = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header, 0, 0);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(0, 4, 0, 8)
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));

        AddRow(fields, "เป้าหมายงาน", _objective, null);
        AddRow(fields, "ประเภทงาน", _operation, null);
        AddRow(fields, "เครื่องมือ", _tool, _detectButton);
        AddRow(fields, "Input", _input, MakeBrowseButton("เลือกไฟล์", () => BrowseInput(_input)));
        AddRow(fields, "Overlay / Mask", _secondary, MakeBrowseButton("เลือกไฟล์", () => BrowseInput(_secondary)));
        AddRow(fields, "Output", _output, MakeBrowseButton("เลือกที่เก็บ", BrowseOutput));
        AddRow(fields, "Target CRS", _crs, null);

        var bufferPanel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        bufferPanel.Controls.Add(_bufferDistance);
        bufferPanel.Controls.Add(new Label { Text = "เมตร", AutoSize = true, Margin = new Padding(8, 7, 16, 0) });
        bufferPanel.Controls.Add(_dissolve);
        AddRow(fields, "Buffer", bufferPanel, null);

        root.Controls.Add(fields, 0, 1);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 4, 0, 8) };
        actions.Controls.AddRange([_preflightButton, _runButton, _cancelButton, _openButton]);
        _detectButton.Click += async (_, _) => await DetectRuntimeAsync();
        _preflightButton.Click += async (_, _) => await PreflightAsync();
        _runButton.Click += async (_, _) => await RunAsync();
        _cancelButton.Click += (_, _) => _cts?.Cancel();
        _openButton.Click += (_, _) => OpenLastPath();
        root.Controls.Add(actions, 0, 2);

        var logGroup = new GroupBox { Text = "สถานะและ Log", Dock = DockStyle.Fill, Padding = new Padding(10) };
        logGroup.Controls.Add(_log);
        root.Controls.Add(logGroup, 0, 3);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        footer.Controls.Add(_status, 0, 0);
        footer.Controls.Add(_progress, 1, 0);
        root.Controls.Add(footer, 0, 4);

        return root;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control, Control? button)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) }, 0, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(3, 4, 3, 4);
        table.Controls.Add(control, 1, row);
        if (button is not null)
        {
            button.Dock = DockStyle.Fill;
            table.Controls.Add(button, 2, row);
        }
    }

    private static Button MakeBrowseButton(string text, Action action)
    {
        var button = new Button { Text = text };
        button.Click += (_, _) => action();
        return button;
    }

    private void BrowseInput(TextBox target)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "GIS Vector|*.gpkg;*.shp;*.geojson;*.json;*.kml|All files|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            target.Text = dialog.FileName;
    }

    private void BrowseOutput()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "GeoPackage|*.gpkg|Shapefile|*.shp|GeoJSON|*.geojson|KML|*.kml",
            FileName = Path.GetFileName(_output.Text),
            InitialDirectory = Directory.Exists(Path.GetDirectoryName(_output.Text))
                ? Path.GetDirectoryName(_output.Text)
                : AppPaths.DefaultOutputRoot,
            OverwritePrompt = false
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _output.Text = dialog.FileName;
    }

    private async Task DetectRuntimeAsync()
    {
        try
        {
            SetBusy(true, "กำลังตรวจ QGIS, ArcGIS Pro และ GDAL");
            _runtime = await new RuntimeDetector().DetectAsync();
            AppendLog($"QGIS: {Display(_runtime.QgisProcessPath)}");
            AppendLog($"GDAL ogr2ogr: {Display(_runtime.Ogr2OgrPath)}");
            AppendLog($"ArcGIS Pro ArcPy: {Display(_runtime.ArcGisPropyPath)}");
            foreach (var warning in _runtime.Warnings)
                AppendLog("คำเตือน: " + warning);
            _status.Text = _runtime.HasQgis || _runtime.HasGdal || _runtime.HasArcGis
                ? "ตรวจ Runtime แล้ว"
                : "ยังไม่พบ GIS Runtime";
        }
        catch (Exception ex)
        {
            AppendLog("ตรวจ Runtime ไม่สำเร็จ: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task PreflightAsync()
    {
        if (_runtime is null)
            await DetectRuntimeAsync();
        if (_runtime is null) return;

        try
        {
            SetBusy(true, "กำลังตรวจ Input และ CRS");
            var job = BuildJob();
            var report = await new PreflightService().CheckAsync(job, _runtime);
            AppendLog($"Preflight: {report.Status}");
            foreach (var message in report.Messages)
                AppendLog($"[{message.Level}] {message.Message}");
            _status.Text = report.Passed ? "Preflight ผ่าน" : "Preflight ไม่ผ่าน";
        }
        catch (Exception ex)
        {
            AppendLog("Preflight Error: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RunAsync()
    {
        if (_runtime is null)
            await DetectRuntimeAsync();
        if (_runtime is null) return;

        _cts = new CancellationTokenSource();
        try
        {
            SetBusy(true, "กำลังเริ่มงาน");
            var job = BuildJob();
            var progress = new Progress<string>(text =>
            {
                _status.Text = text;
                AppendLog(text);
            });
            var result = await new GisJobRunner().RunAsync(job, _runtime, progress, _cts.Token);
            AppendLog($"ผลลัพธ์: {result.Status} — {result.Message}");
            AppendLog($"เครื่องมือ: {result.Tool}");
            AppendLog($"Job folder: {result.JobDirectory}");
            if (!string.IsNullOrWhiteSpace(result.OutputPath))
                AppendLog($"Output: {result.OutputPath}");

            _lastPath = !string.IsNullOrWhiteSpace(result.OutputPath)
                ? result.OutputPath
                : result.JobDirectory;
            _openButton.Enabled = Directory.Exists(result.JobDirectory);
            _status.Text = result.Status;
        }
        catch (OperationCanceledException)
        {
            AppendLog("ยกเลิกงานแล้ว");
            _status.Text = "cancelled";
        }
        catch (Exception ex)
        {
            AppendLog("Run Error: " + ex.Message);
            _status.Text = "rework";
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    private GisJob BuildJob() => new()
    {
        JobId = $"GIS-{DateTime.Now:yyyyMMdd-HHmmss}",
        Objective = _objective.Text.Trim(),
        Operation = (GisOperation)(_operation.SelectedItem ?? GisOperation.Inspect),
        PreferredTool = (ToolPreference)(_tool.SelectedItem ?? ToolPreference.Auto),
        InputPath = _input.Text.Trim(),
        SecondaryInputPath = string.IsNullOrWhiteSpace(_secondary.Text) ? null : _secondary.Text.Trim(),
        OutputPath = _output.Text.Trim(),
        TargetCrs = string.IsNullOrWhiteSpace(_crs.Text) ? null : _crs.Text.Trim().ToUpperInvariant(),
        BufferDistanceMetres = decimal.ToDouble(_bufferDistance.Value),
        Dissolve = _dissolve.Checked,
        Overwrite = false
    };

    private void UpdateOperationUi()
    {
        var operation = (GisOperation)(_operation.SelectedItem ?? GisOperation.Inspect);
        _secondary.Enabled = operation == GisOperation.ClipVector;
        _bufferDistance.Enabled = operation == GisOperation.BufferVector;
        _dissolve.Enabled = operation == GisOperation.BufferVector;
        _crs.Enabled = operation == GisOperation.ReprojectVector;
        _output.Enabled = operation != GisOperation.Inspect;
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _progress.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
        _runButton.Enabled = !busy;
        _preflightButton.Enabled = !busy;
        _detectButton.Enabled = !busy;
        _cancelButton.Enabled = busy;
        if (status is not null) _status.Text = status;
    }

    private void AppendLog(string text)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
    }

    private void OpenLastPath()
    {
        if (string.IsNullOrWhiteSpace(_lastPath)) return;
        var directory = File.Exists(_lastPath) ? Path.GetDirectoryName(_lastPath) : _lastPath;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", directory) { UseShellExecute = true });
    }

    private static string Display(string? path) => string.IsNullOrWhiteSpace(path) ? "ไม่พบ" : path;
}
