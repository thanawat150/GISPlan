using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GISPlan.Core;

public enum DataAuthorityLevel
{
    Official,
    OpenResearch,
    Community
}

public enum DataAccessKind
{
    Ckan,
    Stac,
    Cmr,
    Portal,
    DirectDownload
}

public sealed record ExternalDataSource(
    string Id,
    string Name,
    string Organization,
    DataAuthorityLevel Authority,
    string[] Categories,
    string Coverage,
    string Resolution,
    string DataTypes,
    DataAccessKind AccessKind,
    Uri PortalUri,
    Uri? ApiUri,
    bool RequiresAccount,
    string LicenseNote,
    string Attribution,
    string Caution,
    string[] Keywords);

public sealed record ExternalDataResult(
    string ProviderId,
    string Title,
    string Description,
    string Format,
    Uri LandingUri,
    Uri? DownloadUri,
    long? SizeBytes,
    string LicenseNote,
    string Attribution,
    bool RequiresAccount,
    string? DatasetId = null,
    string? CollectionId = null,
    DateTimeOffset? AcquiredAt = null,
    double? CloudCover = null,
    string? SpatialSummary = null,
    string? ContentType = null,
    bool IsCollection = false);

public sealed class ExternalSearchOptions
{
    public string Query { get; set; } = string.Empty;
    public string? CollectionId { get; set; }
    public double[]? BoundingBox { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public double? MaximumCloudCover { get; set; }
    public int Limit { get; set; } = 50;
}

public sealed record ExternalSourceProbeResult(
    bool Success,
    HttpStatusCode? StatusCode,
    long ElapsedMilliseconds,
    Uri Endpoint,
    string Message);

public sealed record ExternalDownloadReceipt(
    string FilePath,
    string MetadataPath,
    long SizeBytes,
    string Sha256,
    string? ContentType,
    Uri FinalUri);

public sealed class ExternalDataSourceRegistry
{
    public IReadOnlyList<ExternalDataSource> Sources { get; } =
    [
        new(
            "thai-government-data",
            "Open Government Data of Thailand",
            "สำนักงานพัฒนารัฐบาลดิจิทัลและหน่วยงานเจ้าของข้อมูล",
            DataAuthorityLevel.Official,
            ["ขอบเขตการปกครอง", "พิกัดสถานที่", "สถิติ", "ทรัพยากรธรรมชาติ"],
            "ประเทศไทย",
            "แตกต่างตามชุดข้อมูล",
            "CSV, XLSX, SHP, GeoJSON, API และรูปแบบอื่นตามหน่วยงาน",
            DataAccessKind.Ckan,
            new Uri("https://www.data.go.th/"),
            new Uri("https://www.data.go.th/api/3/action/package_search"),
            false,
            "ตรวจ License ของแต่ละ Resource ก่อนใช้",
            "ระบุชื่อหน่วยงานเจ้าของชุดข้อมูลและ data.go.th",
            "ข้อมูลที่เผยแพร่โดยหน่วยงานรัฐอาจมีปี มาตราส่วน หรือความครบถ้วนต่างกัน ต้องอ่าน Metadata รายชุด",
            ["จังหวัด", "อำเภอ", "ตำบล", "หมู่บ้าน", "ขอบเขต", "dopa", "กรมการปกครอง", "ป่า", "ที่ดิน"]),
        new(
            "gistda-open-data",
            "GISTDA Open Data",
            "สำนักงานพัฒนาเทคโนโลยีอวกาศและภูมิสารสนเทศ (องค์การมหาชน)",
            DataAuthorityLevel.Official,
            ["ภาพถ่ายดาวเทียม", "แผนที่ฐาน", "ภัยพิบัติ", "ทะเลและชายฝั่ง", "พืชเศรษฐกิจ"],
            "ประเทศไทย",
            "แตกต่างตามชุดข้อมูล",
            "Raster, Vector, Web Service และเอกสารประกอบ",
            DataAccessKind.Ckan,
            new Uri("https://opendata.gistda.or.th/"),
            new Uri("https://opendata.gistda.or.th/api/3/action/package_search"),
            false,
            "ตรวจ License และเงื่อนไขรายชุดข้อมูล",
            "GISTDA Open Data และหน่วยงานเจ้าของข้อมูล",
            "บาง Resource เป็น WMS/WFS/API หรือหน้าแสดงผล ไม่ใช่ไฟล์ที่ดาวน์โหลดเพื่อวิเคราะห์โดยตรง",
            ["gistda", "ดาวเทียม", "น้ำท่วม", "ภัยแล้ง", "ไฟไหม้", "basemap", "coast", "ทะเล"]),
        new(
            "dmcr-change",
            "DMCR Coastal and Mangrove Data",
            "กรมทรัพยากรทางทะเลและชายฝั่ง",
            DataAuthorityLevel.Official,
            ["ป่าชายเลน", "ชายฝั่ง", "พื้นที่คงสภาพ", "การเปลี่ยนแปลง"],
            "พื้นที่ชายฝั่งประเทศไทย",
            "ขึ้นกับชั้นข้อมูลและปีสำรวจ",
            "Web map และข้อมูลตามที่ระบบอนุญาต",
            DataAccessKind.Portal,
            new Uri("https://change.dmcr.go.th/"),
            null,
            false,
            "ใช้ตามข้อกำหนดของกรมทรัพยากรทางทะเลและชายฝั่ง",
            "กรมทรัพยากรทางทะเลและชายฝั่ง (DMCR)",
            "ตรวจมาตราส่วน ปีข้อมูล วิธีสำรวจ และข้อจำกัดที่แสดงในรายละเอียดชั้นข้อมูล",
            ["dmcr", "ป่าชายเลน", "mangrove", "ชายฝั่ง", "กัดเซาะ", "ทะเล"]),
        new(
            "copernicus-stac",
            "Copernicus Data Space STAC",
            "European Union / ESA / Copernicus",
            DataAuthorityLevel.Official,
            ["Sentinel", "Copernicus DEM", "Land Monitoring", "ไฟป่า", "ภาพดาวเทียม"],
            "ทั่วโลก",
            "ระดับเมตรถึงกิโลเมตรตามผลิตภัณฑ์",
            "STAC, COG, SAFE, GeoTIFF, NetCDF และผลิตภัณฑ์อื่น",
            DataAccessKind.Stac,
            new Uri("https://browser.stac.dataspace.copernicus.eu/"),
            new Uri("https://stac.dataspace.copernicus.eu/v1/"),
            true,
            "Copernicus data policy หรือ License ที่ระบุในแต่ละ Collection",
            "European Union, ESA และผู้ผลิตที่ระบุใน Collection",
            "ค้น Metadata ได้โดยไม่ Login แต่ Asset จำนวนมากต้อง OAuth หรือ S3 credentials ก่อนดาวน์โหลด",
            ["sentinel", "copernicus", "dem", "elevation", "sar", "optical", "burnt area", "land cover"]),
        new(
            "nasa-earthdata",
            "NASA Earthdata CMR",
            "NASA Earth Science Data and Information System",
            DataAuthorityLevel.Official,
            ["DEM", "SRTM", "NASADEM", "ภูมิอากาศ", "น้ำ", "ความสูง"],
            "ทั่วโลกตามผลิตภัณฑ์",
            "แตกต่างตามผลิตภัณฑ์ เช่น SRTM/NASADEM ประมาณ 30–90 เมตร",
            "GeoTIFF, HGT, HDF, NetCDF และรูปแบบผลิตภัณฑ์",
            DataAccessKind.Cmr,
            new Uri("https://search.earthdata.nasa.gov/"),
            new Uri("https://cmr.earthdata.nasa.gov/search/"),
            true,
            "NASA Earthdata data-use policy ของแต่ละผลิตภัณฑ์",
            "NASA Earthdata และศูนย์ข้อมูลผู้ผลิต",
            "การค้นหาเปิดได้ แต่การดาวน์โหลดหลายผลิตภัณฑ์ต้อง Earthdata Login และต้องตรวจ Vertical Datum",
            ["nasa", "srtm", "nasadem", "dem", "elevation", "height", "earthdata"]),
        new(
            "jaxa-aw3d30",
            "ALOS World 3D 30 m (AW3D30)",
            "JAXA EORC",
            DataAuthorityLevel.Official,
            ["DSM", "ความสูง", "ภูมิประเทศ"],
            "ทั่วโลก",
            "ประมาณ 30 เมตร",
            "DSM tiles และ Quality/Mask files",
            DataAccessKind.Portal,
            new Uri("https://www.eorc.jaxa.jp/ALOS/en/dataset/aw3d30/aw3d30_e.htm"),
            null,
            true,
            "Terms of Use ของ JAXA",
            "JAXA EORC / ALOS AW3D30",
            "เป็น DSM ไม่ใช่ DTM และรวมผลจากสิ่งปกคลุมผิวโลกบางส่วน",
            ["jaxa", "alos", "aw3d30", "dem", "dsm", "elevation"]),
        new(
            "esa-worldcover",
            "ESA WorldCover 2021",
            "European Space Agency / WorldCover consortium",
            DataAuthorityLevel.Official,
            ["Land cover", "Mangrove", "Wetland", "Tree cover"],
            "ทั่วโลก",
            "10 เมตร",
            "Cloud Optimized GeoTIFF",
            DataAccessKind.Portal,
            new Uri("https://worldcover2021.esa.int/download"),
            null,
            false,
            "CC BY 4.0",
            "© ESA WorldCover project / Contains modified Copernicus Sentinel data (2021)",
            "ปี 2020 และ 2021 ใช้อัลกอริทึมคนละรุ่น ไม่ควรตีความผลต่างทั้งหมดเป็นการเปลี่ยนแปลงจริง",
            ["esa", "worldcover", "landcover", "mangrove", "wetland", "forest", "10m"]),
        new(
            "jrc-global-surface-water",
            "JRC Global Surface Water",
            "European Commission Joint Research Centre",
            DataAuthorityLevel.Official,
            ["แหล่งน้ำ", "การเปลี่ยนแปลงน้ำ", "Seasonality", "Occurrence"],
            "ทั่วโลก",
            "30 เมตร โดยอิง Landsat",
            "GeoTIFF และ Google Earth Engine assets",
            DataAccessKind.Portal,
            new Uri("https://global-surface-water.appspot.com/download"),
            null,
            false,
            "Copernicus Programme data policy",
            "Source: EC JRC/Google",
            "เลือก Layer ให้ตรงคำถาม เช่น occurrence, seasonality, transition หรือ yearly history",
            ["jrc", "surface water", "water", "น้ำ", "wetland", "flood"]),
        new(
            "hydrosheds",
            "HydroSHEDS",
            "HydroSHEDS / WWF / McGill University partners",
            DataAuthorityLevel.OpenResearch,
            ["ลุ่มน้ำ", "แม่น้ำ", "DEM", "Flow direction", "Flow accumulation"],
            "ทั่วโลกตามผลิตภัณฑ์",
            "ประมาณ 30–90 เมตรและระดับหยาบกว่า",
            "GeoTIFF, Shapefile และผลิตภัณฑ์ Hydrology",
            DataAccessKind.Portal,
            new Uri("https://www.hydrosheds.org/products"),
            null,
            false,
            "License แตกต่างตามผลิตภัณฑ์",
            "HydroSHEDS และผู้ผลิตที่ระบุในผลิตภัณฑ์",
            "DEM ถูกปรับเพื่อการไหลของน้ำ จึงไม่ควรใช้แทน DEM ดิบสำหรับทุกวัตถุประสงค์",
            ["hydrosheds", "basin", "river", "catchment", "flow", "dem", "watershed", "ลุ่มน้ำ"])
    ];

