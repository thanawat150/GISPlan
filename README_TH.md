# GISPlan

GISPlan คือโปรแกรม Windows แบบ Portable สำหรับงาน GIS พื้นฐาน โดยใช้เครื่องมือที่มีอยู่ในเครื่อง เช่น QGIS, ArcGIS Pro หรือ GDAL/OGR

โปรแกรมออกแบบให้:

- เปิดจาก `GISPlan.exe` โดยไม่ติดตั้ง
- ไม่ขอสิทธิ์ Administrator
- ไม่เขียน `Program Files`
- เก็บ Runtime, Log และ Job Report ใน `%LOCALAPPDATA%\GISPlan`
- เก็บผลลัพธ์ในตำแหน่งที่ผู้ใช้เลือก
- ตรวจ Input และ Runtime ก่อนเริ่มงาน
- สร้าง Preflight Report, Run Manifest, Log และ QA Report
- ไม่แก้ไฟล์ต้นฉบับ
- สร้าง Output แบบ Versioned เมื่อชื่อเดิมมีอยู่แล้ว

## งานที่รองรับใน Version 1

```text
Inspect
Reproject Vector
Clip Vector
Buffer Vector
Convert Vector Format
```

Backend ที่รองรับ:

```text
GDAL/OGR
QGIS Processing
ArcGIS Pro / ArcPy
```

ระบบเลือกเครื่องมืออัตโนมัติ หรือผู้ใช้กำหนดเองได้

## สถานะสำคัญ

Source Code และระบบ Build อยู่ใน Repository แล้ว แต่ไฟล์ `GISPlan.exe` ต้องถูก Build ก่อนใช้งาน

GISPlan ไม่ได้รวม QGIS หรือ ArcGIS Pro ไว้ในตัว EXE รุ่นนี้ โปรแกรมจะตรวจและใช้ GIS Runtime ที่มีอยู่ในเครื่อง

## Build บน Windows

ต้องมี .NET 8 SDK เฉพาะเครื่องที่ใช้ Build

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build_portable.ps1
```

ไฟล์ที่ได้:

```text
dist\GISPlan-win-x64\GISPlan.exe
```

การ Publish ใช้ Self-contained Single-file ดังนั้นเครื่องผู้ใช้งานไม่ต้องติดตั้ง .NET Runtime แยก

## คัดลอกไป Desktop โดยไม่ใช้ Admin

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\copy_to_desktop.ps1
```

ผลลัพธ์:

```text
<Desktop>\GISPlan\GISPlan.exe
<Desktop>\GISPlan.lnk
```

Script จะหา Desktop จริงผ่าน Windows API จึงรองรับ Desktop ที่อยู่ใน OneDrive

## ใช้งานโปรแกรม

1. เปิด `GISPlan.exe`
2. กด `ตรวจโปรแกรม GIS`
3. เลือกประเภทงาน
4. เลือก Input และ Output
5. ระบุ Target CRS หรือระยะ Buffer เมื่อเกี่ยวข้อง
6. กด `ตรวจข้อมูล`
7. กด `เริ่มประมวลผล`
8. เปิด Output และตรวจ `qa_report.json`

## Runtime และ Log

```text
%LOCALAPPDATA%\GISPlan\
├── jobs\
├── logs\
├── cache\
└── settings\local_runtime.json
```

แต่ละ Job สร้าง:

```text
gis_job.json
preflight_report.json
run.log
qa_report.json
run_manifest.json
```

## การเลือกเครื่องมือ

ค่า `Auto` ใช้กฎโดยย่อ:

```text
Inspect / Reproject / Clip / Convert
→ GDAL ก่อน เมื่อพร้อม
→ QGIS
→ ArcGIS Pro

Buffer
→ QGIS ก่อน
→ ArcGIS Pro
```

งาน ArcGIS ต้องมี ArcGIS Pro และ License ที่ใช้งานได้

## ทดสอบ

Smoke Tests:

```powershell
dotnet run --project .\tests\GISPlan.SmokeTests\GISPlan.SmokeTests.csproj --configuration Release
```

No-admin Static Acceptance:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test_no_admin.ps1
```

## GitHub Actions

Workflow `Build GISPlan Portable` จะ:

```text
Restore
→ Smoke Test
→ Publish GISPlan.exe
→ No-admin Static Acceptance
→ Upload Artifact
```

เปิดแท็บ Actions แล้วกด `Run workflow` เพื่อสร้างไฟล์ดาวน์โหลด

เมื่อ Push Tag เช่น `v0.1.0` ระบบจะสร้าง GitHub Release พร้อม ZIP

## ข้อจำกัด Version 1

- รองรับ Vector เป็นหลัก
- Dataset หลาย Layer อาจต้องระบุ Layer เพิ่มในรุ่นถัดไป
- ยังไม่มี Raster Processing และ Map Layout ในหน้าจอ
- QA รุ่นแรกตรวจไฟล์ การเปิดใช้งาน และ CRS เท่าที่ Runtime รองรับ
- การรับรองความถูกต้องขั้นสุดท้ายยังต้องให้มนุษย์ตรวจ
- โปรแกรมไม่หลบข้อจำกัดด้าน Policy ขององค์กร

## Roadmap ต่อไป

```text
Raster Clip / Reproject / Mosaic
Zonal Statistics
QGIS/ArcGIS Layout Export
Batch Processing
Job Queue and Resume
Layer selection for multi-layer datasets
Map preview
Project templates
```

หลักการทำงานฉบับเต็มอยู่ที่:

```text
skills/gis-specialist/SKILL.md
skills/gis-specialist/DESKTOP_NO_ADMIN.md
AGENTS.md
```
