[CmdletBinding()]
param(
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repo "dist\GISPlan-$Runtime\GISPlan.exe"
$desktop = [Environment]::GetFolderPath('Desktop')
$targetFolder = Join-Path $desktop 'GISPlan'
$target = Join-Path $targetFolder 'GISPlan.exe'

if (-not (Test-Path $source)) {
    throw "ไม่พบ GISPlan.exe กรุณารัน scripts\build_portable.ps1 ก่อน: $source"
}

New-Item -ItemType Directory -Path $targetFolder -Force | Out-Null
Copy-Item $source $target -Force

$shortcutPath = Join-Path $desktop 'GISPlan.lnk'
try {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $target
    $shortcut.WorkingDirectory = $targetFolder
    $shortcut.Description = 'GISPlan Portable GIS Workspace'
    $shortcut.Save()
} catch {
    Write-Warning "สร้าง Shortcut ไม่สำเร็จ แต่ GISPlan.exe ถูกคัดลอกแล้ว: $($_.Exception.Message)"
}

Write-Host 'ติดตั้งแบบไม่ใช้ Admin เรียบร้อย'
Write-Host $target
