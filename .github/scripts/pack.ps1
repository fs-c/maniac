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

function Get-HostRid {
    $os = if ($IsWindows) { "win" } elseif ($IsMacOS) { "osx" } elseif ($IsLinux) { "linux" } else {
        throw "Unknown OS; pack on Windows, Linux, or macOS."
    }
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    return "$os-$arch"
}

foreach ($rid in $RuntimeIdentifier) {
    $hostRid = Get-HostRid
    if ($rid -ne $hostRid) {
        throw "Pack RID '$rid' does not match this runner ($hostRid). Native launcher is packed on the matching OS."
    }

    $binary = Get-PackedBinaryName $rid
    $dest = Join-Path (Join-Path $root "artifacts") "maniac-$rid"
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    New-Item -ItemType Directory -Force $dest | Out-Null

    $project = Join-Path $root "src/Maniac.Launcher/Maniac.Launcher.csproj"
    $build = @(
        "build", $project,
        "-c", $Configuration,
        "-p:ManiacPack=true",
        "--nologo"
    )
    if ($LazerDir) { $build += "-p:LazerDir=$LazerDir" }
    if ($BuildExpiry) { $build += "-p:ManiacBuildExpiry=$BuildExpiry" }

    dotnet @build
    if ($LASTEXITCODE -ne 0) { throw "dotnet build launcher failed with $LASTEXITCODE" }

    $built = Join-Path $root "src/Maniac.Launcher/bin/$Configuration/net8.0/$binary"
    if (-not (Test-Path $built)) { throw "Packed launcher not found at $built" }
    Copy-Item $built (Join-Path $dest $binary) -Force
    if ($binary -ne "maniac.exe") {
        & chmod +x (Join-Path $dest $binary)
        if ($LASTEXITCODE -ne 0) { throw "chmod +x failed for $binary" }
    }

    Write-Host "Packed $(Join-Path $dest $binary)"
}
