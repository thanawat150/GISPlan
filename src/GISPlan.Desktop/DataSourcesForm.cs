using System.Diagnostics;
using GISPlan.Core;

namespace GISPlan.Desktop;

public sealed class DataSourcesForm : Form
{
    private sealed record SourceChoice(ExternalDataSource Source)
    {
        public override string ToString() => Source.Name;
    }

    private readonly LocalizationService _localizer;
    private readonly bool _thai;
    private readonly ExternalDataSourceRegistry _registry = new();
    private readonly ExternalCatalogSearchService _searchService = new();

    private readonly TextBox _sourceFilter = new();
    private readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ListBox _sources = new() { BorderStyle = BorderStyle.None, IntegralHeight = false };
    private readonly Label _sourceCount = new();

    private readonly Label _name = new();
    private readonly Label _organization = new();
    private readonly StatusPill _authority = new();
    private readonly Label _summary = new();
    private readonly Label _license = new();
    private readonly Label _caution = new();
    private readonly Label _access = new();

    private readonly TextBox _onlineQuery = new();
    private readonly ModernButton _searchButton = new() { Kind = ModernButtonKind.Primary, Width = 130 };
    private readonly ModernButton _openPortalButton = new() { Kind = ModernButtonKind.Ghost, Width = 135 };
    private readonly ModernButton _downloadButton = new() { Kind = ModernButtonKind.Success, Width = 165, Enabled = false };
    private readonly ModernButton _copyButton = new() { Kind = ModernButtonKind.Secondary, Width = 155, Enabled = false };
    private readonly DataGridView _results = new();
    private readonly ProgressBar _progress = new() { Visible = false, Style = ProgressBarStyle.Continuous };
    private readonly StatusPill _status = new();

    private CancellationTokenSource? _cts;
    private IReadOnlyList<ExternalDataResult> _currentResults = [];

    public DataSourcesForm(LocalizationService localizer)
    {
        _localizer = localizer;
        _thai = localizer.LanguageCode.StartsWith("th", StringComparison.OrdinalIgnoreCase);

        Text = _thai ? "GISPlan — คลังข้อมูลภายนอก" : "GISPlan — External data sources";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1120, 720);
        Size = new Size(1320, 850);
        UiTheme.ApplyForm(this);

        UiTheme.StyleInput(_sourceFilter);
        UiTheme.StyleInput(_category);
        UiTheme.StyleInput(_onlineQuery);
        ConfigureResultsGrid();
        Controls.Add(BuildLayout());

        _sourceFilter.PlaceholderText = _thai ? "ค้นหาแหล่งข้อมูล เช่น ขอบเขต DEM ป่าชายเลน..." : "Filter sources: boundaries, DEM, mangrove...";
        _onlineQuery.PlaceholderText = _thai ? "ค้นหาใน Catalog ที่เลือก เช่น ขอบเขตตำบล, DEM, Sentinel-2..." : "Search the selected catalog: boundaries, DEM, Sentinel-2...";
        _searchButton.Text = _thai ? "ค้นหาออนไลน์" : "Search online";
        _openPortalButton.Text = _thai ? "เปิดเว็บไซต์" : "Open portal";
        _downloadButton.Text = _thai ? "ดาวน์โหลดรายการนี้" : "Download selected";
        _copyButton.Text = _thai ? "คัดลอกแหล่งอ้างอิง" : "Copy attribution";
        _status.SetNeutral(_localizer.Text("ready"));

        LoadCategories();
        RefreshSources();

