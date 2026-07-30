# GISPlan Guided and Multilingual UX

## Goal

GISPlan ต้องใช้งานได้โดยผู้ที่ไม่จำชื่อเครื่องมือ GIS และไม่คุ้นกับคำศัพท์เทคนิค

ผู้ใช้เริ่มจากพิมพ์สิ่งที่ต้องการ เช่น:

```text
คำนวณพื้นที่ทุกแปลง
ทำ Buffer 100 เมตร
สร้างจุดจาก Excel
ปรับสีขอบเขต
ทำแผนที่ PDF
ต่อภาพโดรน
```

ระบบตอบได้สองแบบ:

1. **สอนว่าไปตรงไหน** — แสดงเส้นทางเมนูและขั้นตอนที่ควรใช้
2. **เตรียมงานให้อัตโนมัติ** — เปิดหน้าประมวลผลและเลือก Operation ให้ เมื่อรุ่นปัจจุบันรองรับ

ระบบห้ามอ้างว่าทำอัตโนมัติได้ หาก Workflow ยังไม่ถูกพัฒนา

## Simple Mode

ค่าเริ่มต้นคือ `โหมดพาไปทีละขั้น`

- ใช้ชื่อภาษาคนแทนชื่อ Enum หรือ Algorithm
- แสดงคำอธิบายของงานที่เลือก
- แสดง Warning สำคัญใกล้กับช่องที่ต้องกรอก
- ซ่อน Log ทางเทคนิคจนกว่าผู้ใช้จะกดดู
- เลือก Processing Tool เป็น Auto โดยค่าเริ่มต้น
- ตรวจข้อมูลก่อนประมวลผล
- ไม่เขียนทับ Source Data

Advanced Mode สามารถเปิดเครื่องมือและรายละเอียดเพิ่มเติมได้ แต่ต้องใช้ Core Workflow และ Safety Guard ชุดเดียวกัน

## Language Support

รุ่นเริ่มต้นมีข้อความในตัวโปรแกรม:

```text
th-TH — ไทย
en-US — English
```

ผู้ใช้เพิ่มภาษาอื่นได้โดยวางไฟล์ JSON ที่:

```text
%LOCALAPPDATA%\GISPlan\settings\languages\<culture-code>.json
```

ตัวอย่าง:

```text
ja-JP.json
ms-MY.json
zh-CN.json
```

Language Pack เป็น Dictionary แบบ key/value และแก้เฉพาะข้อความที่ต้องการได้ โดยข้อความที่ไม่มีคำแปลจะใช้ English เป็น Fallback

```json
{
  "app.subtitle": "ข้อความแปล",
  "assistant.guide": "ข้อความแปล",
  "new_job": "ข้อความแปล"
}
```

กฎ:

- รหัสภายใน เช่น `BufferVector` ห้ามแปลหรือเปลี่ยน
- แปลเฉพาะ Display Name และข้อความ UI
- Language Pack ที่เสียหายต้องไม่ทำให้โปรแกรมเปิดไม่ได้
- การเปลี่ยนภาษาต้องไม่เปลี่ยนข้อมูล Job หรือผลการประมวลผล

## Guided Assistant Safety

Assistant รุ่นแรกทำงาน Offline ด้วย Keyword และ Alias Catalog

```text
คำขอ
→ จับคู่ Workflow
→ แสดงว่าต้องไปตรงไหน
→ ระบุว่าทำอัตโนมัติได้หรือยัง
→ เปิดหน้าที่เกี่ยวข้องเมื่อรองรับ
```

ต้องขอยืนยันก่อน:

- เริ่มประมวลผล
- เขียน Output
- ซ่อม Geometry
- เปลี่ยน CRS
- ดาวน์โหลดข้อมูล
- ส่งข้อมูลออกจากเครื่อง

## Buffer Safety

งาน Buffer ที่ระบุหน่วยเป็นเมตรต้อง:

```text
รับ Working CRS แบบ Projected
→ Block EPSG:4326 และ CRS แบบองศา
→ Reproject Input เป็นไฟล์ชั่วคราว
→ Buffer ด้วยระยะเมตร
→ QA Output
→ ลบไฟล์ชั่วคราว
```

ห้ามส่งค่า `100` ไปยัง QGIS `native:buffer` บน Layer ที่ยังเป็นพิกัดองศาโดยตรง

## Review Status

ระบบนี้ใช้สถานะ:

```text
draft
passed
passed_with_warnings
rework
needs_human_review
```

AI ห้ามตั้งสถานะ `approved`
