# GISPlan Agent Rules

## Read Order

อ่านตามลำดับเท่านั้น:

1. `AGENTS.md`
2. `skills/gis-specialist/SKILL.md`
3. `skills/gis-specialist/DESKTOP_NO_ADMIN.md` เฉพาะงานติดตั้งหรือเตรียม Environment
4. Job Config ปัจจุบัน
5. Script หรือ Error ที่เกี่ยวข้องเท่านั้น

ห้ามสแกน Repository ทั้งหมดโดยไม่มีเหตุผล

## No-admin Desktop Requirement

GISPlan ต้องทำงานจาก:

```text
<Resolved Desktop>\GISPlan
```

กฎบังคับ:

- ห้ามขอ Administrator
- ห้ามเขียน `Program Files`, `Windows`, `System32`
- ห้ามแก้ Machine-wide PATH หรือ Registry ระดับเครื่อง
- ห้ามติดตั้ง Service หรือ Driver
- ใช้ `%LOCALAPPDATA%\GISPlan` สำหรับ Runtime, Cache และ Logs
- ตรวจเครื่องมือ GIS ที่มีอยู่ก่อน ห้ามติดตั้ง System-wide เอง
- เมื่อ Tool ขาด ให้รายงานตรงไปตรงมา

## GIS Workflow

ทุกงานต้อง:

```text
Preflight
→ CRS/Geometry/Metadata check
→ Process
→ QA/QC
→ Versioned output
→ Human review when required
```

ห้ามเดา CRS ห้ามแก้ Source Data และห้ามตั้งสถานะ `approved`

## Output Status

ใช้ได้เฉพาะ:

```text
draft
passed
passed_with_warnings
rework
needs_human_review
policy_constraint
missing_runtime
```
