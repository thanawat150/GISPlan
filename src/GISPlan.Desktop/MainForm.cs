using System.Diagnostics;
using GISPlan.Core;

namespace GISPlan.Desktop;

public sealed class MainForm : Form
{
    private sealed record Choice<T>(T Value, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly LocalizationService _localizer;
    private readonly bool _simpleMode;

    private readonly ComboBox _operation = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _tool = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _input = new();
    private readonly TextBox _secondary = new();
    private readonly TextBox _output = new();
    private readonly TextBox _crs = new() { Text = "EPSG:32647" };
    private readonly NumericUpDown _bufferDistance = new() { Minimum = 0.01M, Maximum = 1_000_000M, Value = 100M, DecimalPlaces = 2 };
    private readonly CheckBox _dissolve = new() { Text = "Dissolve" };
    private readonly TextBox _objective = new();
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly Label _status = new() { AutoSize = true };
    private readonly Label _operationHelp = new() { AutoSize = true, MaximumSize = new Size(900, 0), ForeColor = Color.DarkSlateGray };
    private readonly Label _warning = new() { AutoSize = true, MaximumSize = new Size(900, 0), ForeColor = Color.DarkOrange, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
    private readonly ProgressBar _progress = new() { Style = ProgressBarStyle.Blocks, Dock = DockStyle.Fill };
    private readonly Button _detectButton = new();
    private readonly Button _preflightButton = new();
    private readonly Button _runButton = new();
    private readonly Button _cancelButton = new() { Enabled = false };
    private readonly Button _openButton = new() { Enabled = false };

    private RuntimeInfo? _runtime;
    private CancellationTokenSource? _cts;
    private string? _lastPath;

    public MainForm(LocalizationService? localizer = null, GisOperation? initialOperation = null, bool simpleMode = true)
    {
        _localizer = localizer ?? new LocalizationService();
        _simpleMode = simpleMode;

        Text = $"{_localizer.Text("app.title")} — {_localizer.Text("new_job")}";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(940, 720);
        Size = new Size(1100, 820);
        Font = new Font("Segoe UI", 10F);

        _input.PlaceholderText = _localizer.Text("input");
        _secondary.PlaceholderText = _localizer.Text("overlay");
        _objective.Text = _localizer.Text("new_job");
        _status.Text = _localizer.Text("ready");
        _detectButton.Text = _localizer.Text("detect_runtime");
        _preflightButton.Text = _localizer.Text("check_data");
        _runButton.Text = _localizer.Text("run");
        _cancelButton.Text = _localizer.Text("cancel");
        _openButton.Text = _localizer.Text("open_output");

        _operation.DataSource = BuildOperationChoices();
        _tool.DataSource = BuildToolChoices();
        _tool.Enabled = !_simpleMode;
        _output.Text = Path.Combine(AppPaths.DefaultOutputRoot, "gis_output.gpkg");

        if (initialOperation is not null)
            _operation.SelectedItem = ((IEnumerable<Choice<GisOperation>>)_operation.DataSource)
                .FirstOrDefault(x => EqualityComparer<GisOperation>.Default.Equals(x.Value, initialOperation.Value));

        Controls.Add(BuildLayout());
        Shown += async (_, _) => await DetectRuntimeAsync();
        _operation.SelectedIndexChanged += (_, _) => UpdateOperationUi();
        UpdateOperationUi();
    }

    private List<Choice<GisOperation>> BuildOperationChoices() =>
    [
        new(GisOperation.Inspect, _localizer.Text("operation.inspect")),
        new(GisOperation.ReprojectVector, _localizer.Text("operation.reproject")),
        new(GisOperation.ClipVector, _localizer.Text("operation.clip")),
        new(GisOperation.BufferVector, _localizer.Text("operation.buffer")),
        new(GisOperation.ConvertVector, _localizer.Text("operation.convert"))
    ];

    private List<Choice<ToolPreference>> BuildToolChoices() =>
    [
        new(ToolPreference.Auto, _localizer.Text("tool.auto")),
        new(ToolPreference.Qgis, _localizer.Text("tool.qgis")),
        new(ToolPreference.ArcGis, _localizer.Text("tool.arcgis")),
        new(ToolPreference.Gdal, _localizer.Text("tool.gdal"))
    ];

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 6
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = _localizer.Text("app.title"),
            Font = new Font(Font.FontFamily, 22F, FontStyle.Bold),
            AutoSize = true
        };
        var subtitle = new Label
        {
            Text = _simpleMode
                ? _localizer.Text("app.subtitle")
                : "Inspect • Reproject • Clip • Buffer • Convert",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 0, 0, 12)
        };
        var header = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header, 0, 0);

        var helpPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(10),
            BackColor = Color.AliceBlue
        };
        helpPanel.Controls.Add(_operationHelp);
        helpPanel.Controls.Add(_warning);
        root.Controls.Add(helpPanel, 0, 1);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(0, 8, 0, 8)
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));

        AddRow(fields, _localizer.Text("objective"), _objective, null);
        AddRow(fields, _localizer.Text("operation"), _operation, null);
        AddRow(fields, _localizer.Text("tool"), _tool, _detectButton);
        AddRow(fields, _localizer.Text("input"), _input, MakeBrowseButton(_localizer.Text("choose_file"), () => BrowseInput(_input)));
        AddRow(fields, _localizer.Text("overlay"), _secondary, MakeBrowseButton(_localizer.Text("choose_file"), () => BrowseInput(_secondary)));
        AddRow(fields, _localizer.Text("output"), _output, MakeBrowseButton(_localizer.Text("choose_output"), BrowseOutput));
        AddRow(fields, _localizer.Text("target_crs"), _crs, null);

        var bufferPanel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        bufferPanel.Controls.Add(_bufferDistance);
        bufferPanel.Controls.Add(new Label { Text = _localizer.Text("metres"), AutoSize = true, Margin = new Padding(8, 7, 16, 0) });
        bufferPanel.Controls.Add(_dissolve);
        AddRow(fields, _localizer.Text("buffer"), bufferPanel, null);
        root.Controls.Add(fields, 0, 2);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 4, 0, 8) };
        actions.Controls.AddRange([_preflightButton, _runButton, _cancelButton, _openButton]);
        _detectButton.Click += async (_, _) => await DetectRuntimeAsync();
        _preflightButton.Click += async (_, _) => await PreflightAsync();
        _runButton.Click += async (_, _) => await RunAsync();
        _cancelButton.Click += (_, _) => _cts?.Cancel();
        _openButton.Click += (_, _) => OpenLastPath();
        root.Controls.Add(actions, 0, 3);

        var logGroup = new GroupBox { Text = _localizer.Text("status_log"), Dock = DockStyle.Fill, Padding = new Padding(10) };
        logGroup.Controls.Add(_log);
        root.Controls.Add(logGroup, 0, 4);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        footer.Controls.Add(_status, 0, 0);
        footer.Controls.Add(_progress, 1, 0);
        root.Controls.Add(footer, 0, 5);
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
            SetBusy(true, _localizer.Text("status.checking_runtime"));
            _runtime = await new RuntimeDetector().DetectAsync();
            AppendLog($"QGIS: {Display(_runtime.QgisProcessPath)}");
            AppendLog($"GDAL ogr2ogr: {Display(_runtime.Ogr2OgrPath)}");
            AppendLog($"ArcGIS Pro ArcPy: {Display(_runtime.ArcGisPropyPath)}");
            foreach (var warning in _runtime.Warnings)
                AppendLog("Warning: " + warning);
            _status.Text = _runtime.HasQgis || _runtime.HasGdal || _runtime.HasArcGis
                ? _localizer.Text("status.runtime_checked")
                : _localizer.Text("status.runtime_missing");
        }
        catch (Exception ex)
        {
            AppendLog("Runtime Error: " + ex.Message);
            _status.Text = "rework";
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
            SetBusy(true, _localizer.Text("status.preflight"));
            var job = BuildJob();
            var report = await new PreflightService().CheckAsync(job, _runtime);
            AppendLog($"Preflight: {report.Status}");
            foreach (var message in report.Messages)
                AppendLog($"[{message.Level}] {message.Message}");
            _status.Text = report.Passed
                ? _localizer.Text("status.preflight_passed")
                : _localizer.Text("status.preflight_failed");
        }
        catch (Exception ex)
        {
            AppendLog("Preflight Error: " + ex.Message);
            _status.Text = "rework";
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
            SetBusy(true, _localizer.Text("status.running"));
            var job = BuildJob();
            var progress = new Progress<string>(text =>
            {
                _status.Text = text;
                AppendLog(text);
            });
            var result = await new GisJobRunner().RunAsync(job, _runtime, progress, _cts.Token);
            AppendLog($"Result: {result.Status} — {result.Message}");
            AppendLog($"Tool: {result.Tool}");
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
            AppendLog(_localizer.Text("status.cancelled"));
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
        Operation = SelectedOperation,
        PreferredTool = SelectedTool,
        InputPath = _input.Text.Trim(),
        SecondaryInputPath = string.IsNullOrWhiteSpace(_secondary.Text) ? null : _secondary.Text.Trim(),
        OutputPath = _output.Text.Trim(),
        TargetCrs = string.IsNullOrWhiteSpace(_crs.Text) ? null : _crs.Text.Trim().ToUpperInvariant(),
        BufferDistanceMetres = decimal.ToDouble(_bufferDistance.Value),
        Dissolve = _dissolve.Checked,
        Overwrite = false
    };

    private GisOperation SelectedOperation =>
        _operation.SelectedItem is Choice<GisOperation> choice ? choice.Value : GisOperation.Inspect;

    private ToolPreference SelectedTool =>
        _tool.SelectedItem is Choice<ToolPreference> choice ? choice.Value : ToolPreference.Auto;

    private void UpdateOperationUi()
    {
        var operation = SelectedOperation;
        _secondary.Enabled = operation == GisOperation.ClipVector;
        _bufferDistance.Enabled = operation == GisOperation.BufferVector;
        _dissolve.Enabled = operation == GisOperation.BufferVector;
        _crs.Enabled = operation is GisOperation.ReprojectVector or GisOperation.BufferVector;
        _output.Enabled = operation != GisOperation.Inspect;

        var helpKey = operation switch
        {
            GisOperation.Inspect => "help.inspect",
            GisOperation.ReprojectVector => "help.reproject",
            GisOperation.ClipVector => "help.clip",
            GisOperation.BufferVector => "help.buffer",
            GisOperation.ConvertVector => "help.convert",
            _ => "help.inspect"
        };
        _operationHelp.Text = _localizer.Text(helpKey);
        _warning.Text = operation == GisOperation.BufferVector
            ? _localizer.Text("warning.buffer_crs")
            : string.Empty;
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

    private void AppendLog(string text) =>
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");

    private void OpenLastPath()
    {
        if (string.IsNullOrWhiteSpace(_lastPath)) return;
        var directory = File.Exists(_lastPath) ? Path.GetDirectoryName(_lastPath) : _lastPath;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", directory) { UseShellExecute = true });
    }

    private string Display(string? path) => string.IsNullOrWhiteSpace(path) ? _localizer.Text("status.not_found") : path;
}
