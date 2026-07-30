# Resume GIS Jobs

GISPlan เก็บ Job แต่ละงานไว้ใน:

```text
%LOCALAPPDATA%\GISPlan\jobs\<job-id>\
```

ไฟล์สำคัญ:

```text
gis_job.json
preflight_report.json
run.log
qa_report.json
run_manifest.json
```

หน้าเริ่มต้นของ `GISPlan.exe` มีสองทางเลือก:

```text
สร้างงาน GIS ใหม่
ทำงานต่อจาก Job เดิม
```

เมื่อเลือก Resume:

1. เลือกไฟล์ `gis_job.json`
2. ระบบตรวจ Runtime ใหม่
3. หาก Output เดิมมีอยู่ ระบบทำ QA ก่อน
4. หาก Output ผ่าน QA แล้ว จะไม่ประมวลผลซ้ำ
5. หาก Output ไม่มีหรือไม่ผ่าน QA จะสร้าง Job Resume ใหม่
6. ไม่เขียนทับ Output เดิม แต่สร้างชื่อ Version ใหม่
7. สามารถกดยกเลิก Process ภายนอกได้

Resume รุ่นแรกทำงานในระดับ Job ไม่ได้ Resume ภายในขั้นตอนย่อยของ QGIS, ArcGIS หรือ GDAL ที่ถูกยกเลิกกลางคำสั่ง เนื่องจากเครื่องมือเหล่านั้นอาจสร้างไฟล์ชั่วคราวไม่ครบ ระบบจึงตรวจ QA และเริ่ม Operation ใหม่แบบ Versioned เพื่อความปลอดภัย
