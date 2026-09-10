param(
    [string]$Configuration = "Release",
    [string]$LazerDir = $env:LAZER_DIR,
    [string]$BuildExpiry = $env:MANIAC_BUILD_EXPIRY
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dest = Join-Path $root "artifacts\maniac"
$project = Join-Path $root "src\Maniac.Launcher\Maniac.Launcher.csproj"

if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }

$publish = @(
    "publish", $project,
    "-c", $Configuration,
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-o", $dest,
    "--nologo"
)
if ($LazerDir) { $publish += "-p:LazerDir=$LazerDir" }
if ($BuildExpiry) { $publish += "-p:ManiacBuildExpiry=$BuildExpiry" }

dotnet @publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with $LASTEXITCODE" }

$exe = Join-Path $dest "maniac.exe"
if (-not (Test-Path $exe)) { throw "Published launcher not found at $exe" }

Get-ChildItem $dest -Force | Where-Object { $_.Name -ne "maniac.exe" } | Remove-Item -Force -Recurse

Write-Host "Packed $exe"
