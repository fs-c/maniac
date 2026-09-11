# CI only (.github/workflows/ci.yml). GitHub-hosted runners include pwsh.
param(
    [string]$Url = $env:OSU_NUPKG_URL,
    [string]$RuntimeIdentifier = $env:RID
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Url)) { throw "OSU_NUPKG_URL is empty." }
if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) { throw "RID is empty." }

$temp = $env:RUNNER_TEMP
if ([string]::IsNullOrWhiteSpace($temp)) { throw "RUNNER_TEMP is empty." }

$cache = Join-Path $temp "osu-cache"
$extract = Join-Path $temp "osu-nupkg"
$nupkg = Join-Path $cache "osu-full-$RuntimeIdentifier.nupkg"
New-Item -ItemType Directory -Force $cache | Out-Null

if (-not (Test-Path $nupkg)) {
    Write-Host "Downloading $Url"
    $curl = if (Get-Command curl.exe -ErrorAction SilentlyContinue) { "curl.exe" } else { "curl" }
    & $curl -L --fail --retry 3 -o $nupkg $Url
    if ($LASTEXITCODE -ne 0) { throw "nupkg download failed with $LASTEXITCODE" }
}

if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
New-Item -ItemType Directory -Force $extract | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($nupkg, $extract)

$game = Get-ChildItem $extract -Recurse -Filter osu.Game.dll | Select-Object -First 1
if (-not $game) { throw "osu.Game.dll not found in nupkg" }

Write-Host "LazerDir $($game.DirectoryName)"
if (-not $env:GITHUB_ENV) { throw "GITHUB_ENV is empty." }
"LAZER_DIR=$($game.DirectoryName)" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8
