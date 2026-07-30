using System.Globalization;
using System.Text.Json;

namespace GISPlan.Core;

public sealed record LanguageOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed class LocalizationService
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["app.title"] = "GISPlan",
        ["app.subtitle"] = "Tell us what you want to do. GISPlan will show where to go or prepare a supported task.",
        ["language"] = "Language",
        ["simple_mode"] = "Step-by-step mode",
        ["assistant.prompt"] = "Describe what you want to do, for example: calculate area, buffer 100 metres, create points from Excel...",
        ["assistant.guide"] = "Show me where",
        ["assistant.prepare"] = "Prepare automatically",
        ["assistant.open"] = "Open the related screen",
        ["assistant.no_query"] = "Type what you want to do first.",
        ["assistant.result"] = "Recommendation",
        ["new_job"] = "New GIS task",
        ["resume_job"] = "Resume a previous task",
        ["detect_runtime"] = "Check GIS software",
        ["cancel"] = "Cancel",
        ["ready"] = "Ready",
        ["runtime_status"] = "System details",
        ["show_details"] = "Show technical details",
        ["hide_details"] = "Hide technical details",
        ["objective"] = "What do you want?",
        ["operation"] = "Task",
        ["tool"] = "Processing tool",
        ["input"] = "Input file",
        ["overlay"] = "Boundary / mask",
        ["output"] = "Output file",
        ["target_crs"] = "Working coordinate system",
        ["buffer"] = "Buffer distance",
        ["metres"] = "metres",
        ["choose_file"] = "Choose file",
        ["choose_output"] = "Choose output",
        ["check_data"] = "Check data first",
        ["run"] = "Start",
        ["open_output"] = "Open result",
        ["status_log"] = "Status and details",
        ["operation.inspect"] = "Check data and CRS",
        ["operation.reproject"] = "Change coordinate system",
        ["operation.clip"] = "Cut data by a boundary",
        ["operation.buffer"] = "Create an area around features",
        ["operation.convert"] = "Convert file format",
        ["tool.auto"] = "Choose automatically",
        ["tool.qgis"] = "QGIS",
        ["tool.arcgis"] = "ArcGIS Pro",
        ["tool.gdal"] = "GDAL / OGR",
        ["help.inspect"] = "Choose a file. GISPlan will inspect its metadata and coordinate system without changing the source file.",
        ["help.reproject"] = "Choose the source file and the target EPSG code. A new output file will be created.",
        ["help.clip"] = "Choose the data to cut and a polygon boundary or mask. Both layers should have valid coordinate systems.",
        ["help.buffer"] = "Enter a distance in metres and choose a projected working CRS, such as the correct UTM zone. GISPlan will reproject temporarily before buffering.",
        ["help.convert"] = "Choose an input and an output extension, such as GeoPackage, GeoJSON, Shapefile or KML.",
        ["warning.buffer_crs"] = "Buffer needs a projected CRS with metre units. Do not use EPSG:4326 for distance buffering.",
        ["status.checking_runtime"] = "Checking QGIS, ArcGIS Pro and GDAL",
        ["status.runtime_checked"] = "GIS software check complete",
        ["status.runtime_missing"] = "No compatible GIS software was found",
        ["status.preflight"] = "Checking input, CRS and output settings",
        ["status.preflight_passed"] = "Data check passed",
        ["status.preflight_failed"] = "Please fix the highlighted settings",
        ["status.running"] = "Starting task",
        ["status.cancelled"] = "Task cancelled",
        ["status.not_found"] = "Not found",
        ["guide.area.title"] = "Calculate area and add it to the attribute table",
        ["guide.area.route"] = "QGIS → Open Attribute Table → Field Calculator",
        ["guide.area.summary"] = "Use a projected CRS first, then create area_sqm and area_rai fields. This automation is planned but is not in the current processing screen yet.",
        ["guide.buffer.title"] = "Create a buffer in metres",
        ["guide.buffer.route"] = "GISPlan → New GIS task → Create an area around features",
        ["guide.buffer.summary"] = "GISPlan can prepare this task. Select the correct projected CRS before starting.",
        ["guide.clip.title"] = "Clip data with a boundary",
        ["guide.clip.route"] = "GISPlan → New GIS task → Cut data by a boundary",
        ["guide.clip.summary"] = "Choose the main layer, then choose the polygon mask or project boundary.",
        ["guide.reproject.title"] = "Change a layer's coordinate system",
        ["guide.reproject.route"] = "GISPlan → New GIS task → Change coordinate system",
        ["guide.reproject.summary"] = "Choose the target EPSG code. GISPlan creates a new file and keeps the source unchanged.",
        ["guide.convert.title"] = "Convert GIS file format",
        ["guide.convert.route"] = "GISPlan → New GIS task → Convert file format",
        ["guide.convert.summary"] = "Choose the extension of the output file, such as .gpkg, .geojson or .kml.",
        ["guide.multipart.title"] = "Convert multipart features to singlepart",
        ["guide.multipart.route"] = "QGIS → Processing Toolbox → Multipart to singleparts",
        ["guide.multipart.summary"] = "The current GISPlan screen does not automate this yet. Keep the original feature ID before splitting.",
        ["guide.split.title"] = "Split data by an attribute",
        ["guide.split.route"] = "QGIS → Processing Toolbox → Split vector layer",
        ["guide.split.summary"] = "Choose the field used for grouping. GISPlan automation for this workflow is planned.",
        ["guide.excel.title"] = "Create points or boundaries from Excel",
        ["guide.excel.route"] = "QGIS → Layer → Add Layer → Add Delimited Text Layer, or use the future GISPlan Excel wizard",
        ["guide.excel.summary"] = "Confirm the X/Y columns, CRS, UTM zone and row order before creating geometry.",
        ["guide.map.title"] = "Create a map or change layer colours",
        ["guide.map.route"] = "QGIS → Layer Styling for colours; Project → New Print Layout for PDF maps",
        ["guide.map.summary"] = "Use QGIS for visual editing, labels, drone imagery and layouts. GISPlan will later provide templates and presets.",
        ["guide.drone.title"] = "Process drone photos and display the orthomosaic",
        ["guide.drone.route"] = "GISPlan Drone module → Metashape workflow → Export GeoTIFF → Open in QGIS",
        ["guide.drone.summary"] = "The drone module is planned. For now, use the existing Metashape workflow and add the exported GeoTIFF to QGIS.",
        ["guide.satellite.title"] = "Search or download satellite imagery",
        ["guide.satellite.route"] = "Future Satellite module → Select AOI, dates, cloud limit and provider",
        ["guide.satellite.summary"] = "This is not automated in the current build. It will use provider adapters such as STAC, Copernicus or Earth Engine.",
        ["guide.unknown.title"] = "I could not match that request yet",
        ["guide.unknown.route"] = "Try a short phrase such as buffer, clip, calculate area, Excel to points, KML, drone or map colours.",
        ["guide.unknown.summary"] = "GISPlan will keep the request local. It will not run or change data until you confirm a supported workflow."
    };

    private static readonly IReadOnlyDictionary<string, string> Thai = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["app.title"] = "GISPlan",
        ["app.subtitle"] = "พิมพ์สิ่งที่ต้องการ ระบบจะบอกว่าไปตรงไหน หรือเตรียมงานที่รองรับให้",
        ["language"] = "ภาษา",
        ["simple_mode"] = "โหมดพาไปทีละขั้น",
        ["assistant.prompt"] = "พิมพ์สิ่งที่ต้องการ เช่น คำนวณพื้นที่, Buffer 100 เมตร, สร้างจุดจาก Excel...",
        ["assistant.guide"] = "สอนว่าไปตรงไหน",
        ["assistant.prepare"] = "เตรียมงานให้อัตโนมัติ",
        ["assistant.open"] = "เปิดหน้าที่เกี่ยวข้อง",
        ["assistant.no_query"] = "กรุณาพิมพ์สิ่งที่ต้องการทำก่อน",
        ["assistant.result"] = "คำแนะนำ",
        ["new_job"] = "สร้างงาน GIS ใหม่",
        ["resume_job"] = "ทำงานต่อจากงานเดิม",
        ["detect_runtime"] = "ตรวจโปรแกรม GIS",
        ["cancel"] = "ยกเลิก",
        ["ready"] = "พร้อมใช้งาน",
        ["runtime_status"] = "รายละเอียดระบบ",
        ["show_details"] = "แสดงรายละเอียดทางเทคนิค",
        ["hide_details"] = "ซ่อนรายละเอียดทางเทคนิค",
        ["objective"] = "ต้องการทำอะไร",
        ["operation"] = "ประเภทงาน",
        ["tool"] = "เครื่องมือประมวลผล",
        ["input"] = "ไฟล์ข้อมูล",
        ["overlay"] = "ขอบเขต / Mask",
        ["output"] = "ไฟล์ผลลัพธ์",
        ["target_crs"] = "ระบบพิกัดที่ใช้ทำงาน",
        ["buffer"] = "ระยะ Buffer",
        ["metres"] = "เมตร",
        ["choose_file"] = "เลือกไฟล์",
        ["choose_output"] = "เลือกที่เก็บ",
        ["check_data"] = "ตรวจข้อมูลก่อน",
        ["run"] = "เริ่มทำงาน",
        ["open_output"] = "เปิดผลลัพธ์",
        ["status_log"] = "สถานะและรายละเอียด",
        ["operation.inspect"] = "ตรวจข้อมูลและ CRS",
        ["operation.reproject"] = "เปลี่ยนระบบพิกัด",
        ["operation.clip"] = "ตัดข้อมูลตามขอบเขต",
        ["operation.buffer"] = "สร้างพื้นที่รอบจุด/เส้น/ขอบเขต",
        ["operation.convert"] = "แปลงรูปแบบไฟล์",
        ["tool.auto"] = "เลือกให้อัตโนมัติ",
        ["tool.qgis"] = "QGIS",
        ["tool.arcgis"] = "ArcGIS Pro",
        ["tool.gdal"] = "GDAL / OGR",
        ["help.inspect"] = "เลือกไฟล์ ระบบจะตรวจข้อมูลและระบบพิกัดโดยไม่แก้ไฟล์ต้นฉบับ",
        ["help.reproject"] = "เลือกไฟล์ต้นทางและ EPSG ปลายทาง ระบบจะสร้างไฟล์ใหม่ให้",
        ["help.clip"] = "เลือกข้อมูลหลัก แล้วเลือก Polygon ที่ใช้เป็นขอบเขตตัด ข้อมูลทั้งสองต้องมี CRS ที่ถูกต้อง",
        ["help.buffer"] = "ใส่ระยะเป็นเมตร และเลือกระบบพิกัดแบบ Projected เช่น UTM Zone ที่ถูกต้อง ระบบจะแปลงชั่วคราวก่อน Buffer",
        ["help.convert"] = "เลือกไฟล์ต้นทางและนามสกุลผลลัพธ์ เช่น GeoPackage, GeoJSON, Shapefile หรือ KML",
        ["warning.buffer_crs"] = "Buffer ต้องใช้ CRS แบบ Projected ที่มีหน่วยเป็นเมตร ห้ามใช้ EPSG:4326 คำนวณระยะโดยตรง",
        ["status.checking_runtime"] = "กำลังตรวจ QGIS, ArcGIS Pro และ GDAL",
        ["status.runtime_checked"] = "ตรวจโปรแกรม GIS แล้ว",
        ["status.runtime_missing"] = "ยังไม่พบโปรแกรม GIS ที่รองรับ",
        ["status.preflight"] = "กำลังตรวจไฟล์ ระบบพิกัด และที่เก็บผลลัพธ์",
        ["status.preflight_passed"] = "ตรวจข้อมูลผ่านแล้ว",
        ["status.preflight_failed"] = "กรุณาแก้ค่าที่แจ้งก่อนเริ่มงาน",
        ["status.running"] = "กำลังเริ่มงาน",
        ["status.cancelled"] = "ยกเลิกงานแล้ว",
        ["status.not_found"] = "ไม่พบ",
        ["guide.area.title"] = "คำนวณพื้นที่และใส่ใน Attribute",
        ["guide.area.route"] = "QGIS → เปิดตาราง Attribute → Field Calculator",
        ["guide.area.summary"] = "ควรแปลงเป็น CRS หน่วยเมตรก่อน แล้วสร้าง Field area_sqm และ area_rai ระบบอัตโนมัติส่วนนี้กำลังวางแผนและยังไม่มีในหน้าประมวลผลปัจจุบัน",
        ["guide.buffer.title"] = "สร้าง Buffer ตามระยะเมตร",
        ["guide.buffer.route"] = "GISPlan → สร้างงาน GIS ใหม่ → สร้างพื้นที่รอบจุด/เส้น/ขอบเขต",
        ["guide.buffer.summary"] = "GISPlan เตรียมงานนี้ได้ กรุณาเลือก CRS แบบ Projected ที่ถูกต้องก่อนเริ่ม",
        ["guide.clip.title"] = "ตัดข้อมูลด้วยขอบเขต",
        ["guide.clip.route"] = "GISPlan → สร้างงาน GIS ใหม่ → ตัดข้อมูลตามขอบเขต",
        ["guide.clip.summary"] = "เลือก Layer หลัก แล้วเลือก Polygon ขอบเขตโครงการหรือ Mask",
        ["guide.reproject.title"] = "เปลี่ยนระบบพิกัดของข้อมูล",
        ["guide.reproject.route"] = "GISPlan → สร้างงาน GIS ใหม่ → เปลี่ยนระบบพิกัด",
        ["guide.reproject.summary"] = "เลือก EPSG ปลายทาง ระบบจะสร้างไฟล์ใหม่และไม่แก้ไฟล์ต้นฉบับ",
        ["guide.convert.title"] = "แปลงรูปแบบไฟล์ GIS",
        ["guide.convert.route"] = "GISPlan → สร้างงาน GIS ใหม่ → แปลงรูปแบบไฟล์",
        ["guide.convert.summary"] = "เลือกนามสกุลไฟล์ผลลัพธ์ เช่น .gpkg, .geojson หรือ .kml",
        ["guide.multipart.title"] = "แยก Multipart เป็น Singlepart",
        ["guide.multipart.route"] = "QGIS → Processing Toolbox → Multipart to singleparts",
        ["guide.multipart.summary"] = "GISPlan รุ่นปัจจุบันยังไม่ทำอัตโนมัติ ควรเก็บรหัส Feature เดิมไว้ก่อนแยก",
        ["guide.split.title"] = "แยกข้อมูลตาม Attribute",
        ["guide.split.route"] = "QGIS → Processing Toolbox → Split vector layer",
        ["guide.split.summary"] = "เลือก Field ที่ใช้แบ่งกลุ่ม ระบบอัตโนมัติของ GISPlan สำหรับงานนี้อยู่ในแผนพัฒนา",
        ["guide.excel.title"] = "สร้างจุดหรือขอบเขตจาก Excel",
        ["guide.excel.route"] = "QGIS → Layer → Add Layer → Add Delimited Text Layer หรือใช้ Excel Wizard ของ GISPlan ในอนาคต",
        ["guide.excel.summary"] = "ต้องตรวจคอลัมน์ X/Y, CRS, UTM Zone และลำดับจุดก่อนสร้าง Geometry",
        ["guide.map.title"] = "ทำแผนที่หรือปรับสี Layer",
        ["guide.map.route"] = "QGIS → Layer Styling สำหรับสี และ Project → New Print Layout สำหรับแผนที่ PDF",
        ["guide.map.summary"] = "ใช้ QGIS สำหรับแก้สี Label แสดงภาพโดรน และทำ Layout โดย GISPlan จะเพิ่ม Template และ Preset ภายหลัง",
        ["guide.drone.title"] = "ประมวลผลภาพโดรนและแสดง Orthomosaic",
        ["guide.drone.route"] = "โมดูล Drone ของ GISPlan → Metashape → Export GeoTIFF → เปิดใน QGIS",
        ["guide.drone.summary"] = "โมดูลโดรนยังอยู่ในแผนพัฒนา ตอนนี้ให้ใช้ Workflow Metashape เดิมแล้วนำ GeoTIFF เข้า QGIS",
        ["guide.satellite.title"] = "ค้นหาหรือดาวน์โหลดภาพดาวเทียม",
        ["guide.satellite.route"] = "โมดูล Satellite ในอนาคต → เลือก AOI, วันที่, เมฆ และแหล่งข้อมูล",
        ["guide.satellite.summary"] = "รุ่นปัจจุบันยังไม่ทำอัตโนมัติ โดยจะเชื่อมผ่าน STAC, Copernicus หรือ Earth Engine",
        ["guide.unknown.title"] = "ยังไม่เข้าใจคำขอนี้",
        ["guide.unknown.route"] = "ลองพิมพ์สั้น ๆ เช่น Buffer, Clip, คำนวณพื้นที่, Excel เป็นจุด, KML, ภาพโดรน หรือปรับสีแผนที่",
        ["guide.unknown.summary"] = "ระบบจะไม่รันหรือเปลี่ยนข้อมูลจนกว่าคุณจะยืนยัน Workflow ที่รองรับ"
    };

    private readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);

    public LocalizationService(string languageCode = "th-TH") => SetLanguage(languageCode);

    public string LanguageCode { get; private set; } = "th-TH";

    public IReadOnlyList<LanguageOption> GetAvailableLanguages()
    {
        var result = new List<LanguageOption>
        {
            new("th-TH", "ไทย"),
            new("en-US", "English")
        };

        var languageDirectory = Path.Combine(AppPaths.SettingsRoot, "languages");
        Directory.CreateDirectory(languageDirectory);
        foreach (var file in Directory.EnumerateFiles(languageDirectory, "*.json"))
        {
            var code = Path.GetFileNameWithoutExtension(file);
            if (result.Any(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase))) continue;
            string display;
            try { display = CultureInfo.GetCultureInfo(code).NativeName; }
            catch { display = code; }
            result.Add(new LanguageOption(code, display));
        }
        return result;
    }

    public void SetLanguage(string? languageCode)
    {
        LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "th-TH" : languageCode;
        _strings.Clear();
        foreach (var item in English) _strings[item.Key] = item.Value;
        if (LanguageCode.StartsWith("th", StringComparison.OrdinalIgnoreCase))
            foreach (var item in Thai) _strings[item.Key] = item.Value;

        var path = Path.Combine(AppPaths.SettingsRoot, "languages", LanguageCode + ".json");
        if (!File.Exists(path)) return;
        try
        {
            var custom = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            if (custom is not null)
                foreach (var item in custom) _strings[item.Key] = item.Value;
        }
        catch
        {
            // A broken optional language pack must not prevent the application from starting.
        }
    }

    public string Text(string key) => _strings.TryGetValue(key, out var value) ? value : key;
}

