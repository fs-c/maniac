param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "src\Maniac.Launcher\bin\$Configuration\net8.0-windows"
$dest = Join-Path $root "artifacts\maniac"
if (-not (Test-Path (Join-Path $src "maniac.exe"))) {
    throw "Launcher output not found at $src. Build Maniac.Launcher first."
}

if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item (Join-Path $src "*") $dest -Recurse
Write-Host "Packed $dest"