    public IEnumerable<ExternalDataSource> Filter(string? query, string? category = null)
    {
        var terms = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Sources.Where(source =>
            (string.IsNullOrWhiteSpace(category) || category.Equals("ทั้งหมด", StringComparison.OrdinalIgnoreCase) ||
             source.Categories.Any(x => x.Contains(category, StringComparison.OrdinalIgnoreCase))) &&
            (terms.Length == 0 || terms.All(term => SearchText(source).Contains(term, StringComparison.OrdinalIgnoreCase))));
    }

    public IReadOnlyList<string> Categories() => Sources
        .SelectMany(x => x.Categories)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(x => x)
        .ToList();

    private static string SearchText(ExternalDataSource source) => string.Join(' ',
        source.Name, source.Organization, source.Coverage, source.Resolution, source.DataTypes,
        string.Join(' ', source.Categories), string.Join(' ', source.Keywords));
}

public sealed class ExternalCatalogSearchService
{
    private static readonly HashSet<string> KnownFileFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "CSV", "XLS", "XLSX", "ZIP", "SHP", "GPKG", "GEOJSON", "JSON", "KML", "KMZ",
        "TIFF", "TIF", "GEOTIFF", "COG", "HGT", "HDF", "HDF5", "NC", "NETCDF", "GDB", "7Z"
    };

