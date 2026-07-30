param(
    [string]$OutputPath = "./dist/external_source_health.json"
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null

$checks = @(
    [pscustomobject]@{
        id = "copernicus-stac"
        url = "https://stac.dataspace.copernicus.eu/v1/collections"
        required = $true
        marker = '"collections"'
    },
    [pscustomobject]@{
        id = "nasa-earthdata"
        url = "https://cmr.earthdata.nasa.gov/search/collections.umm_json?keyword=SRTM&page_size=1"
        required = $true
        marker = '"items"'
    },
    [pscustomobject]@{
        id = "thai-government-data"
        url = "https://www.data.go.th/api/3/action/package_search?q=DEM&rows=1"
        required = $false
        marker = '"success"'
    },
    [pscustomobject]@{
        id = "gistda-open-data"
        url = "https://opendata.gistda.or.th/api/3/action/package_search?q=DEM&rows=1"
        required = $false
        marker = '"success"'
    }
)

$results = @()
$requiredFailures = 0

foreach ($check in $checks) {
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $status = $null
    $ok = $false
    $message = ""
    try {
        $response = Invoke-WebRequest `
            -Uri $check.url `
            -Method Get `
            -UseBasicParsing `
            -TimeoutSec 35 `
            -Headers @{ "User-Agent" = "GISPlan-LiveCheck/0.3" }
        $stopwatch.Stop()
        $status = [int]$response.StatusCode
        $body = [string]$response.Content
        $ok = $status -ge 200 -and $status -lt 300 -and $body.Contains($check.marker)
        $message = if ($ok) { "reachable and response marker found" } else { "unexpected response body" }
    }
    catch {
        $stopwatch.Stop()
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $status = [int]$_.Exception.Response.StatusCode
        }
        $message = $_.Exception.Message
    }

    if (-not $ok -and $check.required) {
        $requiredFailures++
    }

    $results += [pscustomobject]@{
        id = $check.id
        url = $check.url
        required = $check.required
        success = $ok
        httpStatus = $status
        elapsedMilliseconds = $stopwatch.ElapsedMilliseconds
        message = $message
        checkedAt = [DateTimeOffset]::UtcNow.ToString("o")
    }

    $prefix = if ($ok) { "PASS" } elseif ($check.required) { "FAIL" } else { "WARN" }
    Write-Host "$prefix $($check.id): HTTP $status in $($stopwatch.ElapsedMilliseconds) ms - $message"
}

$report = [pscustomobject]@{
    generatedAt = [DateTimeOffset]::UtcNow.ToString("o")
    requiredFailures = $requiredFailures
    results = $results
}
$report | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputPath -Encoding UTF8

if ($requiredFailures -gt 0) {
    throw "$requiredFailures required external provider check(s) failed. See $OutputPath"
}
