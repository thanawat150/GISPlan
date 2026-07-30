@echo off
setlocal
cd /d "%~dp0"

echo ========================================
echo GISPlan - Build Portable and Copy Desktop
echo ========================================

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] ไม่พบ .NET 8 SDK สำหรับ Build
  echo ใช้ GitHub Actions เพื่อสร้าง EXE ได้โดยไม่ต้อง Build ที่เครื่องนี้
  pause
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build_portable.ps1"
if errorlevel 1 (
  echo [ERROR] Build ไม่สำเร็จ
  pause
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\copy_to_desktop.ps1"
if errorlevel 1 (
  echo [ERROR] คัดลอกไป Desktop ไม่สำเร็จ
  pause
  exit /b 1
)

echo [DONE] GISPlan พร้อมใช้งานบน Desktop
pause
