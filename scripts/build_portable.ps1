[CmdletBinding()]
param(
    [ValidateSet('win-x64','win-arm64')]
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'src\GISPlan.Desktop\GISPlan.Desktop.csproj'
$out = Join-Path $repo "dist\GISPlan-$Runtime"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'ไม่พบ .NET SDK สำหรับ Build โปรแกรม'
}

if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}
New-Item -ItemType Directory -Path $out -Force | Out-Null

Write-Host '[1/3] Running smoke tests...'
dotnet run --project (Join-Path $repo 'tests\GISPlan.SmokeTests\GISPlan.SmokeTests.csproj') --configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Smoke tests failed' }

Write-Host '[2/3] Publishing self-contained single-file EXE...'
dotnet publish $project `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:DebugType=embedded `
    --output $out
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Copy-Item (Join-Path $repo 'README_TH.md') (Join-Path $out 'README_TH.md') -ErrorAction SilentlyContinue

$exe = Join-Path $out 'GISPlan.exe'
if (-not (Test-Path $exe)) {
    throw "ไม่พบไฟล์ Build: $exe"
}

Write-Host '[3/3] Portable build completed'
Write-Host $exe
