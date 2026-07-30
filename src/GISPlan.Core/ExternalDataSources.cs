using System.Net.Http.Headers;
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
    string? DatasetId = null);

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
            "CSV, XLSX, SHP, GeoJSON และรูปแบบอื่นตามหน่วยงาน",
            DataAccessKind.Ckan,
            new Uri("https://www.data.go.th/"),
            new Uri("https://www.data.go.th/api/3/action/package_search"),
            false,
            "ตรวจ License ของแต่ละ Resource ก่อนใช้",
            "ระบุชื่อหน่วยงานเจ้าของชุดข้อมูลและ data.go.th",
            "คำว่า Official หมายถึงเผยแพร่ผ่านหน่วยงานรัฐ แต่ต้องตรวจปี มาตราส่วน และข้อจำกัดของแต่ละชุดข้อมูล",
            ["จังหวัด", "อำเภอ", "ตำบล", "หมู่บ้าน", "ขอบเขต", "dopa", "กรมการปกครอง", "ป่า", "ที่ดิน"]),
        new(
            "gistda-open-data",
            "GISTDA Open Data",
            "สำนักงานพัฒนาเทคโนโลยีอวกาศและภูมิสารสนเทศ (องค์การมหาชน)",
            DataAuthorityLevel.Official,
            ["ภาพถ่ายดาวเทียม", "แผนที่ฐาน", "ภัยพิบัติ", "ทะเลและชายฝั่ง", "พืชเศรษฐกิจ"],
            "ประเทศไทย",
            "แตกต่างตามชุดข้อมูล",
            "Raster, Vector, Service และเอกสารประกอบ",
            DataAccessKind.Ckan,
            new Uri("https://opendata.gistda.or.th/"),
            new Uri("https://opendata.gistda.or.th/api/3/action/package_search"),
            false,
            "ตรวจ License และเงื่อนไขรายชุดข้อมูล",
            "GISTDA Open Data และหน่วยงานเจ้าของข้อมูล",
            "บางชุดเป็นข้อมูลแสดงผลหรือบริการเว็บ ไม่ใช่ไฟล์วิเคราะห์โดยตรง",
            ["gistda", "ดาวเทียม", "น้ำท่วม", "ภัยแล้ง", "ไฟไหม้", "basemap", "coast", "ทะเล"]),
        new(
            "dmcr-change",
            "ระบบปฏิบัติการพิทักษ์ทรัพยากรทางทะเลและชายฝั่ง",
            "กรมทรัพยากรทางทะเลและชายฝั่ง",
            DataAuthorityLevel.Official,
            ["ป่าชายเลน", "ชายฝั่ง", "พื้นที่คงสภาพ", "การเปลี่ยนแปลง"],
            "พื้นที่ชายฝั่งประเทศไทย",
            "มีทั้งข้อมูลรายละเอียดสูงและข้อมูลสรุปตามชั้นข้อมูล",
            "Web map และข้อมูลตามที่ระบบอนุญาต",
            DataAccessKind.Portal,
            new Uri("https://change.dmcr.go.th/"),
            null,
            false,
            "ใช้ตามข้อกำหนดของกรมทรัพยากรทางทะเลและชายฝั่ง",
            "กรมทรัพยากรทางทะเลและชายฝั่ง (DMCR)",
            "ตรวจมาตราส่วน ปีข้อมูล และข้อจำกัดที่แสดงในรายละเอียดชั้นข้อมูลก่อนใช้ตัดสินใจ",
            ["dmcr", "ป่าชายเลน", "mangrove", "ชายฝั่ง", "กัดเซาะ", "ทะเล"]),
        new(
            "copernicus-stac",
            "Copernicus Data Space STAC",
            "European Union / ESA / Copernicus",
            DataAuthorityLevel.Official,
            ["Sentinel", "Copernicus DEM", "Land Monitoring", "ไฟป่า", "ภาพดาวเทียม"],
            "ทั่วโลก",
            "ตั้งแต่ระดับเมตรถึงกิโลเมตรตามผลิตภัณฑ์; Copernicus DEM มี GLO-30 และ GLO-90",
            "STAC metadata, COG, SAFE, GeoTIFF, NetCDF และผลิตภัณฑ์อื่น",
            DataAccessKind.Stac,
            new Uri("https://dataspace.copernicus.eu/"),
            new Uri("https://stac.dataspace.copernicus.eu/v1/collections"),
            true,
            "Copernicus data policy หรือ License ที่ระบุในแต่ละ Collection",
            "European Union, ESA และผู้ผลิตที่ระบุใน Collection",
            "ค้น Metadata ได้โดยไม่จำเป็นต้องดาวน์โหลดทันที แต่ Asset จำนวนมากต้อง Login/OAuth หรือ S3 credentials",
            ["sentinel", "copernicus", "dem", "elevation", "sar", "optical", "burnt area", "land cover"]),
        new(
            "nasa-earthdata-dem",
            "NASA Earthdata Elevation",
            "NASA LP DAAC",
            DataAuthorityLevel.Official,
            ["DEM", "SRTM", "NASADEM", "ความสูง"],
            "ใกล้ทั่วโลกตามขอบเขตของผลิตภัณฑ์",
            "30 m และ 90 m ตามผลิตภัณฑ์",
            "HGT, GeoTIFF และผลิตภัณฑ์ประกอบ",
            DataAccessKind.Portal,
            new Uri("https://www.earthdata.nasa.gov/centers/lp-daac"),
            null,
            true,
            "NASA Earthdata data-use policy ของแต่ละผลิตภัณฑ์",
            "NASA / LP DAAC",
            "ต้องมี Earthdata Login สำหรับการดาวน์โหลดหลายผลิตภัณฑ์ และต้องแยก DEM/DSM กับ Vertical Datum ให้ชัด",
            ["nasa", "srtm", "nasadem", "dem", "elevation", "height"]),
        new(
            "jaxa-aw3d30",
            "ALOS World 3D 30 m (AW3D30)",
            "JAXA EORC",
            DataAuthorityLevel.Official,
            ["DSM", "ความสูง", "ภูมิประเทศ"],
            "ทั่วโลก",
            "ประมาณ 30 m (1 arc-second)",
            "DSM tiles และ Quality/Mask files",
            DataAccessKind.Portal,
            new Uri("https://www.eorc.jaxa.jp/ALOS/en/dataset/aw3d30/aw3d30_e.htm"),
            null,
            true,
            "ใช้งานได้โดยไม่มีค่าใช้จ่ายภายใต้ Terms of Use ของ JAXA",
            "JAXA EORC / ALOS AW3D30",
            "เป็น DSM ซึ่งรวมผลของสิ่งปกคลุมผิวโลกบางส่วน ไม่ควรเรียกเป็น DTM โดยอัตโนมัติ",
            ["jaxa", "alos", "aw3d30", "dem", "dsm", "elevation"]),
        new(
            "esa-worldcover",
            "ESA WorldCover 2021",
            "European Space Agency / WorldCover consortium",
            DataAuthorityLevel.Official,
            ["Land cover", "Mangrove", "Wetland", "Tree cover"],
            "ทั่วโลก",
            "10 m",
            "Cloud Optimized GeoTIFF",
            DataAccessKind.Portal,
            new Uri("https://worldcover2021.esa.int/download"),
            null,
            false,
            "CC BY 4.0",
            "© ESA WorldCover project / Contains modified Copernicus Sentinel data (2021)",
            "ปี 2020 และ 2021 ใช้อัลกอริทึมคนละเวอร์ชัน จึงไม่ควรตีความผลต่างทั้งหมดเป็นการเปลี่ยนแปลงจริง",
            ["esa", "worldcover", "landcover", "mangrove", "wetland", "forest", "10m"]),
        new(
            "jrc-global-surface-water",
            "JRC Global Surface Water",
            "European Commission Joint Research Centre",
            DataAuthorityLevel.Official,
            ["แหล่งน้ำ", "การเปลี่ยนแปลงน้ำ", "Seasonality", "Occurrence"],
            "ทั่วโลก",
            "30 m โดยอิง Landsat",
            "GeoTIFF และ Google Earth Engine assets",
            DataAccessKind.Portal,
            new Uri("https://global-surface-water.appspot.com/download"),
            null,
            false,
            "Copernicus Programme data policy",
            "Source: EC JRC/Google",
            "ต้องเลือกช่วงเวลาและ Layer ให้ตรงคำถาม เช่น occurrence, seasonality, transition หรือ yearly history",
            ["jrc", "surface water", "water", "น้ำ", "wetland", "flood"]),
        new(
            "hydrosheds",
            "HydroSHEDS",
            "HydroSHEDS / WWF / McGill University partners",
            DataAuthorityLevel.OpenResearch,
            ["ลุ่มน้ำ", "แม่น้ำ", "DEM", "Flow direction", "Flow accumulation"],
            "ทั่วโลก; ความครอบคลุมของ v2 ยังเพิ่มเป็นระยะ",
            "ประมาณ 30–90 m และระดับหยาบกว่าตามผลิตภัณฑ์",
            "GeoTIFF, Shapefile และผลิตภัณฑ์ Hydrology",
            DataAccessKind.Portal,
            new Uri("https://www.hydrosheds.org/products"),
            null,
            false,
            "License แตกต่างตามผลิตภัณฑ์ ต้องอ่านก่อนดาวน์โหลด",
            "HydroSHEDS และผู้จัดทำผลิตภัณฑ์ที่เกี่ยวข้อง",
            "ข้อมูลบางรุ่นเป็น Legacy; เลือกเวอร์ชันล่าสุดที่เหมาะกับภูมิภาคและอย่าสับสน DEM ปกติกับ Hydrologically conditioned DEM",
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
    private readonly HttpClient _client;

    public ExternalCatalogSearchService(HttpClient? client = null)
    {
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GISPlan", "0.2"));
    }

    public async Task<IReadOnlyList<ExternalDataResult>> SearchAsync(
        ExternalDataSource source,
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        return source.AccessKind switch
        {
            DataAccessKind.Ckan when source.ApiUri is not null => await SearchCkanAsync(source, query, cancellationToken),
            DataAccessKind.Stac when source.ApiUri is not null => await SearchStacCollectionsAsync(source, query, cancellationToken),
            _ => SourceMatches(source, query)
                ? [ToStaticResult(source)]
                : []
        };
    }

    private async Task<IReadOnlyList<ExternalDataResult>> SearchCkanAsync(
        ExternalDataSource source,
        string query,
        CancellationToken cancellationToken)
    {
        var uri = new UriBuilder(source.ApiUri!)
        {
            Query = $"q={Uri.EscapeDataString(query)}&rows=30"
        }.Uri;

        using var response = await _client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!json.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean())
            return [];
        if (!json.RootElement.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("results", out var datasets))
            return [];

        var output = new List<ExternalDataResult>();
        foreach (var dataset in datasets.EnumerateArray())
        {
            var datasetId = String(dataset, "id") ?? String(dataset, "name");
            var title = String(dataset, "title") ?? datasetId ?? "Untitled dataset";
            var description = Clean(String(dataset, "notes"));
            var landing = DatasetLanding(source.PortalUri, datasetId);
            var added = false;

            if (dataset.TryGetProperty("resources", out var resources))
            {
                foreach (var resource in resources.EnumerateArray())
                {
                    var urlText = String(resource, "url");
                    if (!TryHttpsUri(urlText, out var download)) continue;
                    var format = String(resource, "format") ?? Path.GetExtension(download.AbsolutePath).TrimStart('.').ToUpperInvariant();
                    var resourceTitle = String(resource, "name");
                    output.Add(new ExternalDataResult(
                        source.Id,
                        string.IsNullOrWhiteSpace(resourceTitle) ? title : $"{title} — {resourceTitle}",
                        description,
                        format,
                        landing,
                        download,
                        Long(resource, "size"),
                        source.LicenseNote,
                        source.Attribution,
                        source.RequiresAccount,
                        datasetId));
                    added = true;
                }
            }

            if (!added)
                output.Add(new ExternalDataResult(
                    source.Id, title, description, "Catalog", landing, null, null,
                    source.LicenseNote, source.Attribution, source.RequiresAccount, datasetId));
        }
        return output;
    }

    private async Task<IReadOnlyList<ExternalDataResult>> SearchStacCollectionsAsync(
        ExternalDataSource source,
        string query,
        CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(source.ApiUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("collections", out var collections)) return [];

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results = new List<ExternalDataResult>();
        foreach (var collection in collections.EnumerateArray())
        {
            var id = String(collection, "id") ?? string.Empty;
            var title = String(collection, "title") ?? id;
            var description = Clean(String(collection, "description"));
            var keywords = collection.TryGetProperty("keywords", out var keywordElement)
                ? string.Join(' ', keywordElement.EnumerateArray().Select(x => x.GetString()))
                : string.Empty;
            var haystack = $"{id} {title} {description} {keywords}";
            if (!terms.All(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase))) continue;

            var landing = new Uri($"https://browser.stac.dataspace.copernicus.eu/collections/{Uri.EscapeDataString(id)}");
            results.Add(new ExternalDataResult(
                source.Id,
                title,
                description,
                "STAC Collection",
                landing,
                null,
                null,
                String(collection, "license") ?? source.LicenseNote,
                source.Attribution,
                true,
                id));
        }
        return results.Take(50).ToList();
    }

    public async Task<string> DownloadAsync(
        ExternalDataResult result,
        string outputPath,
        IProgress<(long Received, long? Total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (result.DownloadUri is null)
            throw new InvalidOperationException("รายการนี้ไม่มี Direct Download URL");
        if (result.DownloadUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("GISPlan อนุญาตการดาวน์โหลดอัตโนมัติผ่าน HTTPS เท่านั้น");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var temporary = outputPath + ".partial";
        try
        {
            using var response = await _client.GetAsync(result.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, true);
            var buffer = new byte[1024 * 128];
            long received = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                received += read;
                progress?.Report((received, total));
            }
            await output.FlushAsync(cancellationToken);
            File.Move(temporary, outputPath, overwrite: true);

            var metadataPath = outputPath + ".source.json";
            await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(new
            {
                downloadedAt = DateTimeOffset.Now,
                result.ProviderId,
                result.DatasetId,
                result.Title,
                result.Description,
                landingUrl = result.LandingUri,
                downloadUrl = result.DownloadUri,
                result.Format,
                result.LicenseNote,
                result.Attribution
            }, JsonDefaults.Options), cancellationToken);
            return outputPath;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                try { File.Delete(temporary); } catch { }
            }
        }
    }

    private static ExternalDataResult ToStaticResult(ExternalDataSource source) => new(
        source.Id, source.Name, string.Join(", ", source.Categories), source.DataTypes,
        source.PortalUri, null, null, source.LicenseNote, source.Attribution, source.RequiresAccount, source.Id);

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

    private static string? String(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? Long(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number)) return number;
        return null;
    }

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("\r", " ").Replace("\n", " ").Trim();
}
