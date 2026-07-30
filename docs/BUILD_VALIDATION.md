# Build Validation

ไฟล์นี้ใช้บันทึกการตรวจ Build ของ GISPlan บน GitHub Actions Windows runner

Acceptance:

- Core project compiles
- Desktop WinForms project compiles
- Smoke tests pass
- Self-contained single-file `GISPlan.exe` is produced
- Application manifest requests `asInvoker`
- No-admin static checks pass
- Build artifact is uploaded

ผลการตรวจต้องอ้างอิง Workflow run และ Commit ที่ทดสอบจริง ห้ามตั้งสถานะ `approved` โดย AI
