# CI only (.github/workflows/ci.yml). GitHub-hosted runners include pwsh.
param(
    [string]$Configuration = "Release",
    [string]$BuildExpiry = $env:MANIAC_BUILD_EXPIRY
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")
$root = Split-Path (Split-Path $PSScriptRoot)

$project = Join-Path $root "src/Maniac.Stable/Maniac.Stable.csproj"
$publish = @(
    "publish", $project,
    "-c", $Configuration,
    "-p:SelfContained=true",
    "-p:PublishSingleFile=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "--nologo"
)
if ($BuildExpiry) { $publish += "-p:ManiacBuildExpiry=$BuildExpiry" }

dotnet @publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish stable failed with $LASTEXITCODE" }

$publishDirs = @(Get-ChildItem -Path (Join-Path $root "src/Maniac.Stable/bin/$Configuration") -Recurse -Directory -Filter "publish")
if ($publishDirs.Count -ne 1) {
    throw "Expected one publish directory, found $($publishDirs.Count)."
}
$publishDir = $publishDirs[0].FullName

$built = Join-Path $publishDir "maniac-stable.exe"
if (-not (Test-Path $built)) { throw "Published executable not found at $built" }

$publishName = Get-StablePublishName
$published = Join-Path $publishDir $publishName
if ($published -ne $built) {
    Move-Item $built $published -Force
}

$extra = @(Get-ChildItem $publishDir -Recurse -File | Where-Object {
    $_.FullName -ne $published -and $_.Extension -ne ".pdb"
})
if ($extra.Count -gt 0) {
    $names = ($extra | Select-Object -First 12 | ForEach-Object { $_.FullName.Substring($publishDir.Length + 1) }) -join ", "
    throw "Single-file publish left files next to the executable: $names"
}
Get-ChildItem $publishDir -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
    if (-not (Get-ChildItem $_.FullName -Force | Select-Object -First 1)) {
        Remove-Item $_.FullName -Force
    }
}

if ($env:GITHUB_OUTPUT) {
    [System.IO.File]::AppendAllText($env:GITHUB_OUTPUT, "artifact=$published$([Environment]::NewLine)")
}
Write-Host "Published $published"
