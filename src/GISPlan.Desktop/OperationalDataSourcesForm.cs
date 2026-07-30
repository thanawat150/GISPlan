using System.Diagnostics;
using System.Globalization;
using GISPlan.Core;

namespace GISPlan.Desktop;

/// <summary>
/// Production-oriented external data workflow. It separates catalog discovery from item search,
/// validates spatial/date inputs, downloads only direct HTTPS assets, and can open completed data
/// in QGIS Desktop when it is installed.
/// </summary>
public sealed class OperationalDataSourcesForm : Form
{
    private sealed record SourceChoice(ExternalDataSource Source)
    {
        public override string ToString() => Source.Name;
    }

    private readonly LocalizationService _localizer;
    private readonly bool _thai;
    private readonly ExternalDataSourceRegistry _registry = new();
    private readonly ExternalCatalogSearchService _service = new();

    private readonly TextBox _sourceFilter = new();
    private readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ListBox _sources = new() { BorderStyle = BorderStyle.None, IntegralHeight = false };
    private readonly Label _sourceCount = new();

    private readonly Label _sourceName = new();
    private readonly Label _sourceOrganization = new();
    private readonly StatusPill _authority = new();
    private readonly Label _sourceDetails = new();
    private readonly Label _sourceLicense = new();
    private readonly Label _sourceCaution = new();
    private readonly StatusPill _providerStatus = new();
    private readonly ModernButton _probeButton = new() { Kind = ModernButtonKind.Ghost, Width = 155 };
    private readonly ModernButton _portalButton = new() { Kind = ModernButtonKind.Secondary, Width = 145 };

    private readonly TextBox _query = new();
    private readonly TextBox _collectionId = new();
    private readonly TextBox _bbox = new();
    private readonly DateTimePicker _startDate = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
    private readonly DateTimePicker _endDate = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
    private readonly NumericUpDown _cloud = new() { Minimum = 0, Maximum = 100, Value = 20, DecimalPlaces = 0 };
    private readonly CheckBox _useCloud = new() { AutoSize = true };
    private readonly NumericUpDown _limit = new() { Minimum = 1, Maximum = 200, Value = 40 };
    private readonly ModernButton _thailandButton = new() { Kind = ModernButtonKind.Ghost, Width = 150 };
    private readonly ModernButton _searchButton = new() { Kind = ModernButtonKind.Primary, Width = 145 };
    private readonly ModernButton _useCollectionButton = new() { Kind = ModernButtonKind.Secondary, Width = 190, Enabled = false };
    private readonly ModernButton _clearCollectionButton = new() { Kind = ModernButtonKind.Ghost, Width = 145 };

    private readonly DataGridView _results = new();
    private readonly RichTextBox _resultDetails = new()
    {
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        BackColor = UiTheme.SurfaceMuted,
        ForeColor = UiTheme.Text,
        Dock = DockStyle.Fill,
        DetectUrls = false
    };
    private readonly ModernButton _downloadButton = new() { Kind = ModernButtonKind.Success, Width = 160, Enabled = false };
    private readonly ModernButton _openLandingButton = new() { Kind = ModernButtonKind.Ghost, Width = 145, Enabled = false };
    private readonly ModernButton _copyCitationButton = new() { Kind = ModernButtonKind.Secondary, Width = 155, Enabled = false };
    private readonly ModernButton _openQgisButton = new() { Kind = ModernButtonKind.Primary, Width = 160, Enabled = false };
    private readonly ModernButton _openFolderButton = new() { Kind = ModernButtonKind.Ghost, Width = 145, Enabled = false };
    private readonly ProgressBar _progress = new() { Visible = false, Style = ProgressBarStyle.Continuous };
    private readonly StatusPill _status = new();

    private IReadOnlyList<ExternalDataResult> _currentResults = [];
    private CancellationTokenSource? _cts;
    private string? _lastDownloadedPath;
    private RuntimeInfo? _runtime;

