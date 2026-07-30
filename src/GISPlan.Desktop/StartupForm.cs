using System.Diagnostics;
using GISPlan.Core;

namespace GISPlan.Desktop;

public sealed class StartupForm : Form
{
    private readonly UserPreferences _preferences;
    private readonly LocalizationService _localizer;
    private readonly GuidedAssistantService _assistant;

    private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly CheckBox _simpleMode = new() { AutoSize = true };
    private readonly TextBox _command = new() { Font = new Font("Segoe UI", 12F), Height = 36 };
    private readonly Button _guideButton = new() { Width = 165, Height = 38 };
    private readonly Button _prepareButton = new() { Width = 185, Height = 38 };
    private readonly Button _openGuideButton = new() { Width = 180, Height = 36, Enabled = false };
    private readonly RichTextBox _assistantResult = new()
    {
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        BackColor = SystemColors.Window,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 10.5F)
    };

    private readonly TextBox _log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill
    };

    private readonly Button _newJobButton = new() { Width = 180, Height = 42 };
    private readonly Button _resumeButton = new() { Width = 180, Height = 42 };
    private readonly Button _detectButton = new() { Width = 160, Height = 42 };
    private readonly Button _cancelButton = new() { Width = 120, Height = 42, Enabled = false };
    private readonly Button _detailsButton = new() { Width = 190, Height = 34 };
    private readonly Label _status = new() { AutoSize = true };
    private readonly Label _subtitle = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly Label _languageLabel = new() { AutoSize = true, Margin = new Padding(0, 8, 4, 0) };
    private readonly GroupBox _assistantGroup = new() { Dock = DockStyle.Fill, Padding = new Padding(12) };
    private readonly GroupBox _runtimeGroup = new() { Dock = DockStyle.Fill, Padding = new Padding(10) };

    private CancellationTokenSource? _cts;
    private RuntimeInfo? _runtime;
    private GuideResult? _lastGuide;

    public StartupForm()
    {
        _preferences = UserPreferences.Load();
        _localizer = new LocalizationService(_preferences.LanguageCode);
        _assistant = new GuidedAssistantService(_localizer);

        Text = "GISPlan";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 650);
        Size = new Size(980, 760);
        Font = new Font("Segoe UI", 10F);

        Controls.Add(BuildLayout());
        LoadLanguageOptions();
        _simpleMode.Checked = _preferences.SimpleMode;
        ApplySimpleMode();
        ApplyLanguage();

        Shown += async (_, _) => await DetectRuntimeAsync();
        _newJobButton.Click += (_, _) => OpenNewJob();
        _resumeButton.Click += async (_, _) => await ResumeJobAsync();
        _detectButton.Click += async (_, _) => await DetectRuntimeAsync();
        _cancelButton.Click += (_, _) => _cts?.Cancel();
        _guideButton.Click += (_, _) => ShowGuide(prepare: false);
        _prepareButton.Click += (_, _) => ShowGuide(prepare: true);
        _openGuideButton.Click += (_, _) => OpenGuideTarget();
        _command.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            ShowGuide(prepare: false);
        };
        _simpleMode.CheckedChanged += (_, _) =>
        {
            _preferences.SimpleMode = _simpleMode.Checked;
            _preferences.Save();
            ApplySimpleMode();
        };
        _detailsButton.Click += (_, _) =>
        {
            _runtimeGroup.Visible = !_runtimeGroup.Visible;
            UpdateDetailsButtonText();
        };
        _language.SelectedIndexChanged += (_, _) => ChangeLanguage();
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 6
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titlePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill
        };
        titlePanel.Controls.Add(new Label
        {
            Text = "GISPlan",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 24F, FontStyle.Bold)
        });
        titlePanel.Controls.Add(_subtitle);
        header.Controls.Add(titlePanel, 0, 0);

        var preferencePanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        preferencePanel.Controls.Add(_languageLabel);
        preferencePanel.Controls.Add(_language);
        preferencePanel.Controls.Add(_simpleMode);
        header.Controls.Add(preferencePanel, 1, 0);
        root.Controls.Add(header, 0, 0);

        var assistantLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        assistantLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        assistantLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        assistantLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        assistantLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _command.Dock = DockStyle.Top;
        assistantLayout.Controls.Add(_command, 0, 0);

        var assistantActions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 8, 0, 8) };
        assistantActions.Controls.AddRange([_guideButton, _prepareButton]);
        assistantLayout.Controls.Add(assistantActions, 0, 1);
        assistantLayout.Controls.Add(_assistantResult, 0, 2);
        assistantLayout.Controls.Add(_openGuideButton, 0, 3);
        _assistantGroup.Controls.Add(assistantLayout);
        root.Controls.Add(_assistantGroup, 0, 1);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 12, 0, 10)
        };
        actions.Controls.AddRange([_newJobButton, _resumeButton, _detectButton, _cancelButton, _detailsButton]);
        root.Controls.Add(actions, 0, 2);

        _runtimeGroup.Controls.Add(_log);
        root.Controls.Add(_runtimeGroup, 0, 3);
        root.Controls.Add(_status, 0, 4);
        root.Controls.Add(new Label
        {
            Text = "GISPlan keeps source data unchanged and asks before running supported workflows.",
            AutoSize = true,
            ForeColor = Color.Gray
        }, 0, 5);
        return root;
    }

    private void LoadLanguageOptions()
    {
        var options = _localizer.GetAvailableLanguages().ToList();
        _language.DataSource = options;
        _language.SelectedItem = options.FirstOrDefault(x => x.Code.Equals(_preferences.LanguageCode, StringComparison.OrdinalIgnoreCase))
                                 ?? options.First();
    }

    private void ChangeLanguage()
    {
        if (_language.SelectedItem is not LanguageOption option) return;
        _preferences.LanguageCode = option.Code;
        _preferences.Save();
        _localizer.SetLanguage(option.Code);
        ApplyLanguage();
        if (!string.IsNullOrWhiteSpace(_command.Text)) ShowGuide(prepare: false);
    }

    private void ApplyLanguage()
    {
        Text = _localizer.Text("app.title");
        _subtitle.Text = _localizer.Text("app.subtitle");
        _languageLabel.Text = _localizer.Text("language");
        _simpleMode.Text = _localizer.Text("simple_mode");
        _command.PlaceholderText = _localizer.Text("assistant.prompt");
        _guideButton.Text = _localizer.Text("assistant.guide");
        _prepareButton.Text = _localizer.Text("assistant.prepare");
        _openGuideButton.Text = _localizer.Text("assistant.open");
        _assistantGroup.Text = _localizer.Text("assistant.result");
        _newJobButton.Text = _localizer.Text("new_job");
        _resumeButton.Text = _localizer.Text("resume_job");
        _detectButton.Text = _localizer.Text("detect_runtime");
        _cancelButton.Text = _localizer.Text("cancel");
        _runtimeGroup.Text = _localizer.Text("runtime_status");
        if (string.IsNullOrWhiteSpace(_status.Text) || _status.Text == "พร้อมใช้งาน" || _status.Text == "Ready")
            _status.Text = _localizer.Text("ready");
        UpdateDetailsButtonText();
    }

    private void ApplySimpleMode()
    {
        _runtimeGroup.Visible = !_simpleMode.Checked;
        UpdateDetailsButtonText();
    }

    private void UpdateDetailsButtonText() =>
        _detailsButton.Text = _runtimeGroup.Visible
            ? _localizer.Text("hide_details")
            : _localizer.Text("show_details");

    private void ShowGuide(bool prepare)
    {
        if (string.IsNullOrWhiteSpace(_command.Text))
        {
            _assistantResult.Text = _localizer.Text("assistant.no_query");
            _openGuideButton.Enabled = false;
            return;
        }

        _lastGuide = _assistant.Find(_command.Text);
        _assistantResult.Text = _assistant.Format(_lastGuide);
        _openGuideButton.Enabled = _lastGuide.SuggestedOperation is not null;

        if (!prepare) return;
        if (_lastGuide.CanPrepareAutomatically && _lastGuide.SuggestedOperation is not null)
            OpenNewJob(_lastGuide.SuggestedOperation);
        else
            MessageBox.Show(this, _assistant.Format(_lastGuide), _localizer.Text("assistant.result"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenGuideTarget()
    {
        if (_lastGuide?.SuggestedOperation is GisOperation operation)
            OpenNewJob(operation);
    }

    private void OpenNewJob(GisOperation? operation = null)
    {
        Hide();
        try
        {
            using var form = new MainForm(_localizer, operation, _preferences.SimpleMode);
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
            SetBusy(true, _localizer.Text("status.checking_runtime"));
            _runtime = await new RuntimeDetector().DetectAsync();
            Append($"QGIS: {ShowPath(_runtime.QgisProcessPath)}");
            Append($"GDAL/OGR: {ShowPath(_runtime.Ogr2OgrPath)}");
            Append($"ArcGIS Pro / ArcPy: {ShowPath(_runtime.ArcGisPropyPath)}");
            foreach (var warning in _runtime.Warnings)
                Append("Warning: " + warning);
            _status.Text = _runtime.HasQgis || _runtime.HasGdal || _runtime.HasArcGis
                ? _localizer.Text("status.runtime_checked")
                : _localizer.Text("status.runtime_missing");
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
            Title = "Select gis_job.json",
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
            SetBusy(true, "Resume Job");
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
                var answer = MessageBox.Show(this, _localizer.Text("open_output"), "GISPlan", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (answer == DialogResult.Yes)
                    Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { openPath } });
            }
        }
        catch (OperationCanceledException)
        {
            Append(_localizer.Text("status.cancelled"));
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
        _guideButton.Enabled = !busy;
        _prepareButton.Enabled = !busy;
        _cancelButton.Enabled = busy;
        if (status is not null) _status.Text = status;
    }

    private void Append(string text) =>
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");

    private string ShowPath(string? path) => string.IsNullOrWhiteSpace(path) ? _localizer.Text("status.not_found") : path;
}
