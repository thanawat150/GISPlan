[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$runtimeRoot = Join-Path $env:LOCALAPPDATA 'GISPlan\settings'
$configPath = Join-Path $runtimeRoot 'local_runtime.json'
New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null

function Find-FirstExecutable {
    param([string[]]$Names, [string[]]$Candidates = @())

    foreach ($name in $Names) {
        $command = Get-Command $name -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($command) { return $command.Source }
    }
    foreach ($candidate in $Candidates) {
        if ($candidate -and (Test-Path $candidate)) { return (Resolve-Path $candidate).Path }
    }
    return $null
}

$qgisRoots = @()
foreach ($root in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, (Join-Path $env:LOCALAPPDATA 'Programs'))) {
    if ($root -and (Test-Path $root)) {
        $qgisRoots += Get-ChildItem $root -Directory -Filter 'QGIS*' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
    }
}

$qgisCandidates = foreach ($root in $qgisRoots) {
    Join-Path $root 'bin\qgis_process.exe'
    Join-Path $root 'bin\qgis_process-qgis.bat'
    Join-Path $root 'bin\qgis_process-qgis-ltr.bat'
}
$ogrInfoCandidates = foreach ($root in $qgisRoots) { Join-Path $root 'bin\ogrinfo.exe' }
$ogr2OgrCandidates = foreach ($root in $qgisRoots) { Join-Path $root 'bin\ogr2ogr.exe' }
$gdalSrsCandidates = foreach ($root in $qgisRoots) { Join-Path $root 'bin\gdalsrsinfo.exe' }

$result = [ordered]@{
    detectedAt = (Get-Date).ToString('o')
    qgisProcessPath = Find-FirstExecutable @('qgis_process.exe','qgis_process-qgis.bat','qgis_process-qgis-ltr.bat') $qgisCandidates
    ogrInfoPath = Find-FirstExecutable @('ogrinfo.exe') $ogrInfoCandidates
    ogr2OgrPath = Find-FirstExecutable @('ogr2ogr.exe') $ogr2OgrCandidates
    gdalSrsInfoPath = Find-FirstExecutable @('gdalsrsinfo.exe') $gdalSrsCandidates
    arcGisPropyPath = Find-FirstExecutable @('propy.bat') @((Join-Path $env:ProgramFiles 'ArcGIS\Pro\bin\Python\Scripts\propy.bat'))
    pythonPath = Find-FirstExecutable @('python.exe','py.exe')
}

$result | ConvertTo-Json -Depth 4 | Set-Content $configPath -Encoding UTF8
$result | Format-List
Write-Host "Saved: $configPath"