public sealed class UserPreferences
{
    public string LanguageCode { get; set; } = "th-TH";
    public bool SimpleMode { get; set; } = true;

    public static string FilePath => Path.Combine(AppPaths.SettingsRoot, "user_preferences.json");

    public static UserPreferences Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(FilePath), JsonDefaults.Options) ?? new UserPreferences();
        }
        catch { }
        return new UserPreferences();
    }

    public void Save()
    {
        AppPaths.EnsureCreated();
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonDefaults.Options));
        File.Move(temp, FilePath, overwrite: true);
    }
}

public sealed record GuideResult(
    string Id,
    string Title,
    string Route,
    string Summary,
    GisOperation? SuggestedOperation,
    bool CanPrepareAutomatically,
    bool RequiresConfirmation = true);

public sealed class GuidedAssistantService
{
    private sealed record Entry(string Id, string[] Aliases, GisOperation? Operation, bool CanPrepare);

    private static readonly Entry[] Entries =
    [
        new("area", ["คำนวณพื้นที่", "หาพื้นที่", "พื้นที่ไร่", "area", "calculate area", "attribute area"], null, false),
        new("buffer", ["buffer", "บัฟเฟอร์", "พื้นที่รอบ", "รัศมี", "ระยะรอบ"], GisOperation.BufferVector, true),
        new("clip", ["clip", "ตัดข้อมูล", "ตัดตามขอบเขต", "mask", "ครอบขอบเขต"], GisOperation.ClipVector, true),
        new("reproject", ["reproject", "เปลี่ยนระบบพิกัด", "แปลง crs", "utm", "epsg"], GisOperation.ReprojectVector, true),
        new("convert", ["แปลงไฟล์", "convert", "สร้าง kml", "kml", "kmz", "geojson", "shapefile", "gpkg"], GisOperation.ConvertVector, true),
        new("multipart", ["multipart", "singlepart", "แยกชิ้น", "แยก polygon"], null, false),
        new("split", ["แยกตาม attribute", "แยกตามฟิลด์", "แยกตามจังหวัด", "split by attribute", "split vector"], null, false),
        new("excel", ["excel", "สร้าง point", "สร้างจุด", "สร้าง polygon จาก excel", "พิกัด excel", "excel to point"], null, false),
        new("map", ["ทำแผนที่", "ปรับสี", "สีขอบ", "layout", "แผนที่ pdf", "label", "symbology"], null, false),
        new("drone", ["โดรน", "ต่อภาพ", "orthomosaic", "metashape", "ภาพโดรน"], null, false),
        new("satellite", ["ดาวเทียม", "sentinel", "landsat", "ndvi", "stac", "copernicus", "earth engine"], null, false)
    ];

    private readonly LocalizationService _localizer;

    public GuidedAssistantService(LocalizationService localizer) => _localizer = localizer;

    public GuideResult Find(string? query)
    {
        var normalized = Normalize(query);
        var entry = Entries
            .Select(item => new
            {
                Item = item,
                Score = item.Aliases.Count(alias => normalized.Contains(Normalize(alias), StringComparison.Ordinal))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Item.Aliases.Max(a => a.Length))
            .Select(x => x.Item)
            .FirstOrDefault();

        var id = entry?.Id ?? "unknown";
        return new GuideResult(
            id,
            _localizer.Text($"guide.{id}.title"),
            _localizer.Text($"guide.{id}.route"),
            _localizer.Text($"guide.{id}.summary"),
            entry?.Operation,
            entry?.CanPrepare ?? false);
    }

    public string Format(GuideResult result) =>
        $"{result.Title}{Environment.NewLine}{Environment.NewLine}" +
        $"{result.Route}{Environment.NewLine}{Environment.NewLine}" +
        result.Summary;

    private static string Normalize(string? value) =>
        string.Concat((value ?? string.Empty).Trim().ToLowerInvariant().Where(c => !char.IsWhiteSpace(c)));
}
