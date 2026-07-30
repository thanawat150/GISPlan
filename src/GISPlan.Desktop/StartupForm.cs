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
    private readonly TextBox _command = new() { Font = new Font("Segoe UI", 12F), Height = 42 };
    private readonly ModernButton _guideButton = new() { Width = 170, Kind = ModernButtonKind.Primary };
    private readonly ModernButton _prepareButton = new() { Width = 195, Kind = ModernButtonKind.Secondary };
    private readonly ModernButton _openGuideButton = new() { Width = 205, Kind = ModernButtonKind.Success, Enabled = false };
    private readonly RichTextBox _assistantResult = new()
    {
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        BackColor = UiTheme.SurfaceMuted,
        ForeColor = UiTheme.Text,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 10.5F),
        DetectUrls = false
    };

    private readonly TextBox _log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        BorderStyle = BorderStyle.None,
        BackColor = UiTheme.Navy,
        ForeColor = Color.FromArgb(203, 213, 225),
        Dock = DockStyle.Fill,
        Font = new Font("Cascadia Mono", 9F)
    };

    private readonly ModernButton _newJobButton = new() { Height = 46, Kind = ModernButtonKind.Primary };
    private readonly ModernButton _resumeButton = new() { Height = 46, Kind = ModernButtonKind.Secondary };
    private readonly ModernButton _detectButton = new() { Height = 42, Kind = ModernButtonKind.Ghost };
    private readonly ModernButton _cancelButton = new() { Height = 42, Kind = ModernButtonKind.Danger, Enabled = false };
    private readonly ModernButton _detailsButton = new() { Height = 38, Kind = ModernButtonKind.Ghost };
    private readonly StatusPill _status = new();

    private readonly Label _subtitle = new();
    private readonly Label _languageLabel = new();
    private readonly Label _assistantTitle = new();
    private readonly Label _assistantHint = new();
    private readonly Label _quickTitle = new();
    private readonly Label _quickHint = new();
    private readonly Label _runtimeTitle = new();
    private readonly Label _footerNote = new();
    private readonly ModernCard _runtimeCard = new() { Dock = DockStyle.Top, Height = 270, BackColor = UiTheme.Navy, BorderColor = UiTheme.Navy };

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
        MinimumSize = new Size(980, 690);
        Size = new Size(1180, 790);
        UiTheme.ApplyForm(this);

        UiTheme.StyleInput(_language);
        UiTheme.StyleInput(_command);
        _simpleMode.Font = new Font("Segoe UI", 9.5F);
        _simpleMode.ForeColor = Color.White;
        _simpleMode.BackColor = UiTheme.Navy;

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
            _runtimeCard.Visible = !_runtimeCard.Visible;
            UpdateDetailsButtonText();
        };
        _language.SelectedIndexChanged += (_, _) => ChangeLanguage();
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Background,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        root.Controls.Add(BuildHero(), 0, 0);
        root.Controls.Add(BuildContent(), 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        return root;
    }

    private Control BuildHero()
    {
        var hero = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Navy,
            Padding = new Padding(30, 22, 30, 20)
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var brand = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        var mark = new Label
        {
            Text = "G",
            AutoSize = false,
            Size = new Size(58, 58),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = UiTheme.Primary,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 24F, FontStyle.Bold),
            Margin = new Padding(0, 0, 16, 0)
        };
        var titleStack = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 0)
        };
        titleStack.Controls.Add(new Label
        {
            Text = "GISPlan",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 25F, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(0)
        });
        _subtitle.AutoSize = true;
        _subtitle.MaximumSize = new Size(680, 0);
        _subtitle.Font = new Font("Segoe UI", 10F);
        _subtitle.ForeColor = Color.FromArgb(203, 213, 225);
        _subtitle.Margin = new Padding(1, 2, 0, 0);
        titleStack.Controls.Add(_subtitle);
        brand.Controls.Add(mark);
        brand.Controls.Add(titleStack);
        layout.Controls.Add(brand, 0, 0);

        var preferences = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Margin = new Padding(0, 8, 0, 0)
        };
        _languageLabel.AutoSize = true;
        _languageLabel.ForeColor = Color.FromArgb(203, 213, 225);
        _languageLabel.Margin = new Padding(0, 10, 8, 0);
        _language.BackColor = Color.White;
        _simpleMode.Margin = new Padding(16, 10, 0, 0);
        preferences.Controls.AddRange([_languageLabel, _language, _simpleMode]);
        layout.Controls.Add(preferences, 1, 0);

        hero.Controls.Add(layout);
        return hero;
    }

    private Control BuildContent()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 22, 24, 18),
            ColumnCount = 2,
            RowCount = 1,
            BackColor = UiTheme.Background
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));

        shell.Controls.Add(BuildAssistantCard(), 0, 0);
        shell.Controls.Add(BuildRightColumn(), 1, 0);
        return shell;
    }

    private Control BuildAssistantCard()
    {
        var card = new ModernCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _assistantTitle.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold);
        _assistantTitle.ForeColor = UiTheme.Text;
        _assistantTitle.AutoSize = true;
        _assistantHint.AutoSize = true;
        _assistantHint.MaximumSize = new Size(700, 0);
        _assistantHint.ForeColor = UiTheme.MutedText;
        _assistantHint.Margin = new Padding(0, 4, 0, 14);

        _command.Dock = DockStyle.Top;
        _command.Margin = new Padding(0, 0, 0, 10);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 0, 0, 12)
        };
        actions.Controls.AddRange([_guideButton, _prepareButton]);

        var resultSurface = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.SurfaceMuted,
            Padding = new Padding(16),
            Margin = new Padding(0, 2, 0, 12)
        };
        resultSurface.Controls.Add(_assistantResult);

        _openGuideButton.Anchor = AnchorStyles.Left;
        layout.Controls.Add(_assistantTitle, 0, 0);
        layout.Controls.Add(_assistantHint, 0, 1);
        layout.Controls.Add(_command, 0, 2);
        layout.Controls.Add(resultSurface, 0, 3);
        layout.Controls.Add(_openGuideButton, 0, 4);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildRightColumn()
    {
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Margin = new Padding(10, 0, 0, 0),
            Padding = new Padding(0)
        };

        var quickCard = new ModernCard { Height = 304, Margin = new Padding(0, 0, 0, 14) };
        var quickLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
        quickLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        quickLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        quickLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        quickLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        quickLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        quickLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _quickTitle.AutoSize = true;
        _quickTitle.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold);
        _quickTitle.ForeColor = UiTheme.Text;
        _quickHint.AutoSize = true;
        _quickHint.MaximumSize = new Size(320, 0);
        _quickHint.ForeColor = UiTheme.MutedText;
        _quickHint.Margin = new Padding(0, 3, 0, 14);

        foreach (var button in new[] { _newJobButton, _resumeButton, _detectButton, _detailsButton })
        {
            button.Dock = DockStyle.Top;
            button.Margin = new Padding(0, 0, 0, 9);
        }
        _cancelButton.Dock = DockStyle.Top;
        _cancelButton.Margin = new Padding(0, 0, 0, 0);

        quickLayout.Controls.Add(_quickTitle, 0, 0);
        quickLayout.Controls.Add(_quickHint, 0, 1);
        quickLayout.Controls.Add(_newJobButton, 0, 2);
        quickLayout.Controls.Add(_resumeButton, 0, 3);
        quickLayout.Controls.Add(_detectButton, 0, 4);

        var bottomActions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        bottomActions.Controls.AddRange([_detailsButton, _cancelButton]);
        quickLayout.Controls.Add(bottomActions, 0, 5);
        quickCard.Controls.Add(quickLayout);

        var runtimeLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        runtimeLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        runtimeLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _runtimeTitle.AutoSize = true;
        _runtimeTitle.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
        _runtimeTitle.ForeColor = Color.White;
        _runtimeTitle.Margin = new Padding(0, 0, 0, 10);
        runtimeLayout.Controls.Add(_runtimeTitle, 0, 0);
        runtimeLayout.Controls.Add(_log, 0, 1);
        _runtimeCard.Controls.Add(runtimeLayout);

        stack.Controls.Add(quickCard);
        stack.Controls.Add(_runtimeCard);
        stack.SizeChanged += (_, _) =>
        {
            var width = Math.Max(260, stack.ClientSize.Width - (stack.VerticalScroll.Visible ? 24 : 4));
            quickCard.Width = width;
            _runtimeCard.Width = width;
        };
        return stack;
    }

    private Control BuildFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(24, 10, 24, 8)
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _footerNote.AutoSize = true;
        _footerNote.ForeColor = UiTheme.MutedText;
        _footerNote.Anchor = AnchorStyles.Left;
        layout.Controls.Add(_footerNote, 0, 0);
        layout.Controls.Add(_status, 1, 0);
        footer.Controls.Add(layout);
        return footer;
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
        var thai = _localizer.LanguageCode.StartsWith("th", StringComparison.OrdinalIgnoreCase);
        Text = _localizer.Text("app.title");
        _subtitle.Text = _localizer.Text("app.subtitle");
        _languageLabel.Text = _localizer.Text("language");
        _simpleMode.Text = _localizer.Text("simple_mode");
        _command.PlaceholderText = _localizer.Text("assistant.prompt");
        _guideButton.Text = _localizer.Text("assistant.guide");
        _prepareButton.Text = _localizer.Text("assistant.prepare");
        _openGuideButton.Text = _localizer.Text("assistant.open");
        _assistantTitle.Text = thai ? "ผู้ช่วยงาน GIS" : "GIS Assistant";
        _assistantHint.Text = thai
            ? "อธิบายงานที่ต้องการด้วยภาษาปกติ ระบบจะบอกขั้นตอนหรือเตรียมหน้าทำงานให้"
            : "Describe the result you need. GISPlan will guide you or prepare a supported workflow.";
        _quickTitle.Text = thai ? "เริ่มต้นอย่างรวดเร็ว" : "Quick start";
        _quickHint.Text = thai ? "เลือกเริ่มงานใหม่ ทำงานต่อ หรือตรวจโปรแกรม GIS ในเครื่อง" : "Start a task, resume previous work, or check installed GIS software.";
        _runtimeTitle.Text = _localizer.Text("runtime_status");
        _newJobButton.Text = _localizer.Text("new_job");
        _resumeButton.Text = _localizer.Text("resume_job");
        _detectButton.Text = _localizer.Text("detect_runtime");
        _cancelButton.Text = _localizer.Text("cancel");
        _footerNote.Text = thai
            ? "ไฟล์ต้นฉบับจะไม่ถูกแก้ไข และระบบจะขอยืนยันก่อนเริ่มงาน"
            : "Source files remain unchanged and supported workflows require confirmation.";
        if (string.IsNullOrWhiteSpace(_status.Text) || _status.Text is "พร้อมใช้งาน" or "Ready")
            _status.SetNeutral(_localizer.Text("ready"));
        UpdateDetailsButtonText();
    }

    private void ApplySimpleMode()
    {
        _runtimeCard.Visible = !_simpleMode.Checked;
        UpdateDetailsButtonText();
    }

    private void UpdateDetailsButtonText() =>
        _detailsButton.Text = _runtimeCard.Visible
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
            var ready = _runtime.HasQgis || _runtime.HasGdal || _runtime.HasArcGis;
            if (ready) _status.SetSuccess(_localizer.Text("status.runtime_checked"));
            else _status.SetWarning(_localizer.Text("status.runtime_missing"));
        }
        catch (Exception ex)
        {
            Append("Runtime Error: " + ex.Message);
            _status.SetError("rework");
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
                _status.SetBusy(text);
            });
            var result = await new JobResumeService().ResumeAsync(dialog.FileName, _runtime, progress, _cts.Token);
            Append($"Resume result: {result.Status} — {result.Message}");
            Append($"Job folder: {result.JobDirectory}");
            if (!string.IsNullOrWhiteSpace(result.OutputPath))
                Append($"Output: {result.OutputPath}");
            SetResultStatus(result.Status);

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
            _status.SetWarning(_localizer.Text("status.cancelled"));
        }
        catch (Exception ex)
        {
            Append("Resume Error: " + ex.Message);
            _status.SetError("rework");
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
        if (status is not null) _status.SetBusy(status);
    }

    private void SetResultStatus(string status)
    {
        if (status is "passed" or "passed_with_warnings") _status.SetSuccess(status);
        else if (status is "cancelled" or "missing_runtime") _status.SetWarning(status);
        else _status.SetError(status);
    }

    private void Append(string text) =>
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");

    private string ShowPath(string? path) => string.IsNullOrWhiteSpace(path) ? _localizer.Text("status.not_found") : path;
}
