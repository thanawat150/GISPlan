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
    private readonly bool _thai;

    private readonly ComboBox _operation = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _tool = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _input = new();
    private readonly TextBox _secondary = new();
    private readonly TextBox _output = new();
    private readonly TextBox _crs = new() { Text = "EPSG:32647" };
    private readonly NumericUpDown _bufferDistance = new() { Minimum = 0.01M, Maximum = 1_000_000M, Value = 100M, DecimalPlaces = 2 };
    private readonly CheckBox _dissolve = new() { Text = "Dissolve", AutoSize = true };
    private readonly TextBox _objective = new();
    private readonly TextBox _log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        BackColor = UiTheme.Navy,
        ForeColor = Color.FromArgb(203, 213, 225),
        Font = new Font("Cascadia Mono", 9F)
    };

    private readonly Label _operationTitle = new();
    private readonly Label _operationHelp = new();
    private readonly Label _warning = new();
    private readonly Label _runtimeSummary = new();
    private readonly Label _reviewSummary = new();
    private readonly StatusPill _status = new();
    private readonly ProgressBar _progress = new() { Style = ProgressBarStyle.Blocks, Height = 7 };

    private readonly ModernButton _detectButton = new() { Kind = ModernButtonKind.Ghost };
    private readonly ModernButton _preflightButton = new() { Kind = ModernButtonKind.Secondary };
    private readonly ModernButton _runButton = new() { Kind = ModernButtonKind.Primary };
    private readonly ModernButton _cancelButton = new() { Kind = ModernButtonKind.Danger, Enabled = false };
    private readonly ModernButton _openButton = new() { Kind = ModernButtonKind.Success, Enabled = false };
    private readonly ModernButton _detailsButton = new() { Kind = ModernButtonKind.Ghost };

    private readonly ModernCard _technicalCard = new()
    {
        Dock = DockStyle.Fill,
        BackColor = UiTheme.Navy,
        BorderColor = UiTheme.Navy,
        Visible = false
    };

    private readonly Panel _secondaryRow = new() { Dock = DockStyle.Top, AutoSize = true };
    private readonly Panel _crsRow = new() { Dock = DockStyle.Top, AutoSize = true };
    private readonly Panel _bufferRow = new() { Dock = DockStyle.Top, AutoSize = true };
    private readonly Panel _outputRow = new() { Dock = DockStyle.Top, AutoSize = true };
    private readonly Panel _toolRow = new() { Dock = DockStyle.Top, AutoSize = true };

    private RuntimeInfo? _runtime;
    private CancellationTokenSource? _cts;
    private string? _lastPath;

    public MainForm(LocalizationService? localizer = null, GisOperation? initialOperation = null, bool simpleMode = true)
    {
        _localizer = localizer ?? new LocalizationService();
        _simpleMode = simpleMode;
        _thai = _localizer.LanguageCode.StartsWith("th", StringComparison.OrdinalIgnoreCase);

        Text = $"{_localizer.Text("app.title")} — {_localizer.Text("new_job")}";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1020, 720);
        Size = new Size(1240, 850);
        UiTheme.ApplyForm(this);

        foreach (var control in new Control[] { _operation, _tool, _input, _secondary, _output, _crs, _bufferDistance, _objective })
            UiTheme.StyleInput(control);
        _dissolve.ForeColor = UiTheme.Text;
        _dissolve.Margin = new Padding(14, 10, 0, 0);

        _input.PlaceholderText = _localizer.Text("input");
        _secondary.PlaceholderText = _localizer.Text("overlay");
        _objective.Text = _localizer.Text("new_job");
        _status.SetNeutral(_localizer.Text("ready"));
        _detectButton.Text = _localizer.Text("detect_runtime");
        _preflightButton.Text = _localizer.Text("check_data");
        _runButton.Text = _localizer.Text("run");
        _cancelButton.Text = _localizer.Text("cancel");
        _openButton.Text = _localizer.Text("open_output");
        _detailsButton.Text = _thai ? "แสดงรายละเอียดทางเทคนิค" : "Show technical details";

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
        _detectButton.Click += async (_, _) => await DetectRuntimeAsync();
        _preflightButton.Click += async (_, _) => await PreflightAsync();
        _runButton.Click += async (_, _) => await RunAsync();
        _cancelButton.Click += (_, _) => _cts?.Cancel();
        _openButton.Click += (_, _) => OpenLastPath();
        _detailsButton.Click += (_, _) => ToggleTechnicalDetails();
        foreach (var textBox in new[] { _input, _secondary, _output, _crs, _objective })
            textBox.TextChanged += (_, _) => UpdateReviewSummary();
        _bufferDistance.ValueChanged += (_, _) => UpdateReviewSummary();
        _dissolve.CheckedChanged += (_, _) => UpdateReviewSummary();
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
            ColumnCount = 1,
            RowCount = 3,
            BackColor = UiTheme.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildWorkspace(), 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        return root;
    }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Navy,
            Padding = new Padding(28, 18, 28, 16)
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titleStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        titleStack.Controls.Add(new Label
        {
            Text = _localizer.Text("new_job"),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 23F, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(0)
        });
        titleStack.Controls.Add(new Label
        {
            Text = _thai
                ? "เลือกงาน ใส่ข้อมูล ตรวจสอบ แล้วจึงเริ่มประมวลผล"
                : "Choose a task, provide inputs, validate, then run.",
            AutoSize = true,
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 10F),
            Margin = new Padding(1, 4, 0, 0)
        });
        layout.Controls.Add(titleStack, 0, 0);

        _runtimeSummary.AutoSize = true;
        _runtimeSummary.ForeColor = Color.FromArgb(203, 213, 225);
        _runtimeSummary.Font = new Font("Segoe UI", 9.5F);
        _runtimeSummary.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _runtimeSummary.Margin = new Padding(0, 12, 0, 0);
        _runtimeSummary.Text = _thai ? "กำลังตรวจโปรแกรม GIS..." : "Checking GIS software...";
        layout.Controls.Add(_runtimeSummary, 1, 0);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildWorkspace()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(22, 20, 22, 18),
            BackColor = UiTheme.Background
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        shell.Controls.Add(BuildConfigurationCard(), 0, 0);
        shell.Controls.Add(BuildReviewColumn(), 1, 0);
        return shell;
    }

    private Control BuildConfigurationCard()
    {
        var card = new ModernCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _operationTitle.AutoSize = true;
        _operationTitle.Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold);
        _operationTitle.ForeColor = UiTheme.Text;
        _operationHelp.AutoSize = true;
        _operationHelp.MaximumSize = new Size(740, 0);
        _operationHelp.ForeColor = UiTheme.MutedText;
        _operationHelp.Margin = new Padding(0, 4, 0, 6);
        _warning.AutoSize = true;
        _warning.MaximumSize = new Size(740, 0);
        _warning.ForeColor = Color.FromArgb(146, 64, 14);
        _warning.Font = new Font("Segoe UI Semibold", 9.25F, FontStyle.Bold);
        _warning.Margin = new Padding(0, 4, 0, 12);

        var intro = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        intro.Controls.Add(_operationTitle);
        intro.Controls.Add(_operationHelp);
        intro.Controls.Add(_warning);
        layout.Controls.Add(intro, 0, 0);

        var taskRow = BuildFieldRow(_localizer.Text("operation"), _operation, null);
        taskRow.Margin = new Padding(0, 2, 0, 10);
        layout.Controls.Add(taskRow, 0, 1);

        var fieldsScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, 0, 8, 0) };
        var fields = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        fields.Controls.Add(BuildFieldRow(_localizer.Text("objective"), _objective, null));
        fields.Controls.Add(BuildFieldRow(_localizer.Text("input"), _input, MakeBrowseButton(_localizer.Text("choose_file"), () => BrowseInput(_input))));

        _secondaryRow.Controls.Add(BuildFieldRow(_localizer.Text("overlay"), _secondary, MakeBrowseButton(_localizer.Text("choose_file"), () => BrowseInput(_secondary))));
        fields.Controls.Add(_secondaryRow);

        _outputRow.Controls.Add(BuildFieldRow(_localizer.Text("output"), _output, MakeBrowseButton(_localizer.Text("choose_output"), BrowseOutput)));
        fields.Controls.Add(_outputRow);

        _crsRow.Controls.Add(BuildFieldRow(_localizer.Text("target_crs"), _crs, null));
        fields.Controls.Add(_crsRow);

        var bufferPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _bufferDistance.Width = 160;
        bufferPanel.Controls.Add(_bufferDistance);
        bufferPanel.Controls.Add(new Label
        {
            Text = _localizer.Text("metres"),
            AutoSize = true,
            ForeColor = UiTheme.MutedText,
            Margin = new Padding(8, 12, 4, 0)
        });
        bufferPanel.Controls.Add(_dissolve);
        _bufferRow.Controls.Add(BuildFieldRow(_localizer.Text("buffer"), bufferPanel, null));
        fields.Controls.Add(_bufferRow);

        _toolRow.Controls.Add(BuildFieldRow(_localizer.Text("tool"), _tool, _detectButton));
        fields.Controls.Add(_toolRow);
        fieldsScroll.Controls.Add(fields);
        layout.Controls.Add(fieldsScroll, 0, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 12, 0, 0)
        };
        _preflightButton.Width = 165;
        _runButton.Width = 165;
        _cancelButton.Width = 120;
        actions.Controls.AddRange([_preflightButton, _runButton, _cancelButton]);
        layout.Controls.Add(actions, 0, 3);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildReviewColumn()
    {
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Margin = new Padding(10, 0, 0, 0)
        };

        var reviewCard = new ModernCard { Height = 330, Margin = new Padding(0, 0, 0, 14) };
        var reviewLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        reviewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        reviewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        reviewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        reviewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        reviewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        reviewLayout.Controls.Add(UiTheme.Heading(_thai ? "ตรวจทานก่อนเริ่ม" : "Review before running", 16F), 0, 0);
        var reviewHint = UiTheme.Caption(_thai
            ? "ระบบจะสร้างไฟล์ใหม่ ไม่เขียนทับข้อมูลต้นฉบับ"
            : "GISPlan creates a new output and keeps the source unchanged.", 320);
        reviewHint.Margin = new Padding(0, 3, 0, 12);
        reviewLayout.Controls.Add(reviewHint, 0, 1);

        var summaryPanel = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.SurfaceMuted, Padding = new Padding(14) };
        _reviewSummary.Dock = DockStyle.Fill;
        _reviewSummary.AutoSize = false;
        _reviewSummary.ForeColor = UiTheme.Text;
        _reviewSummary.Font = new Font("Segoe UI", 9.5F);
        summaryPanel.Controls.Add(_reviewSummary);
        reviewLayout.Controls.Add(summaryPanel, 0, 2);

        _openButton.Dock = DockStyle.Top;
        _openButton.Margin = new Padding(0, 12, 0, 7);
        _detailsButton.Dock = DockStyle.Top;
        reviewLayout.Controls.Add(_openButton, 0, 3);
        reviewLayout.Controls.Add(_detailsButton, 0, 4);
        reviewCard.Controls.Add(reviewLayout);

        var technicalLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        technicalLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        technicalLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        technicalLayout.Controls.Add(new Label
        {
            Text = _localizer.Text("status_log"),
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        }, 0, 0);
        technicalLayout.Controls.Add(_log, 0, 1);
        _technicalCard.Controls.Add(technicalLayout);
        _technicalCard.Height = 330;

        stack.Controls.Add(reviewCard);
        stack.Controls.Add(_technicalCard);
        stack.SizeChanged += (_, _) =>
        {
            var width = Math.Max(280, stack.ClientSize.Width - (stack.VerticalScroll.Visible ? 24 : 4));
            reviewCard.Width = width;
            _technicalCard.Width = width;
        };
        return stack;
    }

    private Control BuildFooter()
    {
        var footer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(22, 9, 22, 8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        layout.Controls.Add(_status, 0, 0);
        var note = new Label
        {
            Text = _thai ? "ตรวจข้อมูลก่อนเริ่มทุกครั้ง" : "Validate before every run",
            AutoSize = true,
            ForeColor = UiTheme.MutedText,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(12, 8, 0, 0)
        };
        layout.Controls.Add(note, 1, 0);
        _progress.Dock = DockStyle.Fill;
        _progress.Margin = new Padding(10, 11, 0, 0);
        layout.Controls.Add(_progress, 2, 0);
        footer.Controls.Add(layout);
        return footer;
    }

    private static Panel BuildFieldRow(string label, Control control, Control? button)
    {
        var row = new Panel { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 0, 0, 9) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, button is null ? 0 : 130));
        layout.Controls.Add(UiTheme.FieldLabel(label), 0, 0);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(control, 1, 0);
        if (button is not null)
        {
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(8, 6, 0, 6);
            layout.Controls.Add(button, 2, 0);
        }
        row.Controls.Add(layout);
        return row;
    }

    private static ModernButton MakeBrowseButton(string text, Action action)
    {
        var button = new ModernButton { Text = text, Kind = ModernButtonKind.Ghost, Height = 36 };
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

            var tools = new List<string>();
            if (_runtime.HasQgis) tools.Add("QGIS");
            if (_runtime.HasGdal) tools.Add("GDAL");
            if (_runtime.HasArcGis) tools.Add("ArcGIS Pro");
            _runtimeSummary.Text = tools.Count > 0
                ? (_thai ? $"พร้อมใช้: {string.Join(" • ", tools)}" : $"Available: {string.Join(" • ", tools)}")
                : _localizer.Text("status.runtime_missing");
            if (tools.Count > 0) _status.SetSuccess(_localizer.Text("status.runtime_checked"));
            else _status.SetWarning(_localizer.Text("status.runtime_missing"));
        }
        catch (Exception ex)
        {
            AppendLog("Runtime Error: " + ex.Message);
            _runtimeSummary.Text = _localizer.Text("status.runtime_missing");
            _status.SetError("rework");
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

            if (report.Passed)
            {
                _status.SetSuccess(_localizer.Text("status.preflight_passed"));
                MessageBox.Show(this,
                    _thai ? "ตรวจข้อมูลผ่านแล้ว สามารถเริ่มทำงานได้" : "Validation passed. The task is ready to run.",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                _status.SetError(_localizer.Text("status.preflight_failed"));
                _technicalCard.Visible = true;
                UpdateDetailsButtonText();
                MessageBox.Show(this,
                    string.Join(Environment.NewLine, report.Messages.Where(x => x.Level == "error").Select(x => "• " + x.Message)),
                    _localizer.Text("status.preflight_failed"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            AppendLog("Preflight Error: " + ex.Message);
            _status.SetError("rework");
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

        var job = BuildJob();
        var confirmText = BuildConfirmation(job);
        if (MessageBox.Show(this, confirmText,
                _thai ? "ยืนยันก่อนเริ่มงาน" : "Confirm task",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information) != DialogResult.OK)
            return;

        _cts = new CancellationTokenSource();
        try
        {
            SetBusy(true, _localizer.Text("status.running"));
            var progress = new Progress<string>(text =>
            {
                _status.SetBusy(text);
                AppendLog(text);
            });
            var result = await new SafeGisJobRunner().RunAsync(job, _runtime, progress, _cts.Token);
            AppendLog($"Result: {result.Status} — {result.Message}");
            AppendLog($"Tool: {result.Tool}");
            AppendLog($"Job folder: {result.JobDirectory}");
            if (!string.IsNullOrWhiteSpace(result.OutputPath))
                AppendLog($"Output: {result.OutputPath}");

            _lastPath = !string.IsNullOrWhiteSpace(result.OutputPath)
                ? result.OutputPath
                : result.JobDirectory;
            _openButton.Enabled = Directory.Exists(result.JobDirectory);
            SetResultStatus(result.Status, result.Message);
            if (!result.Success)
            {
                _technicalCard.Visible = true;
                UpdateDetailsButtonText();
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog(_localizer.Text("status.cancelled"));
            _status.SetWarning(_localizer.Text("status.cancelled"));
        }
        catch (Exception ex)
        {
            AppendLog("Run Error: " + ex.Message);
            _status.SetError("rework");
            _technicalCard.Visible = true;
            UpdateDetailsButtonText();
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
        _secondaryRow.Visible = operation == GisOperation.ClipVector;
        _bufferRow.Visible = operation == GisOperation.BufferVector;
        _crsRow.Visible = operation is GisOperation.ReprojectVector or GisOperation.BufferVector;
        _outputRow.Visible = operation != GisOperation.Inspect;
        _toolRow.Visible = !_simpleMode;

        var helpKey = operation switch
        {
            GisOperation.Inspect => "help.inspect",
            GisOperation.ReprojectVector => "help.reproject",
            GisOperation.ClipVector => "help.clip",
            GisOperation.BufferVector => "help.buffer",
            GisOperation.ConvertVector => "help.convert",
            _ => "help.inspect"
        };
        _operationTitle.Text = _operation.SelectedItem?.ToString() ?? _localizer.Text("operation");
        _operationHelp.Text = _localizer.Text(helpKey);
        _warning.Text = operation == GisOperation.BufferVector
            ? _localizer.Text("warning.buffer_crs")
            : string.Empty;
        UpdateReviewSummary();
    }

    private void UpdateReviewSummary()
    {
        var lines = new List<string>
        {
            $"{(_thai ? "งาน" : "Task")}: {_operation.SelectedItem}",
            $"{(_thai ? "ไฟล์ต้นทาง" : "Input")}: {ShortPath(_input.Text)}"
        };
        if (SelectedOperation == GisOperation.ClipVector)
            lines.Add($"{(_thai ? "ขอบเขตตัด" : "Boundary")}: {ShortPath(_secondary.Text)}");
        if (SelectedOperation is GisOperation.ReprojectVector or GisOperation.BufferVector)
            lines.Add($"CRS: {EmptyAsDash(_crs.Text)}");
        if (SelectedOperation == GisOperation.BufferVector)
            lines.Add($"Buffer: {_bufferDistance.Value:0.##} {_localizer.Text("metres")} • Dissolve: {(_dissolve.Checked ? "Yes" : "No")}");
        if (SelectedOperation != GisOperation.Inspect)
            lines.Add($"{(_thai ? "ผลลัพธ์" : "Output")}: {ShortPath(_output.Text)}");
        lines.Add($"{(_thai ? "เครื่องมือ" : "Tool")}: {_tool.SelectedItem}");
        _reviewSummary.Text = string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private string BuildConfirmation(GisJob job)
    {
        var lines = new List<string>
        {
            _thai ? "โปรดตรวจสอบก่อนเริ่ม:" : "Review before starting:",
            string.Empty,
            $"{(_thai ? "งาน" : "Task")}: {_operation.SelectedItem}",
            $"{(_thai ? "ต้นทาง" : "Input")}: {job.InputPath}"
        };
        if (job.SecondaryInputPath is not null)
            lines.Add($"{(_thai ? "ขอบเขต" : "Boundary")}: {job.SecondaryInputPath}");
        if (!string.IsNullOrWhiteSpace(job.TargetCrs))
            lines.Add($"CRS: {job.TargetCrs}");
        if (job.Operation == GisOperation.BufferVector)
            lines.Add($"Buffer: {job.BufferDistanceMetres:0.##} {_localizer.Text("metres")}");
        if (job.Operation != GisOperation.Inspect)
            lines.Add($"{(_thai ? "ผลลัพธ์" : "Output")}: {job.OutputPath}");
        lines.Add(string.Empty);
        lines.Add(_thai
            ? "ระบบจะไม่เขียนทับไฟล์เดิมโดยอัตโนมัติ"
            : "Existing outputs are versioned rather than overwritten automatically.");
        return string.Join(Environment.NewLine, lines);
    }

    private void ToggleTechnicalDetails()
    {
        _technicalCard.Visible = !_technicalCard.Visible;
        UpdateDetailsButtonText();
    }

    private void UpdateDetailsButtonText() =>
        _detailsButton.Text = _technicalCard.Visible
            ? (_thai ? "ซ่อนรายละเอียดทางเทคนิค" : "Hide technical details")
            : (_thai ? "แสดงรายละเอียดทางเทคนิค" : "Show technical details");

    private void SetBusy(bool busy, string? status = null)
    {
        _progress.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
        _runButton.Enabled = !busy;
        _preflightButton.Enabled = !busy;
        _detectButton.Enabled = !busy;
        _cancelButton.Enabled = busy;
        _operation.Enabled = !busy;
        if (status is not null) _status.SetBusy(status);
    }

    private void SetResultStatus(string status, string message)
    {
        if (status == "passed") _status.SetSuccess(message);
        else if (status == "passed_with_warnings") _status.SetWarning(message);
        else if (status is "cancelled" or "missing_runtime") _status.SetWarning(message);
        else _status.SetError(message);
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

    private static string ShortPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "—";
        if (path.Length <= 52) return path;
        return "…" + path[^49..];
    }

    private static string EmptyAsDash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
}
