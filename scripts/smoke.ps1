# Verify compile + startup probe against the installed osu!lazer.
# Full in-game smoke still requires a running osu!lazer instance and `maniac.exe`.
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$candidates = @(
    (Join-Path $env:LOCALAPPDATA "osulazer\current"),
    (Join-Path $env:LOCALAPPDATA "osu!lazer\current")
)
$lazer = $candidates | Where-Object { Test-Path (Join-Path $_ "osu.Game.dll") } | Select-Object -First 1
if (-not $lazer) { throw "osu!lazer install not found." }

Write-Host "LazerDir $lazer"
$env:LAZER_DIR = $lazer
dotnet test tests/Maniac.Core.Tests/Maniac.Core.Tests.csproj -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test tests/Maniac.Compatibility.Tests/Maniac.Compatibility.Tests.csproj -c $Configuration -p:LazerDir=$lazer --nologo
exit $LASTEXITCODE
