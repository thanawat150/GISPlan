[CmdletBinding()]
param(
    [string]$ExePath = ''
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) {
    $ExePath = Join-Path $repo 'dist\GISPlan-win-x64\GISPlan.exe'
}

$checks = @()
function Add-Check([string]$Name, [bool]$Passed, [string]$Detail) {
    $script:checks += [pscustomobject]@{ check = $Name; passed = $Passed; detail = $Detail }
}

Add-Check 'exe_exists' (Test-Path $ExePath) $ExePath

$manifestPath = Join-Path $repo 'src\GISPlan.Desktop\app.manifest'
$manifest = if (Test-Path $manifestPath) { Get-Content $manifestPath -Raw } else { '' }
Add-Check 'asInvoker_manifest' ($manifest -match 'requestedExecutionLevel level="asInvoker"') $manifestPath
Add-Check 'no_requireAdministrator' ($manifest -notmatch 'requireAdministrator') $manifestPath

$projectPath = Join-Path $repo 'src\GISPlan.Desktop\GISPlan.Desktop.csproj'
$project = if (Test-Path $projectPath) { Get-Content $projectPath -Raw } else { '' }
Add-Check 'self_contained' ($project -match '<SelfContained>true</SelfContained>') $projectPath
Add-Check 'single_file' ($project -match '<PublishSingleFile>true</PublishSingleFile>') $projectPath

$runtime = Join-Path $env:LOCALAPPDATA 'GISPlan'
try {
    New-Item -ItemType Directory -Path $runtime -Force | Out-Null
    $probe = Join-Path $runtime '.write-test'
    Set-Content $probe 'ok'
    Remove-Item $probe -Force
    Add-Check 'localappdata_writable' $true $runtime
} catch {
    Add-Check 'localappdata_writable' $false $_.Exception.Message
}

$programFiles = [Environment]::GetFolderPath('ProgramFiles')
Add-Check 'runtime_not_program_files' (-not $runtime.StartsWith($programFiles, [System.StringComparison]::OrdinalIgnoreCase)) $runtime

$checks | Format-Table -AutoSize
$failed = @($checks | Where-Object { -not $_.passed })
$reportPath = Join-Path $repo 'dist\no_admin_test.json'
New-Item -ItemType Directory -Path (Split-Path $reportPath) -Force | Out-Null
$checks | ConvertTo-Json -Depth 4 | Set-Content $reportPath -Encoding UTF8

if ($failed.Count -gt 0) {
    throw "No-admin acceptance failed: $($failed.Count) check(s)"
}
Write-Host "No-admin static acceptance passed. Report: $reportPath"
