# Desktop No-admin Deployment Contract

## Goal

ติดตั้ง GISPlan เป็น Workspace ภายใต้ Desktop ของผู้ใช้ โดยใช้บัญชี Windows ปกติและไม่ขอสิทธิ์ Administrator

```text
<Resolved Desktop>\GISPlan
```

ต้องหา Desktop จริงด้วย:

```powershell
[Environment]::GetFolderPath('Desktop')
```

ห้ามสมมติว่า Desktop อยู่ใน `C:\Users\<name>\Desktop` เพราะ Desktop อาจอยู่ใน OneDrive หรือตำแหน่งอื่น

## No-admin Rules

ห้าม:

```text
ขอ UAC elevation
เขียน Program Files
เขียน Windows หรือ System32
ติดตั้ง Service หรือ Driver
แก้ Machine-wide PATH
เขียน HKEY_LOCAL_MACHINE
ติดตั้ง Package แบบ Global
หลบข้อจำกัดขององค์กร
```

อนุญาตให้เขียนเฉพาะ:

```text
Resolved Desktop\GISPlan
%LOCALAPPDATA%\GISPlan
%APPDATA%\GISPlan เมื่อจำเป็น
User-owned project folders
```

ตำแหน่งมาตรฐาน:

```text
Workspace: <Desktop>\GISPlan
Runtime: %LOCALAPPDATA%\GISPlan
Cache: %LOCALAPPDATA%\GISPlan\cache
Logs: %LOCALAPPDATA%\GISPlan\logs
Settings: %LOCALAPPDATA%\GISPlan\settings
Outputs: <Desktop>\GISPlan\outputs
```

## GIS Software

GISPlan เป็น Skill Workspace ไม่ใช่ตัวติดตั้ง QGIS หรือ ArcGIS Pro

ระบบต้อง:

1. ตรวจ QGIS, ArcGIS Pro, GDAL และ Python ที่มีอยู่แล้ว
2. ใช้ Path จาก `config/local_runtime.json` เมื่อยังถูกต้อง
3. อนุญาตให้ผู้ใช้ระบุ Executable Path เอง
4. ห้ามติดตั้ง GIS Software แบบ System-wide โดยอัตโนมัติ
5. ห้ามแก้ PATH หรือ Registry ระดับเครื่อง
6. เมื่อไม่พบเครื่องมือ ให้ติดตั้ง Workspace ต่อได้และรายงานสิ่งที่ขาด
7. ArcGIS Workflow ต้องตรวจโปรแกรมและ License ก่อนใช้
8. ห้ามรายงานว่า Tool ใช้งานได้เมื่อยังไม่ได้ตรวจจริง

ตรวจเฉพาะ:

```text
PATH
%LOCALAPPDATA%\Programs
%PROGRAMFILES%
%PROGRAMFILES(X86)%
Path ที่ผู้ใช้ระบุ
```

ห้ามค้นหาทั้ง Drive

## Python Environment

เมื่อมี Python ให้สร้าง Environment ใน User Space:

```text
<Desktop>\GISPlan\.venv
```

กฎ:

- ไม่ติดตั้ง Global Package
- ไม่เขียน Program Files
- บันทึก Python Path และ Version ใน `config/local_runtime.json`
- QGIS Python และ ArcPy ต้องใช้ Runtime ของโปรแกรมนั้นโดยเฉพาะ
- ติดตั้ง Dependency เท่าที่ Job ปัจจุบันต้องใช้

## Installer Requirements

`INSTALL_DESKTOP.ps1` ต้อง:

```text
resolve Desktop path
ติดตั้งหรืออัปเดต Workspace อย่างปลอดภัย
ไม่ลบ Workspace เดิมอัตโนมัติ
สร้าง data, outputs, logs, config และ cache
สร้าง Shortcut โดยไม่ขอ Admin
เรียก scripts/detect_gis.ps1
สร้าง config/local_runtime.json
```

## Acceptance

ก่อนรายงานสำเร็จต้องตรวจ:

```text
รันด้วย Standard User
ไม่มี UAC Prompt
Workspace อยู่บน Desktop ที่ Resolve ได้จริง
Runtime Folder เขียนได้
ไม่ได้แก้ Program Files
ไม่ได้แก้ Machine PATH
ไม่ได้สร้าง Service หรือ Driver
START_GISPLAN.bat เปิดได้
local_runtime.json ถูกสร้าง
Tool ที่ขาดถูกแจ้งตรงไปตรงมา
```

หาก PowerShell, Script Execution, MSI หรือ Application Execution ถูกบล็อกโดยนโยบายองค์กร ให้รายงาน `policy_constraint` และหยุด ห้ามพยายามหลบ Policy
