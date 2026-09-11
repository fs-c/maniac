# CI only (.github/workflows/ci.yml). GitHub-hosted runners include pwsh.
param(
    [string]$Configuration = "Release",
    [Parameter(Mandatory = $true)]
    [string[]]$RuntimeIdentifier,
    [string]$LazerDir = $env:LAZER_DIR,
    [string]$BuildExpiry = $env:MANIAC_BUILD_EXPIRY
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")
$root = Split-Path (Split-Path $PSScriptRoot)

foreach ($rid in $RuntimeIdentifier) {
    $binary = Get-PackedBinaryName $rid
    $dest = Join-Path (Join-Path $root "artifacts") "maniac-$rid"

    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }

    $publish = @(
        "publish", (Join-Path $root "src/Maniac.Launcher/Maniac.Launcher.csproj"),
        "-c", $Configuration,
        "-r", $rid,
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-o", $dest,
        "--nologo"
    )
    if ($LazerDir) { $publish += "-p:LazerDir=$LazerDir" }
    if ($BuildExpiry) { $publish += "-p:ManiacBuildExpiry=$BuildExpiry" }

    dotnet @publish
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with $LASTEXITCODE" }

    $exe = Join-Path $dest $binary
    if (-not (Test-Path $exe)) { throw "Published launcher not found at $exe" }

    Get-ChildItem $dest -Force | Where-Object { $_.Name -ne $binary } | Remove-Item -Force -Recurse
    if ($binary -ne "maniac.exe") {
        & chmod +x $exe
        if ($LASTEXITCODE -ne 0) { throw "chmod +x failed for $exe" }
    }

    Write-Host "Packed $exe"
}