        _sourceFilter.TextChanged += (_, _) => RefreshSources();
        _category.SelectedIndexChanged += (_, _) => RefreshSources();
        _sources.SelectedIndexChanged += (_, _) => ShowSelectedSource();
        _searchButton.Click += async (_, _) => await SearchOnlineAsync();
        _openPortalButton.Click += (_, _) => OpenSelectedPortal();
        _downloadButton.Click += async (_, _) => await DownloadSelectedAsync();
        _copyButton.Click += (_, _) => CopySelectedAttribution();
        _results.SelectionChanged += (_, _) => UpdateResultActions();
        _onlineQuery.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            await SearchOnlineAsync();
        };
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = UiTheme.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 98));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
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
            Padding = new Padding(28, 18, 28, 15)
        };
        var title = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        title.Controls.Add(new Label
        {
            Text = _thai ? "คลังข้อมูลภูมิสารสนเทศ" : "Geospatial data catalog",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 23F, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(0)
        });
        title.Controls.Add(new Label
        {
            Text = _thai
                ? "ค้นหาแหล่งข้อมูลทางการและข้อมูลเปิด พร้อมตรวจที่มา License ความละเอียด และข้อจำกัดก่อนดาวน์โหลด"
                : "Find official and open datasets with provenance, licensing, resolution and usage cautions.",
            AutoSize = true,
            MaximumSize = new Size(1000, 0),
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 10F),
            Margin = new Padding(1, 4, 0, 0)
        });
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildWorkspace()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(22, 20, 22, 18)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(BuildSourcesCard(), 0, 0);
        layout.Controls.Add(BuildDetailsCard(), 1, 0);
        layout.Controls.Add(BuildSearchCard(), 2, 0);
        return layout;
    }

    private Control BuildSourcesCard()
    {
        var card = new ModernCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 9, 0) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(UiTheme.Heading(_thai ? "แหล่งข้อมูล" : "Data sources", 16F), 0, 0);
        var hint = UiTheme.Caption(_thai ? "เลือกแหล่งที่เหมาะกับงานและระดับความน่าเชื่อถือ" : "Choose a source by purpose and authority level.", 260);
        hint.Margin = new Padding(0, 3, 0, 12);
        layout.Controls.Add(hint, 0, 1);
        _sourceFilter.Dock = DockStyle.Top;
        _category.Dock = DockStyle.Top;
        var filters = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2 };
        filters.Controls.Add(_sourceFilter, 0, 0);
        filters.Controls.Add(_category, 0, 1);
        layout.Controls.Add(filters, 0, 2);

        _sources.Dock = DockStyle.Fill;
        _sources.BackColor = UiTheme.Surface;
        _sources.ForeColor = UiTheme.Text;
        _sources.Font = new Font("Segoe UI", 10F);
        layout.Controls.Add(_sources, 0, 3);
        _sourceCount.AutoSize = true;
        _sourceCount.ForeColor = UiTheme.MutedText;
        _sourceCount.Margin = new Padding(0, 8, 0, 0);
        layout.Controls.Add(_sourceCount, 0, 4);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildDetailsCard()
    {
        var card = new ModernCard { Dock = DockStyle.Fill, Margin = new Padding(9, 0, 9, 0) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, AutoScroll = true };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _name.AutoSize = true;
        _name.MaximumSize = new Size(315, 0);
        _name.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold);
        _name.ForeColor = UiTheme.Text;
        _organization.AutoSize = true;
        _organization.MaximumSize = new Size(315, 0);
        _organization.ForeColor = UiTheme.MutedText;
        _organization.Margin = new Padding(0, 4, 0, 10);
        _authority.Margin = new Padding(0, 0, 0, 14);

        ConfigureDetailLabel(_summary);
        ConfigureDetailLabel(_access);
        ConfigureDetailLabel(_license);
        ConfigureDetailLabel(_caution, true);
        _openPortalButton.Anchor = AnchorStyles.Left;
        _openPortalButton.Margin = new Padding(0, 12, 0, 0);

        layout.Controls.Add(_name, 0, 0);
        layout.Controls.Add(_organization, 0, 1);
        layout.Controls.Add(_authority, 0, 2);
        layout.Controls.Add(Section(_thai ? "รายละเอียดข้อมูล" : "Data details", _summary), 0, 3);
        layout.Controls.Add(Section(_thai ? "การเข้าถึง" : "Access", _access), 0, 4);
        layout.Controls.Add(Section(_thai ? "License และการอ้างอิง" : "License and attribution", _license), 0, 5);
        layout.Controls.Add(Section(_thai ? "ข้อควรระวัง" : "Caution", _caution, warning: true), 0, 6);
        layout.Controls.Add(_openPortalButton, 0, 7);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildSearchCard()
    {
        var card = new ModernCard { Dock = DockStyle.Fill, Margin = new Padding(9, 0, 0, 0) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(UiTheme.Heading(_thai ? "ค้นหาและดาวน์โหลด" : "Search and download", 16F), 0, 0);
        var hint = UiTheme.Caption(_thai
            ? "ระบบจะค้น Catalog ของแหล่งที่เลือก การค้นหาต้องใช้อินเทอร์เน็ต แต่จะไม่ส่งไฟล์ GIS ของคุณออกไป"
            : "Search the selected provider catalog. Your local GIS files are not uploaded.", 560);
        hint.Margin = new Padding(0, 3, 0, 12);
        layout.Controls.Add(hint, 0, 1);

        var searchRow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _onlineQuery.Dock = DockStyle.Top;
        _searchButton.Margin = new Padding(8, 6, 0, 6);
        searchRow.Controls.Add(_onlineQuery, 0, 0);
        searchRow.Controls.Add(_searchButton, 1, 0);
        layout.Controls.Add(searchRow, 0, 2);
        layout.Controls.Add(_results, 0, 3);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var actions = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        actions.Controls.AddRange([_copyButton, _downloadButton]);
        _progress.Dock = DockStyle.Top;
        _progress.Height = 7;
        _progress.Margin = new Padding(0, 12, 12, 0);
        bottom.Controls.Add(_progress, 0, 0);
        bottom.Controls.Add(actions, 1, 0);
        layout.Controls.Add(bottom, 0, 4);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildFooter()
    {
        var footer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(22, 8, 22, 8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label
        {
            Text = _thai
                ? "ตรวจปีข้อมูล CRS มาตราส่วน ความละเอียด และ License ทุกครั้งก่อนใช้ในรายงานหรือการตัดสินใจ"
                : "Verify date, CRS, scale, resolution and license before analysis or publication.",
            AutoSize = true,
            ForeColor = UiTheme.MutedText,
            Anchor = AnchorStyles.Left
        }, 0, 0);
        layout.Controls.Add(_status, 1, 0);
        footer.Controls.Add(layout);
        return footer;
    }

    private void ConfigureResultsGrid()
    {
        _results.Dock = DockStyle.Fill;
        _results.BackgroundColor = Color.White;
        _results.BorderStyle = BorderStyle.None;
        _results.AllowUserToAddRows = false;
        _results.AllowUserToDeleteRows = false;
        _results.AllowUserToResizeRows = false;
        _results.MultiSelect = false;
        _results.ReadOnly = true;
        _results.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _results.AutoGenerateColumns = false;
        _results.RowHeadersVisible = false;
        _results.EnableHeadersVisualStyles = false;
        _results.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.SurfaceMuted;
        _results.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.Text;
        _results.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _results.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
        _results.DefaultCellStyle.SelectionForeColor = UiTheme.Text;
        _results.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
        _results.RowTemplate.Height = 34;
        _results.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = _thai ? "ชื่อชุดข้อมูล / Resource" : "Dataset / resource",
            DataPropertyName = nameof(ExternalDataResult.Title),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 72
        });
        _results.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = _thai ? "รูปแบบ" : "Format",
            DataPropertyName = nameof(ExternalDataResult.Format),
            Width = 110
        });
        _results.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = _thai ? "ดาวน์โหลด" : "Download",
            Name = "downloadStatus",
            Width = 95
        });
    }

    private void LoadCategories()
    {
        var all = _thai ? "ทั้งหมด" : "All";
        var categories = new List<string> { all };
        categories.AddRange(_registry.Categories());
        _category.DataSource = categories;
    }

    private void RefreshSources()
    {
        var category = _category.SelectedItem?.ToString();
        if (category is "All") category = "ทั้งหมด";
        var matches = _registry.Filter(_sourceFilter.Text, category)
            .Select(x => new SourceChoice(x))
            .ToList();
        var selectedId = SelectedSource?.Id;
        _sources.DataSource = matches;
        if (selectedId is not null)
        {
            var index = matches.FindIndex(x => x.Source.Id == selectedId);
            if (index >= 0) _sources.SelectedIndex = index;
        }
        if (_sources.SelectedIndex < 0 && matches.Count > 0) _sources.SelectedIndex = 0;
        _sourceCount.Text = _thai ? $"พบ {matches.Count} แหล่งข้อมูล" : $"{matches.Count} sources";
    }

    private ExternalDataSource? SelectedSource => (_sources.SelectedItem as SourceChoice)?.Source;

    private ExternalDataResult? SelectedResult
    {
        get
        {
            if (_results.CurrentRow?.DataBoundItem is ExternalDataResult result) return result;
            return null;
        }
    }

    private void ShowSelectedSource()
    {
        var source = SelectedSource;
        if (source is null) return;
        _name.Text = source.Name;
        _organization.Text = source.Organization;
        switch (source.Authority)
        {
            case DataAuthorityLevel.Official:
                _authority.SetSuccess(_thai ? "แหล่งข้อมูลทางการ" : "Official source");
                break;
            case DataAuthorityLevel.OpenResearch:
                _authority.SetBusy(_thai ? "ข้อมูลเปิดด้านวิจัย" : "Open research data");
                break;
            default:
                _authority.SetWarning(_thai ? "ข้อมูลชุมชน" : "Community data");
                break;
        }

        _summary.Text = $"{string.Join(" • ", source.Categories)}\n\n{(_thai ? "พื้นที่ครอบคลุม" : "Coverage")}: {source.Coverage}\n{(_thai ? "ความละเอียด" : "Resolution")}: {source.Resolution}\n{(_thai ? "รูปแบบ" : "Data types")}: {source.DataTypes}";
        _access.Text = $"{(_thai ? "วิธีเข้าถึง" : "Access")}: {source.AccessKind}\n{(_thai ? "ต้องมีบัญชี" : "Account required")}: {(source.RequiresAccount ? (_thai ? "ใช่" : "Yes") : (_thai ? "ไม่จำเป็นสำหรับ Catalog/บางรายการ" : "Not for catalog/some resources"))}";
        _license.Text = $"{source.LicenseNote}\n\n{(_thai ? "ข้อความอ้างอิง" : "Attribution")}: {source.Attribution}";
        _caution.Text = source.Caution;
        _currentResults = [];
        _results.DataSource = null;
        UpdateResultActions();
        _status.SetNeutral(_localizer.Text("ready"));
    }

    private async Task SearchOnlineAsync()
    {
        var source = SelectedSource;
        if (source is null) return;
        if (string.IsNullOrWhiteSpace(_onlineQuery.Text))
        {
            _status.SetWarning(_thai ? "กรุณาพิมพ์คำค้น" : "Enter a search term");
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        try
        {
            SetBusy(true, _thai ? "กำลังค้นหา Catalog" : "Searching catalog");
            _currentResults = await _searchService.SearchAsync(source, _onlineQuery.Text.Trim(), _cts.Token);
            _results.DataSource = _currentResults.ToList();
            foreach (DataGridViewRow row in _results.Rows)
            {
                if (row.DataBoundItem is ExternalDataResult item)
                    row.Cells["downloadStatus"].Value = item.DownloadUri is not null
                        ? (_thai ? "มีไฟล์ตรง" : "Direct")
                        : item.RequiresAccount ? (_thai ? "เปิด Portal" : "Portal") : (_thai ? "ดูรายละเอียด" : "Details");
            }
            if (_currentResults.Count == 0)
                _status.SetWarning(_thai ? "ไม่พบรายการ ลองใช้คำค้นสั้นลงหรือเปิดเว็บไซต์ต้นทาง" : "No results. Try a shorter query or open the portal.");
            else
                _status.SetSuccess(_thai ? $"พบ {_currentResults.Count} รายการ" : $"{_currentResults.Count} results");
        }
        catch (OperationCanceledException)
        {
            _status.SetWarning(_localizer.Text("status.cancelled"));
        }
        catch (Exception ex)
        {
            _status.SetError(_thai ? "ค้นหาไม่สำเร็จ" : "Search failed");
            MessageBox.Show(this,
                (_thai ? "ไม่สามารถเชื่อมต่อ Catalog นี้ได้ในขณะนี้\n\n" : "The catalog could not be reached.\n\n") + ex.Message,
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task DownloadSelectedAsync()
    {
        var result = SelectedResult;
        if (result?.DownloadUri is null) return;

        var suggested = SafeFileName(Path.GetFileName(result.DownloadUri.AbsolutePath));
        if (string.IsNullOrWhiteSpace(suggested)) suggested = "downloaded_dataset";
        using var dialog = new SaveFileDialog
        {
            Title = _thai ? "เลือกที่เก็บข้อมูล" : "Save dataset",
            FileName = suggested,
            InitialDirectory = AppPaths.DefaultOutputRoot,
            OverwritePrompt = true,
            Filter = "All files|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var confirmation = MessageBox.Show(this,
            (_thai
                ? $"แหล่งข้อมูล: {result.Title}\nLicense: {result.LicenseNote}\nที่มา: {result.Attribution}\n\nดาวน์โหลดไปที่:\n{dialog.FileName}"
                : $"Dataset: {result.Title}\nLicense: {result.LicenseNote}\nAttribution: {result.Attribution}\n\nDownload to:\n{dialog.FileName}"),
            _thai ? "ยืนยันการดาวน์โหลด" : "Confirm download",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information);
        if (confirmation != DialogResult.OK) return;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        try
        {
            _progress.Visible = true;
            _progress.Style = ProgressBarStyle.Marquee;
            SetBusy(true, _thai ? "กำลังดาวน์โหลด" : "Downloading");
            var progress = new Progress<(long Received, long? Total)>(value =>
            {
                if (value.Total is > 0)
                {
                    _progress.Style = ProgressBarStyle.Continuous;
                    _progress.Maximum = 1000;
                    _progress.Value = Math.Clamp((int)(value.Received * 1000L / value.Total.Value), 0, 1000);
                    _status.SetBusy($"{FormatBytes(value.Received)} / {FormatBytes(value.Total.Value)}");
                }
                else
                {
                    _status.SetBusy(FormatBytes(value.Received));
                }
            });
            var path = await _searchService.DownloadAsync(result, dialog.FileName, progress, _cts.Token);
            _status.SetSuccess(_thai ? "ดาวน์โหลดและบันทึก Metadata แล้ว" : "Downloaded with metadata sidecar");
            var open = MessageBox.Show(this,
                _thai ? "ดาวน์โหลดเสร็จแล้ว ต้องการเปิดโฟลเดอร์หรือไม่" : "Download complete. Open the folder?",
                Text,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (open == DialogResult.Yes)
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (OperationCanceledException)
        {
            _status.SetWarning(_localizer.Text("status.cancelled"));
        }
        catch (Exception ex)
        {
            _status.SetError(_thai ? "ดาวน์โหลดไม่สำเร็จ" : "Download failed");
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _progress.Visible = false;
            _progress.Value = 0;
            SetBusy(false);
        }
    }

    private void OpenSelectedPortal()
    {
        var uri = SelectedResult?.LandingUri ?? SelectedSource?.PortalUri;
        if (uri is null) return;
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private void CopySelectedAttribution()
    {
        var result = SelectedResult;
        if (result is null) return;
        Clipboard.SetText($"{result.Title}\n{result.Attribution}\n{result.LicenseNote}\n{result.LandingUri}");
        _status.SetSuccess(_thai ? "คัดลอกแหล่งอ้างอิงแล้ว" : "Attribution copied");
    }

    private void UpdateResultActions()
    {
        var result = SelectedResult;
        _downloadButton.Enabled = result?.DownloadUri is not null;
        _copyButton.Enabled = result is not null;
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _searchButton.Enabled = !busy;
        _downloadButton.Enabled = !busy && SelectedResult?.DownloadUri is not null;
        _copyButton.Enabled = !busy && SelectedResult is not null;
        _openPortalButton.Enabled = !busy;
        _sources.Enabled = !busy;
        _category.Enabled = !busy;
        if (message is not null) _status.SetBusy(message);
    }

    private static Control Section(string title, Label content, bool warning = false)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 10),
            BackColor = warning ? Color.FromArgb(255, 251, 235) : UiTheme.SurfaceMuted
        };
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        layout.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = warning ? UiTheme.Warning : UiTheme.Primary,
            Margin = new Padding(0, 0, 0, 5)
        });
        layout.Controls.Add(content);
        panel.Controls.Add(layout);
        return panel;
    }

    private static void ConfigureDetailLabel(Label label, bool warning = false)
    {
        label.AutoSize = true;
        label.MaximumSize = new Size(300, 0);
        label.ForeColor = warning ? Color.FromArgb(146, 64, 14) : UiTheme.Text;
        label.Font = new Font("Segoe UI", 9.25F);
        label.Margin = new Padding(0);
    }

    private static string SafeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
        return value;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }
        return $"{value:0.##} {units[index]}";
    }
}