    private readonly HttpClient _client;

    public ExternalCatalogSearchService(HttpClient? client = null)
    {
        _client = client ?? new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 8
        })
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        if (!_client.DefaultRequestHeaders.UserAgent.Any())
            _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GISPlan", "0.3"));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public Task<IReadOnlyList<ExternalDataResult>> SearchAsync(
        ExternalDataSource source,
        string query,
        CancellationToken cancellationToken = default) =>
        SearchAsync(source, new ExternalSearchOptions { Query = query }, cancellationToken);

    public async Task<IReadOnlyList<ExternalDataResult>> SearchAsync(
        ExternalDataSource source,
        ExternalSearchOptions options,
        CancellationToken cancellationToken = default)
    {
        options.Limit = Math.Clamp(options.Limit, 1, 200);
        return source.AccessKind switch
        {
            DataAccessKind.Ckan when source.ApiUri is not null => await SearchCkanAsync(source, options, cancellationToken),
            DataAccessKind.Stac when source.ApiUri is not null => await SearchStacAsync(source, options, cancellationToken),
            DataAccessKind.Cmr when source.ApiUri is not null => await SearchCmrAsync(source, options, cancellationToken),
            _ => SourceMatches(source, options.Query) || string.IsNullOrWhiteSpace(options.Query)
                ? [ToStaticResult(source)]
                : []
        };
    }

    public async Task<ExternalSourceProbeResult> ProbeAsync(
        ExternalDataSource source,
        CancellationToken cancellationToken = default)
    {
        var endpoint = source.ApiUri ?? source.PortalUri;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            stopwatch.Stop();
            return new ExternalSourceProbeResult(
                response.IsSuccessStatusCode,
                response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                endpoint,
                response.IsSuccessStatusCode
                    ? $"เชื่อมต่อสำเร็จ ({(int)response.StatusCode})"
                    : $"ปลายทางตอบกลับ {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            return new ExternalSourceProbeResult(false, null, stopwatch.ElapsedMilliseconds, endpoint, ex.Message);
        }
    }

    private async Task<IReadOnlyList<ExternalDataResult>> SearchCkanAsync(
        ExternalDataSource source,
        ExternalSearchOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Query)) return [];
        var separator = source.ApiUri!.Query.Length == 0 ? "?" : "&";
        var uri = new Uri(source.ApiUri + separator + $"q={Uri.EscapeDataString(options.Query)}&rows={options.Limit}");
        using var response = await _client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!json.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean()) return [];
        if (!json.RootElement.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("results", out var datasets)) return [];

        var output = new List<ExternalDataResult>();
        foreach (var dataset in datasets.EnumerateArray())
        {
            var datasetId = String(dataset, "id") ?? String(dataset, "name");
            var title = String(dataset, "title") ?? datasetId ?? "Untitled dataset";
            var description = Clean(String(dataset, "notes"));
            var landing = DatasetLanding(source.PortalUri, datasetId);
            var license = FirstNonEmpty(String(dataset, "license_title"), String(dataset, "license_id"), source.LicenseNote);
            var added = false;

            if (dataset.TryGetProperty("resources", out var resources) && resources.ValueKind == JsonValueKind.Array)
            {
                foreach (var resource in resources.EnumerateArray())
                {
                    var urlText = FirstNonEmpty(String(resource, "url"), String(resource, "download_url"));
                    if (!TryHttpsUri(urlText, out var resourceUri)) continue;
                    var format = NormalizeFormat(FirstNonEmpty(String(resource, "format"), Path.GetExtension(resourceUri.AbsolutePath).TrimStart('.')));
                    var resourceTitle = FirstNonEmpty(String(resource, "name"), String(resource, "description"));
                    var direct = IsDirectFileResource(resourceUri, format, resource);
                    output.Add(new ExternalDataResult(
                        source.Id,
                        string.IsNullOrWhiteSpace(resourceTitle) ? title : $"{title} — {resourceTitle}",
                        description,
                        string.IsNullOrWhiteSpace(format) ? "RESOURCE" : format,
                        landing,
                        direct ? resourceUri : null,
                        Long(resource, "size"),
                        license,
                        source.Attribution,
                        source.RequiresAccount,
                        datasetId,
                        ContentType: String(resource, "mimetype")));
                    added = true;
                    if (output.Count >= options.Limit * 4) break;
                }
            }

            if (!added)
            {
                output.Add(new ExternalDataResult(
                    source.Id, title, description, "CATALOG", landing, null, null,
                    license, source.Attribution, source.RequiresAccount, datasetId, IsCollection: true));
            }
            if (output.Count >= options.Limit * 4) break;
        }
        return output;
    }

    private async Task<IReadOnlyList<ExternalDataResult>> SearchStacAsync(
        ExternalDataSource source,
        ExternalSearchOptions options,
        CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(options.CollectionId)
            ? await SearchStacCollectionsAsync(source, options, cancellationToken)
            : await SearchStacItemsAsync(source, options, cancellationToken);
    }

    private async Task<IReadOnlyList<ExternalDataResult>> SearchStacCollectionsAsync(
        ExternalDataSource source,
        ExternalSearchOptions options,
        CancellationToken cancellationToken)
    {
        var endpoint = new Uri(source.ApiUri!, string.IsNullOrWhiteSpace(options.Query)
            ? "collections"
            : $"collections?q={Uri.EscapeDataString(options.Query)}");
        using var response = await _client.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("collections", out var collections)) return [];

        var terms = options.Query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results = new List<ExternalDataResult>();
        foreach (var collection in collections.EnumerateArray())
        {
            var id = String(collection, "id") ?? string.Empty;
            var title = FirstNonEmpty(String(collection, "title"), id);
            var description = Clean(String(collection, "description"));
            var keywords = collection.TryGetProperty("keywords", out var keywordElement) && keywordElement.ValueKind == JsonValueKind.Array
                ? string.Join(' ', keywordElement.EnumerateArray().Select(x => x.GetString()))
                : string.Empty;
            var haystack = $"{id} {title} {description} {keywords}";
            if (terms.Length > 0 && !terms.All(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase))) continue;

            var landing = FirstLink(collection, "self") ?? new Uri(source.PortalUri, $"collections/{Uri.EscapeDataString(id)}");
            results.Add(new ExternalDataResult(
                source.Id,
                title,
                description,
                "STAC COLLECTION",
                landing,
                null,
                null,
                FirstNonEmpty(String(collection, "license"), source.LicenseNote),
                source.Attribution,
                true,
                id,
                id,
                IsCollection: true));
            if (results.Count >= options.Limit) break;
        }
        return results;
    }

    private async Task<IReadOnlyList<ExternalDataResult>> SearchStacItemsAsync(
        ExternalDataSource source,
        ExternalSearchOptions options,
        CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object?>
        {
            ["collections"] = new[] { options.CollectionId! },
            ["limit"] = options.Limit,
            ["sortby"] = new[] { new Dictionary<string, string> { ["field"] = "datetime", ["direction"] = "desc" } }
        };
        if (options.BoundingBox is { Length: 4 }) body["bbox"] = options.BoundingBox;
        if (options.StartDate is not null || options.EndDate is not null)
        {
            var start = (options.StartDate ?? DateTimeOffset.MinValue).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            var end = (options.EndDate ?? DateTimeOffset.MaxValue).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            body["datetime"] = $"{start}/{end}";
        }
        if (options.MaximumCloudCover is not null)
        {
            body["query"] = new Dictionary<string, object>
            {
                ["eo:cloud_cover"] = new Dictionary<string, double> { ["lte"] = options.MaximumCloudCover.Value }
            };
        }

        var endpoint = new Uri(source.ApiUri!, "search");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonDefaults.Options), Encoding.UTF8, "application/json")
        };
        using var response = await _client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("features", out var features)) return [];

        var results = new List<ExternalDataResult>();
        foreach (var feature in features.EnumerateArray())
        {
            var itemId = String(feature, "id") ?? "STAC item";
            var collectionId = String(feature, "collection") ?? options.CollectionId;
            var properties = feature.TryGetProperty("properties", out var props) ? props : default;
            var acquired = ParseDate(PropertyString(properties, "datetime") ?? PropertyString(properties, "start_datetime"));
            var cloud = PropertyDouble(properties, "eo:cloud_cover");
            var spatial = feature.TryGetProperty("bbox", out var bbox) && bbox.ValueKind == JsonValueKind.Array
                ? string.Join(",", bbox.EnumerateArray().Select(x => x.GetDouble().ToString("0.######", CultureInfo.InvariantCulture)))
                : null;
            var landing = FirstLink(feature, "self") ?? source.PortalUri;
            var addedAsset = false;

            if (feature.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Object)
            {
                foreach (var asset in assets.EnumerateObject())
                {
                    if (!asset.Value.TryGetProperty("href", out var hrefValue) || hrefValue.ValueKind != JsonValueKind.String) continue;
                    if (!Uri.TryCreate(hrefValue.GetString(), UriKind.Absolute, out var href)) continue;
                    var roles = asset.Value.TryGetProperty("roles", out var rolesElement) && rolesElement.ValueKind == JsonValueKind.Array
                        ? rolesElement.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray()
                        : [];
                    if (roles.Length > 0 && !roles.Any(x => x.Equals("data", StringComparison.OrdinalIgnoreCase))) continue;
                    var title = FirstNonEmpty(PropertyString(asset.Value, "title"), asset.Name);
                    var contentType = PropertyString(asset.Value, "type");
                    var hasAuth = asset.Value.TryGetProperty("auth:refs", out var auth) && auth.ValueKind == JsonValueKind.Array && auth.GetArrayLength() > 0;
                    var direct = href.Scheme == Uri.UriSchemeHttps && !hasAuth;
                    results.Add(new ExternalDataResult(
                        source.Id,
                        $"{itemId} — {title}",
                        $"Collection: {collectionId}",
                        FormatFromContentTypeOrPath(contentType, href.AbsolutePath),
                        landing,
                        direct ? href : null,
                        null,
                        source.LicenseNote,
                        source.Attribution,
                        hasAuth || href.Scheme != Uri.UriSchemeHttps,
                        itemId,
                        collectionId,
                        acquired,
                        cloud,
                        spatial,
                        contentType));
                    addedAsset = true;
                    if (results.Count >= options.Limit * 4) break;
                }
            }

            if (!addedAsset)
            {
                results.Add(new ExternalDataResult(
                    source.Id, itemId, $"Collection: {collectionId}", "STAC ITEM", landing, null, null,
                    source.LicenseNote, source.Attribution, true, itemId, collectionId, acquired, cloud, spatial));
            }
            if (results.Count >= options.Limit * 4) break;
        }
        return results;
    }

    private async Task<IReadOnlyList<ExternalDataResult>> SearchCmrAsync(
        ExternalDataSource source,
        ExternalSearchOptions options,
        CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(options.CollectionId)
            ? await SearchCmrCollectionsAsync(source, options, cancellationToken)
            : await SearchCmrGranulesAsync(source, options, cancellationToken);
    }

    private async Task<IReadOnlyList<ExternalDataResult>> SearchCmrCollectionsAsync(
        ExternalDataSource source,
        ExternalSearchOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Query)) return [];
        var uri = new Uri(source.ApiUri!, $"collections.umm_json?keyword={Uri.EscapeDataString(options.Query)}&page_size={options.Limit}&include_granule_counts=true");
        using var response = await _client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("items", out var items)) return [];

        var results = new List<ExternalDataResult>();
        foreach (var item in items.EnumerateArray())
        {
            var conceptId = NestedString(item, "meta", "concept-id") ?? string.Empty;
            var umm = item.TryGetProperty("umm", out var u) ? u : default;
            var title = FirstNonEmpty(PropertyString(umm, "EntryTitle"), PropertyString(umm, "ShortName"), conceptId);
            var description = Clean(PropertyString(umm, "Abstract"));
            var landing = FindRelatedUrl(umm, requireData: false) ?? new Uri(source.PortalUri, $"search?q={Uri.EscapeDataString(title)}");
            results.Add(new ExternalDataResult(
                source.Id, title, description, "NASA COLLECTION", landing, null, null,
                source.LicenseNote, source.Attribution, true, conceptId, conceptId, IsCollection: true));
        }
        return results;
    }

    private async Task<IReadOnlyList<ExternalDataResult>> SearchCmrGranulesAsync(
        ExternalDataSource source,
        ExternalSearchOptions options,
        CancellationToken cancellationToken)
    {
        var parameters = new List<string>
        {
            $"collection_concept_id={Uri.EscapeDataString(options.CollectionId!)}",
            $"page_size={options.Limit}",
            "downloadable=true"
        };
        if (options.BoundingBox is { Length: 4 })
            parameters.Add("bounding_box=" + string.Join(',', options.BoundingBox.Select(x => x.ToString("0.######", CultureInfo.InvariantCulture))));
        if (options.StartDate is not null || options.EndDate is not null)
        {
            var start = (options.StartDate ?? DateTimeOffset.MinValue).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            var end = (options.EndDate ?? DateTimeOffset.MaxValue).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            parameters.Add($"temporal={Uri.EscapeDataString(start + "," + end)}");
        }
        if (!string.IsNullOrWhiteSpace(options.Query)) parameters.Add($"granule_ur[]={Uri.EscapeDataString(options.Query)}");

        var uri = new Uri(source.ApiUri!, "granules.umm_json?" + string.Join('&', parameters));
        using var response = await _client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("items", out var items)) return [];

        var results = new List<ExternalDataResult>();
        foreach (var item in items.EnumerateArray())
        {
            var conceptId = NestedString(item, "meta", "concept-id") ?? string.Empty;
            var umm = item.TryGetProperty("umm", out var u) ? u : default;
            var title = FirstNonEmpty(PropertyString(umm, "GranuleUR"), conceptId);
            var download = FindRelatedUrl(umm, requireData: true);
            var landing = FindRelatedUrl(umm, requireData: false) ?? source.PortalUri;
            var acquired = ParseCmrBeginningDate(umm);
            var size = ParseCmrSize(umm);
            results.Add(new ExternalDataResult(
                source.Id,
                title,
                $"Collection: {options.CollectionId}",
                download is null ? "NASA GRANULE" : NormalizeFormat(Path.GetExtension(download.AbsolutePath).TrimStart('.')),
                landing,
                download,
                size,
                source.LicenseNote,
                source.Attribution,
                true,
                conceptId,
                options.CollectionId,
                acquired));
        }
        return results;
    }

    public async Task<ExternalDownloadReceipt> DownloadWithReceiptAsync(
        ExternalDataResult result,
        string outputPath,
        IProgress<(long Received, long? Total)>? progress = null,
        CancellationToken cancellationToken = default,
        long maximumBytes = 50L * 1024 * 1024 * 1024)
    {
        if (result.DownloadUri is null) throw new InvalidOperationException("รายการนี้ไม่มี Direct Download URL");
        if (result.DownloadUri.Scheme != Uri.UriSchemeHttps) throw new InvalidOperationException("อนุญาตการดาวน์โหลดอัตโนมัติผ่าน HTTPS เท่านั้น");
        if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(outputPath))) throw new InvalidOperationException("กรุณาเลือกโฟลเดอร์ปลายทางที่ถูกต้อง");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var temporary = outputPath + ".partial";
        try
        {
            using var response = await _client.GetAsync(result.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var finalUri = response.RequestMessage?.RequestUri ?? result.DownloadUri;
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
                throw new InvalidDataException("ปลายทางส่งหน้าเว็บ HTML กลับมา ไม่ใช่ไฟล์ข้อมูล กรุณาเปิด Portal และตรวจสิทธิ์/Login");

            var total = response.Content.Headers.ContentLength;
            if (total is > 0 && total > maximumBytes)
                throw new InvalidDataException($"ไฟล์มีขนาด {total:N0} bytes เกินขีดจำกัดความปลอดภัยของงานนี้");

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, true);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[1024 * 128];
            long received = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                received += read;
                if (received > maximumBytes) throw new InvalidDataException("ข้อมูลที่ดาวน์โหลดเกินขนาดสูงสุดที่อนุญาต");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                hash.AppendData(buffer, 0, read);
                progress?.Report((received, total));
            }
            await output.FlushAsync(cancellationToken);
            if (received == 0) throw new InvalidDataException("ดาวน์โหลดได้ไฟล์ว่าง 0 bytes");
            if (total is > 0 && received != total.Value)
                throw new EndOfStreamException($"ดาวน์โหลดไม่ครบ: ได้ {received:N0} จาก {total.Value:N0} bytes");

            var sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            File.Move(temporary, outputPath, overwrite: true);
            var metadataPath = outputPath + ".source.json";
            await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(new
            {
                downloadedAt = DateTimeOffset.Now,
                result.ProviderId,
                result.DatasetId,
                result.CollectionId,
                result.Title,
                result.Description,
                acquiredAt = result.AcquiredAt,
                result.CloudCover,
                result.SpatialSummary,
                landingUrl = result.LandingUri.AbsoluteUri,
                requestedDownloadUrl = result.DownloadUri.AbsoluteUri,
                finalDownloadUrl = finalUri.AbsoluteUri,
                result.Format,
                contentType,
                sizeBytes = received,
                sha256,
                result.LicenseNote,
                result.Attribution
            }, JsonDefaults.Options), cancellationToken);
            return new ExternalDownloadReceipt(outputPath, metadataPath, received, sha256, contentType, finalUri);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                try { File.Delete(temporary); } catch { }
            }
        }
    }

    public async Task<string> DownloadAsync(
        ExternalDataResult result,
        string outputPath,
        IProgress<(long Received, long? Total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var receipt = await DownloadWithReceiptAsync(result, outputPath, progress, cancellationToken);
        return receipt.FilePath;
    }

    public static string SuggestFileName(ExternalDataResult result)
    {
        var candidate = result.DownloadUri is null ? string.Empty : Path.GetFileName(result.DownloadUri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(candidate)) candidate = result.DatasetId ?? result.Title;
        foreach (var invalid in Path.GetInvalidFileNameChars()) candidate = candidate.Replace(invalid, '_');
        candidate = candidate.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(candidate) ? "downloaded_dataset" : candidate;
    }

    public static bool TryParseBoundingBox(string? text, out double[]? boundingBox)
    {
        boundingBox = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        var parts = text.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4) return false;
        var values = new double[4];
        for (var i = 0; i < 4; i++)
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])) return false;
        if (values[0] >= values[2] || values[1] >= values[3] || values[0] < -180 || values[2] > 180 || values[1] < -90 || values[3] > 90)
            return false;
        boundingBox = values;
        return true;
    }

    private static ExternalDataResult ToStaticResult(ExternalDataSource source) => new(
        source.Id, source.Name, string.Join(", ", source.Categories), source.DataTypes,
        source.PortalUri, null, null, source.LicenseNote, source.Attribution, source.RequiresAccount, source.Id, IsCollection: true);

    private static bool SourceMatches(ExternalDataSource source, string query) =>
        string.Join(' ', source.Name, source.Organization, string.Join(' ', source.Keywords), string.Join(' ', source.Categories))
            .Contains(query, StringComparison.OrdinalIgnoreCase);

    private static Uri DatasetLanding(Uri portal, string? datasetId)
    {
        if (string.IsNullOrWhiteSpace(datasetId)) return portal;
        if (portal.Host.Equals("www.data.go.th", StringComparison.OrdinalIgnoreCase))
            return new Uri(portal, $"th/dataset/{Uri.EscapeDataString(datasetId)}");
        if (portal.Host.Equals("opendata.gistda.or.th", StringComparison.OrdinalIgnoreCase))
            return new Uri(portal, $"dataset/{Uri.EscapeDataString(datasetId)}");
        return portal;
    }

    private static bool IsDirectFileResource(Uri uri, string format, JsonElement resource)
    {
        if (KnownFileFormats.Contains(format)) return true;
        var extension = Path.GetExtension(uri.AbsolutePath).TrimStart('.');
        if (KnownFileFormats.Contains(extension)) return true;
        var resourceType = FirstNonEmpty(String(resource, "resource_type"), String(resource, "url_type"));
        if (resourceType.Contains("api", StringComparison.OrdinalIgnoreCase) || resourceType.Contains("service", StringComparison.OrdinalIgnoreCase)) return false;
        return false;
    }

    private static string FormatFromContentTypeOrPath(string? contentType, string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.');
        if (!string.IsNullOrWhiteSpace(extension)) return NormalizeFormat(extension);
        if (contentType?.Contains("geotiff", StringComparison.OrdinalIgnoreCase) == true) return "GEOTIFF";
        if (contentType?.Contains("netcdf", StringComparison.OrdinalIgnoreCase) == true) return "NETCDF";
        if (contentType?.Contains("geo+json", StringComparison.OrdinalIgnoreCase) == true) return "GEOJSON";
        if (contentType?.Contains("zip", StringComparison.OrdinalIgnoreCase) == true) return "ZIP";
        return "DATA";
    }

    private static string NormalizeFormat(string? value) => (value ?? string.Empty).Trim().TrimStart('.').ToUpperInvariant();

    private static bool TryHttpsUri(string? text, out Uri uri)
    {
        if (Uri.TryCreate(text, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps)
        {
            uri = parsed;
            return true;
        }
        uri = null!;
        return false;
    }

    private static Uri? FirstLink(JsonElement element, string relation)
    {
        if (!element.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array) return null;
        foreach (var link in links.EnumerateArray())
        {
            if (!string.Equals(String(link, "rel"), relation, StringComparison.OrdinalIgnoreCase)) continue;
            if (Uri.TryCreate(String(link, "href"), UriKind.Absolute, out var uri)) return uri;
        }
        return null;
    }

    private static Uri? FindRelatedUrl(JsonElement umm, bool requireData)
    {
        if (umm.ValueKind != JsonValueKind.Object || !umm.TryGetProperty("RelatedUrls", out var urls) || urls.ValueKind != JsonValueKind.Array) return null;
        Uri? fallback = null;
        foreach (var item in urls.EnumerateArray())
        {
            if (!Uri.TryCreate(PropertyString(item, "URL"), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) continue;
            var type = PropertyString(item, "Type") ?? string.Empty;
            var subtype = PropertyString(item, "Subtype") ?? string.Empty;
            var data = type.Contains("GET DATA", StringComparison.OrdinalIgnoreCase) || subtype.Contains("DIRECT DOWNLOAD", StringComparison.OrdinalIgnoreCase);
            if (requireData && data) return uri;
            if (!requireData && !data) fallback ??= uri;
        }
        return requireData ? null : fallback;
    }

    private static DateTimeOffset? ParseCmrBeginningDate(JsonElement umm)
    {
        if (!umm.TryGetProperty("TemporalExtent", out var temporal)) return null;
        if (temporal.TryGetProperty("RangeDateTime", out var range))
            return ParseDate(PropertyString(range, "BeginningDateTime"));
        if (temporal.TryGetProperty("SingleDateTime", out var single)) return ParseDate(single.GetString());
        return null;
    }

    private static long? ParseCmrSize(JsonElement umm)
    {
        if (!umm.TryGetProperty("DataGranule", out var dataGranule) ||
            !dataGranule.TryGetProperty("ArchiveAndDistributionInformation", out var info) ||
            info.ValueKind != JsonValueKind.Array) return null;
        foreach (var item in info.EnumerateArray())
        {
            if (item.TryGetProperty("SizeInBytes", out var size) && size.TryGetInt64(out var bytes)) return bytes;
            if (item.TryGetProperty("Size", out size) && size.TryGetDouble(out var megabytes)) return (long)(megabytes * 1024 * 1024);
        }
        return null;
    }

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null;

    private static string? NestedString(JsonElement element, string parent, string property) =>
        element.TryGetProperty(parent, out var nested) ? PropertyString(nested, property) : null;

    private static string? PropertyString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? PropertyDouble(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return number;
        return null;
    }

    private static string? String(JsonElement element, string property) => PropertyString(element, property);

    private static long? Long(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number)) return number;
        return null;
    }

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace("\r", " ").Replace("\n", " ").Trim();
}