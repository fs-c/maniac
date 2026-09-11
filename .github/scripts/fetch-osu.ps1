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

function Find-OsuGameDll([string]$root) {
    Get-ChildItem $root -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq "osu.Game.dll" } |
        Select-Object -First 1
}

$game = Find-OsuGameDll $extract
if (-not $game) {
    # Velopack Linux full nupkgs wrap a single AppImage instead of loose assemblies.
    $appImage = Get-ChildItem $extract -Recurse -File |
        Where-Object { $_.Name -like "*.AppImage" } |
        Select-Object -First 1
    if (-not $appImage) {
        $names = @(Get-ChildItem $extract -Recurse -File | Select-Object -First 20 -ExpandProperty Name)
        throw "osu.Game.dll not found in nupkg. Entries: $($names -join ', ')"
    }

    Write-Host "Extracting $($appImage.Name)"
    & chmod +x $appImage.FullName
    if ($LASTEXITCODE -ne 0) { throw "chmod +x failed for $($appImage.FullName)" }

    Push-Location $extract
    try {
        & $appImage.FullName --appimage-extract
        if ($LASTEXITCODE -ne 0) { throw "AppImage extract failed with $LASTEXITCODE" }
    } finally {
        Pop-Location
    }

    $game = Find-OsuGameDll $extract
}
if (-not $game) { throw "osu.Game.dll not found in nupkg" }

Write-Host "LazerDir $($game.DirectoryName)"
if (-not $env:GITHUB_ENV) { throw "GITHUB_ENV is empty." }
"LAZER_DIR=$($game.DirectoryName)" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8