    public OperationalDataSourcesForm(LocalizationService localizer)
    {
        _localizer = localizer;
        _thai = localizer.LanguageCode.StartsWith("th", StringComparison.OrdinalIgnoreCase);

        Text = _thai ? "GISPlan — คลังข้อมูลที่ใช้งานได้จริง" : "GISPlan — Operational data catalog";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1180, 760);
        Size = new Size(1440, 900);
        UiTheme.ApplyForm(this);

        foreach (var control in new Control[] { _sourceFilter, _category, _query, _collectionId, _bbox, _startDate, _endDate, _cloud, _limit })
            UiTheme.StyleInput(control);
        ConfigureGrid();
        Controls.Add(BuildLayout());
        ApplyText();
        LoadCategories();
        RefreshSources();

        _sourceFilter.TextChanged += (_, _) => RefreshSources();
        _category.SelectedIndexChanged += (_, _) => RefreshSources();
        _sources.SelectedIndexChanged += (_, _) => ShowSelectedSource();
        _probeButton.Click += async (_, _) => await ProbeSelectedSourceAsync();
        _portalButton.Click += (_, _) => OpenUri(SelectedSource?.PortalUri);
        _thailandButton.Click += (_, _) => _bbox.Text = "97.3,5.6,105.7,20.5";
        _searchButton.Click += async (_, _) => await SearchAsync();
        _useCollectionButton.Click += async (_, _) => await UseSelectedCollectionAsync();
        _clearCollectionButton.Click += (_, _) => { _collectionId.Clear(); UpdateSearchMode(); };
        _results.SelectionChanged += (_, _) => ShowSelectedResult();
        _downloadButton.Click += async (_, _) => await DownloadSelectedAsync();
        _openLandingButton.Click += (_, _) => OpenUri(SelectedResult?.LandingUri);
        _copyCitationButton.Click += (_, _) => CopyCitation();
        _openQgisButton.Click += async (_, _) => await OpenLastDownloadInQgisAsync();
        _openFolderButton.Click += (_, _) => OpenLastDownloadFolder();
        _query.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            await SearchAsync();
        };
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Background,
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 98));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildWorkspace(), 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        return root;
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Navy, Padding = new Padding(28, 17, 28, 14) };
        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        stack.Controls.Add(new Label
        {
            Text = _thai ? "คลังข้อมูลภูมิสารสนเทศ — ใช้งานจริง" : "Operational geospatial data catalog",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(0)
        });
        stack.Controls.Add(new Label
        {
            Text = _thai
                ? "ค้น Catalog จริง กำหนด AOI/วันที่ ดาวน์โหลดผ่าน HTTPS บันทึก SHA-256 และเปิดผลลัพธ์ใน QGIS"
                : "Search live catalogs, filter by AOI/date, download over HTTPS, record SHA-256, and open results in QGIS.",
            AutoSize = true,
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 10F),
            Margin = new Padding(1, 4, 0, 0)
        });
        panel.Controls.Add(stack);
        return panel;
    }

    private Control BuildWorkspace()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 420,
            BackColor = UiTheme.Background,
            Padding = new Padding(20, 18, 20, 16)
        };
        split.Panel1.Padding = new Padding(0, 0, 10, 0);
        split.Panel2.Padding = new Padding(10, 0, 0, 0);
        split.Panel1.Controls.Add(BuildLeftColumn());
        split.Panel2.Controls.Add(BuildRightColumn());
        return split;
    }

    private Control BuildLeftColumn()
    {
        var stack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
        stack.Controls.Add(BuildSourceListCard(), 0, 0);
        stack.Controls.Add(BuildSourceDetailsCard(), 0, 1);
        return stack;
    }

    private Control BuildSourceListCard()
    {
        var card = new ModernCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(UiTheme.Heading(_thai ? "1. เลือกแหล่งข้อมูล" : "1. Choose a provider", 16F), 0, 0);
        var hint = UiTheme.Caption(_thai ? "เลือกหน่วยงานหรือ Catalog ให้ตรงกับข้อมูลที่ต้องการ" : "Choose the catalog that best matches the required data.", 360);
        hint.Margin = new Padding(0, 3, 0, 10);
        layout.Controls.Add(hint, 0, 1);
        var filters = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        _sourceFilter.Dock = DockStyle.Top;
        _category.Dock = DockStyle.Top;
        filters.Controls.Add(_sourceFilter, 0, 0);
        filters.Controls.Add(_category, 1, 0);
        layout.Controls.Add(filters, 0, 2);
        _sources.Dock = DockStyle.Fill;
        _sources.BackColor = Color.White;
        _sources.ForeColor = UiTheme.Text;
        _sources.Font = new Font("Segoe UI", 10F);
        layout.Controls.Add(_sources, 0, 3);
        _sourceCount.AutoSize = true;
        _sourceCount.ForeColor = UiTheme.MutedText;
        _sourceCount.Margin = new Padding(0, 7, 0, 0);
        layout.Controls.Add(_sourceCount, 0, 4);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildSourceDetailsCard()
    {
        var card = new ModernCard { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 1, RowCount = 8 };
        _sourceName.AutoSize = true;
        _sourceName.MaximumSize = new Size(360, 0);
        _sourceName.Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold);
        _sourceName.ForeColor = UiTheme.Text;
        _sourceOrganization.AutoSize = true;
        _sourceOrganization.MaximumSize = new Size(360, 0);
        _sourceOrganization.ForeColor = UiTheme.MutedText;
        ConfigureTextLabel(_sourceDetails);
        ConfigureTextLabel(_sourceLicense);
        ConfigureTextLabel(_sourceCaution, warning: true);
        var statusRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        statusRow.Controls.AddRange([_authority, _providerStatus]);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        actions.Controls.AddRange([_probeButton, _portalButton]);
        layout.Controls.Add(_sourceName, 0, 0);
        layout.Controls.Add(_sourceOrganization, 0, 1);
        layout.Controls.Add(statusRow, 0, 2);
        layout.Controls.Add(Section(_thai ? "รายละเอียด" : "Details", _sourceDetails), 0, 3);
        layout.Controls.Add(Section(_thai ? "License / Attribution" : "License / attribution", _sourceLicense), 0, 4);
        layout.Controls.Add(Section(_thai ? "ข้อควรระวัง" : "Caution", _sourceCaution, true), 0, 5);
        layout.Controls.Add(actions, 0, 6);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildRightColumn()
    {
        var stack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 260));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        stack.Controls.Add(BuildSearchCard(), 0, 0);
        stack.Controls.Add(BuildResultsCard(), 0, 1);
        return stack;
    }

    private Control BuildSearchCard()
    {
        var card = new ModernCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.Controls.Add(UiTheme.Heading(_thai ? "2. กำหนดสิ่งที่ต้องการ" : "2. Define the search", 16F), 0, 0);
        var hint = UiTheme.Caption(_thai
            ? "ค้น Collection ก่อน แล้วเลือก Collection เพื่อค้นภาพ/Granule จริงตามพื้นที่และวันที่"
            : "Find a collection first, then use it to search real items/granules by area and date.", 820);
        hint.Margin = new Padding(0, 3, 0, 10);
        layout.Controls.Add(hint, 0, 1);

        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        AddField(fields, 0, _thai ? "คำค้น" : "Keyword", _query, _thai ? "Collection ID" : "Collection ID", _collectionId);
        AddField(fields, 1, "BBOX", _bbox, _thai ? "จำนวนผลลัพธ์" : "Result limit", _limit);
        var startPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        startPanel.Controls.Add(_startDate);
        var endPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        endPanel.Controls.Add(_endDate);
        AddField(fields, 2, _thai ? "วันที่เริ่ม" : "Start date", startPanel, _thai ? "วันที่สิ้นสุด" : "End date", endPanel);
        layout.Controls.Add(fields, 0, 2);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
        _useCloud.Text = _thai ? "กรองเมฆไม่เกิน" : "Maximum cloud";
        _useCloud.Margin = new Padding(0, 10, 4, 0);
        _cloud.Width = 65;
        var percent = new Label { Text = "%", AutoSize = true, Margin = new Padding(3, 11, 10, 0) };
        actions.Controls.AddRange([_useCloud, _cloud, percent, _thailandButton, _clearCollectionButton, _searchButton]);
        layout.Controls.Add(actions, 0, 3);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildResultsCard()
    {
        var card = new ModernCard { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(UiTheme.Heading(_thai ? "3. เลือกผลลัพธ์และนำไปใช้" : "3. Select and use a result", 16F), 0, 0);
        layout.Controls.Add(_results, 0, 1);
        var detailPanel = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.SurfaceMuted, Padding = new Padding(12), Margin = new Padding(0, 8, 0, 8) };
        detailPanel.Controls.Add(_resultDetails);
        layout.Controls.Add(detailPanel, 0, 2);
        var primaryActions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        primaryActions.Controls.AddRange([_useCollectionButton, _downloadButton, _openLandingButton, _copyCitationButton]);
        layout.Controls.Add(primaryActions, 0, 3);
        var downloadedActions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        downloadedActions.Controls.AddRange([_openQgisButton, _openFolderButton]);
        _progress.Width = 260;
        _progress.Height = 8;
        _progress.Margin = new Padding(12, 12, 0, 0);
        downloadedActions.Controls.Add(_progress);
        layout.Controls.Add(downloadedActions, 0, 4);
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
                ? "ระบบไม่ถือว่าข้อมูลทุกชุดเหมาะกับงานโดยอัตโนมัติ ต้องตรวจปี CRS ความละเอียด Vertical datum และ License"
                : "Always verify date, CRS, resolution, vertical datum and license before using a dataset.",
            AutoSize = true,
            ForeColor = UiTheme.MutedText,
            Anchor = AnchorStyles.Left
        }, 0, 0);
        layout.Controls.Add(_status, 1, 0);
        footer.Controls.Add(layout);
        return footer;
    }

    private void ApplyText()
    {
        _sourceFilter.PlaceholderText = _thai ? "กรองแหล่งข้อมูล เช่น DEM ขอบเขต ป่าชายเลน" : "Filter providers: DEM, boundaries, mangrove";
        _query.PlaceholderText = _thai ? "เช่น SRTM, sentinel-2, ขอบเขตตำบล" : "e.g. SRTM, sentinel-2, administrative boundary";
        _collectionId.PlaceholderText = _thai ? "เลือกจากผลลัพธ์ Collection" : "Select from a collection result";
        _bbox.PlaceholderText = "minLon,minLat,maxLon,maxLat";
        _probeButton.Text = _thai ? "ทดสอบการเชื่อมต่อ" : "Test connection";
        _portalButton.Text = _thai ? "เปิดเว็บไซต์ต้นทาง" : "Open provider";
        _thailandButton.Text = _thai ? "ใช้กรอบประเทศไทย" : "Thailand extent";
        _searchButton.Text = _thai ? "ค้นหาออนไลน์" : "Search live";
        _useCollectionButton.Text = _thai ? "ใช้ Collection ที่เลือก" : "Use selected collection";
        _clearCollectionButton.Text = _thai ? "ล้าง Collection" : "Clear collection";
        _downloadButton.Text = _thai ? "ดาวน์โหลดไฟล์" : "Download file";
        _openLandingButton.Text = _thai ? "เปิดรายละเอียด" : "Open details";
        _copyCitationButton.Text = _thai ? "คัดลอก Citation" : "Copy citation";
        _openQgisButton.Text = _thai ? "เปิดไฟล์ใน QGIS" : "Open in QGIS";
        _openFolderButton.Text = _thai ? "เปิดโฟลเดอร์" : "Open folder";
        _status.SetNeutral(_localizer.Text("ready"));
        _providerStatus.SetNeutral(_thai ? "ยังไม่ได้ทดสอบ" : "Not tested");
    }

    private void ConfigureGrid()
    {
        _results.Dock = DockStyle.Fill;
        _results.BackgroundColor = Color.White;
        _results.BorderStyle = BorderStyle.None;
        _results.ReadOnly = true;
        _results.AllowUserToAddRows = false;
        _results.AllowUserToDeleteRows = false;
        _results.MultiSelect = false;
        _results.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _results.AutoGenerateColumns = false;
        _results.RowHeadersVisible = false;
        _results.EnableHeadersVisualStyles = false;
        _results.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.SurfaceMuted;
        _results.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.Text;
        _results.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _results.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
        _results.DefaultCellStyle.SelectionForeColor = UiTheme.Text;
        _results.RowTemplate.Height = 33;
        _results.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = _thai ? "ชื่อรายการ" : "Item", DataPropertyName = nameof(ExternalDataResult.Title), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 55 });
        _results.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = _thai ? "รูปแบบ" : "Format", DataPropertyName = nameof(ExternalDataResult.Format), Width = 120 });
        _results.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = _thai ? "วันที่" : "Date", Name = "date", Width = 105 });
        _results.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = _thai ? "เมฆ" : "Cloud", Name = "cloud", Width = 75 });
        _results.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = _thai ? "การเข้าถึง" : "Access", Name = "access", Width = 105 });
    }

    private void LoadCategories()
    {
        var categories = new List<string> { _thai ? "ทั้งหมด" : "All" };
        categories.AddRange(_registry.Categories());
        _category.DataSource = categories;
    }

    private void RefreshSources()
    {
        var category = _category.SelectedItem?.ToString();
        if (category == "All") category = "ทั้งหมด";
        var matches = _registry.Filter(_sourceFilter.Text, category).Select(x => new SourceChoice(x)).ToList();
        var selectedId = SelectedSource?.Id;
        _sources.DataSource = matches;
        if (selectedId is not null)
        {
            var index = matches.FindIndex(x => x.Source.Id == selectedId);
            if (index >= 0) _sources.SelectedIndex = index;
        }
        if (_sources.SelectedIndex < 0 && matches.Count > 0) _sources.SelectedIndex = 0;
        _sourceCount.Text = _thai ? $"พบ {matches.Count} แหล่งข้อมูล" : $"{matches.Count} providers";
    }

    private ExternalDataSource? SelectedSource => (_sources.SelectedItem as SourceChoice)?.Source;
    private ExternalDataResult? SelectedResult => _results.CurrentRow?.DataBoundItem as ExternalDataResult;

    private void ShowSelectedSource()
    {
        var source = SelectedSource;
        if (source is null) return;
        _sourceName.Text = source.Name;
        _sourceOrganization.Text = source.Organization;
        if (source.Authority == DataAuthorityLevel.Official) _authority.SetSuccess(_thai ? "ทางการ" : "Official");
        else if (source.Authority == DataAuthorityLevel.OpenResearch) _authority.SetBusy(_thai ? "ข้อมูลวิจัยเปิด" : "Open research");
        else _authority.SetWarning(_thai ? "ข้อมูลชุมชน" : "Community");
        _providerStatus.SetNeutral(_thai ? "ยังไม่ได้ทดสอบ" : "Not tested");
        _sourceDetails.Text = $"{string.Join(" • ", source.Categories)}\nพื้นที่: {source.Coverage}\nความละเอียด: {source.Resolution}\nรูปแบบ: {source.DataTypes}\nการเข้าถึง: {source.AccessKind}";
        _sourceLicense.Text = $"{source.LicenseNote}\n\nAttribution: {source.Attribution}";
        _sourceCaution.Text = source.Caution;
        _collectionId.Clear();
        _query.Clear();
        _currentResults = [];
        _results.DataSource = null;
        _resultDetails.Clear();
        UpdateResultActions();
        UpdateSearchMode();
    }

    private async Task ProbeSelectedSourceAsync()
    {
        var source = SelectedSource;
        if (source is null) return;
        SetBusy(true, _thai ? "กำลังทดสอบปลายทาง" : "Testing endpoint");
        try
        {
            var result = await _service.ProbeAsync(source);
            if (result.Success) _providerStatus.SetSuccess($"OK {result.ElapsedMilliseconds} ms");
            else _providerStatus.SetError(result.StatusCode is null ? "Offline" : $"HTTP {(int)result.StatusCode}");
            _status.SetNeutral(result.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SearchAsync()
    {
        var source = SelectedSource;
        if (source is null) return;
        if (source.AccessKind is DataAccessKind.Ckan or DataAccessKind.Cmr && string.IsNullOrWhiteSpace(_query.Text) && string.IsNullOrWhiteSpace(_collectionId.Text))
        {
            _status.SetWarning(_thai ? "กรุณาพิมพ์คำค้น" : "Enter a keyword");
            return;
        }
        if (!ExternalCatalogSearchService.TryParseBoundingBox(_bbox.Text, out var bbox))
        {
            _status.SetError(_thai ? "BBOX ไม่ถูกต้อง" : "Invalid BBOX");
            MessageBox.Show(this, "BBOX: minLon,minLat,maxLon,maxLat\nExample: 97.3,5.6,105.7,20.5", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_startDate.Checked && _endDate.Checked && _startDate.Value.Date > _endDate.Value.Date)
        {
            _status.SetError(_thai ? "วันที่เริ่มต้องไม่เกินวันที่สิ้นสุด" : "Start date must be before end date");
            return;
        }

        var options = new ExternalSearchOptions
        {
            Query = _query.Text.Trim(),
            CollectionId = string.IsNullOrWhiteSpace(_collectionId.Text) ? null : _collectionId.Text.Trim(),
            BoundingBox = bbox,
            StartDate = _startDate.Checked ? new DateTimeOffset(_startDate.Value.Date, TimeZoneInfo.Local.GetUtcOffset(_startDate.Value.Date)) : null,
            EndDate = _endDate.Checked ? new DateTimeOffset(_endDate.Value.Date.AddDays(1).AddTicks(-1), TimeZoneInfo.Local.GetUtcOffset(_endDate.Value.Date)) : null,
            MaximumCloudCover = _useCloud.Checked ? decimal.ToDouble(_cloud.Value) : null,
            Limit = decimal.ToInt32(_limit.Value)
        };

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        SetBusy(true, _thai ? "กำลังค้น Catalog จริง" : "Searching live catalog");
        try
        {
            _currentResults = await _service.SearchAsync(source, options, _cts.Token);
            _results.DataSource = _currentResults.ToList();
            PopulateComputedColumns();
            if (_currentResults.Count == 0) _status.SetWarning(_thai ? "ไม่พบข้อมูล ลองลดเงื่อนไขหรือเปิดเว็บไซต์ต้นทาง" : "No results. Relax filters or open the provider portal.");
            else _status.SetSuccess(_thai ? $"พบ {_currentResults.Count} รายการ" : $"{_currentResults.Count} results");
        }
        catch (OperationCanceledException)
        {
            _status.SetWarning(_localizer.Text("status.cancelled"));
        }
        catch (Exception ex)
        {
            _status.SetError(_thai ? "ค้นหาไม่สำเร็จ" : "Search failed");
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void PopulateComputedColumns()
    {
        foreach (DataGridViewRow row in _results.Rows)
        {
            if (row.DataBoundItem is not ExternalDataResult item) continue;
            row.Cells["date"].Value = item.AcquiredAt?.ToLocalTime().ToString("yyyy-MM-dd") ?? "—";
            row.Cells["cloud"].Value = item.CloudCover is null ? "—" : $"{item.CloudCover:0.#}%";
            row.Cells["access"].Value = item.DownloadUri is not null
                ? (_thai ? "ดาวน์โหลด" : "Direct")
                : item.IsCollection ? (_thai ? "Collection" : "Collection")
                : item.RequiresAccount ? (_thai ? "Login/Portal" : "Login/portal") : (_thai ? "รายละเอียด" : "Details");
        }
    }

    private async Task UseSelectedCollectionAsync()
    {
        var result = SelectedResult;
        if (result is null || !result.IsCollection) return;
        _collectionId.Text = result.CollectionId ?? result.DatasetId ?? string.Empty;
        UpdateSearchMode();
        _status.SetNeutral(_thai ? "เลือก Collection แล้ว กำหนด BBOX/วันที่ แล้วกดค้นหาอีกครั้ง" : "Collection selected. Set BBOX/date and search again.");
        await Task.CompletedTask;
    }

    private void ShowSelectedResult()
    {
        var item = SelectedResult;
        if (item is null)
        {
            _resultDetails.Clear();
            UpdateResultActions();
            return;
        }
        _resultDetails.Text = $"{item.Title}\n\n{item.Description}\n\nProvider: {item.ProviderId}\nCollection: {item.CollectionId ?? "—"}\nDataset ID: {item.DatasetId ?? "—"}\nFormat: {item.Format}\nDate: {item.AcquiredAt?.ToString("u") ?? "—"}\nCloud: {(item.CloudCover is null ? "—" : item.CloudCover.Value.ToString("0.##", CultureInfo.InvariantCulture) + "%")}\nBBOX: {item.SpatialSummary ?? "—"}\nSize: {(item.SizeBytes is null ? "unknown" : FormatBytes(item.SizeBytes.Value))}\nLicense: {item.LicenseNote}\nAttribution: {item.Attribution}";
        UpdateResultActions();
    }

    private async Task DownloadSelectedAsync()
    {
        var item = SelectedResult;
        if (item?.DownloadUri is null) return;
        var fileName = ExternalCatalogSearchService.SuggestFileName(item);
        using var dialog = new SaveFileDialog
        {
            Title = _thai ? "เลือกที่เก็บข้อมูล" : "Save dataset",
            FileName = fileName,
            InitialDirectory = AppPaths.DefaultOutputRoot,
            Filter = "All files|*.*",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var confirm = MessageBox.Show(this,
            (_thai
                ? $"ชุดข้อมูล: {item.Title}\nขนาด: {(item.SizeBytes is null ? "ไม่ทราบ" : FormatBytes(item.SizeBytes.Value))}\nLicense: {item.LicenseNote}\n\nดาวน์โหลดไปที่:\n{dialog.FileName}"
                : $"Dataset: {item.Title}\nSize: {(item.SizeBytes is null ? "unknown" : FormatBytes(item.SizeBytes.Value))}\nLicense: {item.LicenseNote}\n\nSave to:\n{dialog.FileName}"),
            _thai ? "ยืนยันการดาวน์โหลด" : "Confirm download",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information);
        if (confirm != DialogResult.OK) return;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _progress.Visible = true;
        _progress.Style = ProgressBarStyle.Marquee;
        SetBusy(true, _thai ? "กำลังดาวน์โหลดและคำนวณ SHA-256" : "Downloading and calculating SHA-256");
        try
        {
            var progress = new Progress<(long Received, long? Total)>(value =>
            {
                if (value.Total is > 0)
                {
                    _progress.Style = ProgressBarStyle.Continuous;
                    _progress.Maximum = 1000;
                    _progress.Value = Math.Clamp((int)(value.Received * 1000L / value.Total.Value), 0, 1000);
                    _status.SetBusy($"{FormatBytes(value.Received)} / {FormatBytes(value.Total.Value)}");
                }
                else _status.SetBusy(FormatBytes(value.Received));
            });
            var receipt = await _service.DownloadWithReceiptAsync(item, dialog.FileName, progress, _cts.Token);
            _lastDownloadedPath = receipt.FilePath;
            _openFolderButton.Enabled = true;
            _openQgisButton.Enabled = IsQgisReadable(receipt.FilePath);
            _status.SetSuccess(_thai ? $"เสร็จแล้ว • SHA-256 {receipt.Sha256[..12]}…" : $"Complete • SHA-256 {receipt.Sha256[..12]}…");
            MessageBox.Show(this,
                (_thai
                    ? $"ดาวน์โหลดสำเร็จ\n\nไฟล์: {receipt.FilePath}\nขนาด: {FormatBytes(receipt.SizeBytes)}\nSHA-256: {receipt.Sha256}\nMetadata: {receipt.MetadataPath}"
                    : $"Download complete\n\nFile: {receipt.FilePath}\nSize: {FormatBytes(receipt.SizeBytes)}\nSHA-256: {receipt.Sha256}\nMetadata: {receipt.MetadataPath}"),
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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

    private async Task OpenLastDownloadInQgisAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastDownloadedPath) || !File.Exists(_lastDownloadedPath)) return;
        _runtime ??= await new RuntimeDetector().LoadCachedAsync() ?? await new RuntimeDetector().DetectAsync();
        if (!_runtime.HasQgisGui)
        {
            MessageBox.Show(this, _thai ? "ไม่พบ QGIS Desktop กรุณาติดตั้ง QGIS หรือกดตรวจโปรแกรม GIS จากหน้าหลัก" : "QGIS Desktop was not found.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var info = new ProcessStartInfo(_runtime.QgisGuiPath!) { UseShellExecute = false };
        info.ArgumentList.Add(_lastDownloadedPath);
        Process.Start(info);
        _status.SetSuccess(_thai ? "ส่งไฟล์ไปเปิดใน QGIS แล้ว" : "Opened in QGIS");
    }

    private void OpenLastDownloadFolder()
    {
        if (string.IsNullOrWhiteSpace(_lastDownloadedPath) || !File.Exists(_lastDownloadedPath)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_lastDownloadedPath}\"") { UseShellExecute = true });
    }

    private void CopyCitation()
    {
        var item = SelectedResult;
        if (item is null) return;
        Clipboard.SetText($"{item.Title}\n{item.Attribution}\nLicense: {item.LicenseNote}\n{item.LandingUri}");
        _status.SetSuccess(_thai ? "คัดลอก Citation แล้ว" : "Citation copied");
    }

    private void UpdateResultActions()
    {
        var item = SelectedResult;
        _useCollectionButton.Enabled = item?.IsCollection == true;
        _downloadButton.Enabled = item?.DownloadUri is not null;
        _openLandingButton.Enabled = item is not null;
        _copyCitationButton.Enabled = item is not null;
    }

    private void UpdateSearchMode()
    {
        var hasCollection = !string.IsNullOrWhiteSpace(_collectionId.Text);
        _searchButton.Text = hasCollection
            ? (_thai ? "ค้นรายการใน Collection" : "Search collection items")
            : (_thai ? "ค้นหา Collection/ชุดข้อมูล" : "Search collections/datasets");
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _searchButton.Enabled = !busy;
        _probeButton.Enabled = !busy;
        _downloadButton.Enabled = !busy && SelectedResult?.DownloadUri is not null;
        _useCollectionButton.Enabled = !busy && SelectedResult?.IsCollection == true;
        _sources.Enabled = !busy;
        if (message is not null) _status.SetBusy(message);
    }

    private static void AddField(TableLayoutPanel table, int row, string label1, Control value1, string label2, Control value2)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label1, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 9, 5, 0), ForeColor = UiTheme.MutedText }, 0, row);
        value1.Dock = DockStyle.Top;
        table.Controls.Add(value1, 1, row);
        table.Controls.Add(new Label { Text = label2, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 9, 5, 0), ForeColor = UiTheme.MutedText }, 2, row);
        value2.Dock = DockStyle.Top;
        table.Controls.Add(value2, 3, row);
    }

    private static Control Section(string title, Label content, bool warning = false)
    {
        var panel = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10), Margin = new Padding(0, 5, 0, 5), BackColor = warning ? Color.FromArgb(255, 251, 235) : UiTheme.SurfaceMuted };
        var stack = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        stack.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold), ForeColor = warning ? UiTheme.Warning : UiTheme.Primary, Margin = new Padding(0, 0, 0, 4) });
        stack.Controls.Add(content);
        panel.Controls.Add(stack);
        return panel;
    }

    private static void ConfigureTextLabel(Label label, bool warning = false)
    {
        label.AutoSize = true;
        label.MaximumSize = new Size(350, 0);
        label.ForeColor = warning ? Color.FromArgb(146, 64, 14) : UiTheme.Text;
        label.Font = new Font("Segoe UI", 9.25F);
    }

    private static void OpenUri(Uri? uri)
    {
        if (uri is null) return;
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private static bool IsQgisReadable(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".gpkg" or ".shp" or ".geojson" or ".json" or ".kml" or ".kmz" or ".tif" or ".tiff" or ".vrt" or ".hgt" or ".nc" or ".gml" or ".csv";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return $"{value:0.##} {units[index]}";
    }
}