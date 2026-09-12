# POC: run a framework-dependent app on osu!lazer's bundled .NET runtime.
param(
    [string]$OsuDir = "$env:LOCALAPPDATA\osulazer\current"
)

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$out = Join-Path $here "out"
$osuCorelib = Join-Path $OsuDir "System.Private.CoreLib.dll"
if (-not (Test-Path $osuCorelib)) { throw "osu runtime not found at $OsuDir" }
$osuFileVer = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($osuCorelib).FileVersion
$osuRuntimeVer = (Get-Content (Join-Path $OsuDir "osu!.runtimeconfig.json") -Raw | ConvertFrom-Json).runtimeOptions.includedFrameworks[0].version

Write-Host "osu dir     $OsuDir"
Write-Host "osu fx      Microsoft.NETCore.App $osuRuntimeVer"
Write-Host "osu corelib $osuFileVer"
Write-Host ""

function Reset-Out {
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    New-Item -ItemType Directory -Path $out | Out-Null
}

function Invoke-Logged($name, [scriptblock]$body) {
    Write-Host "======== $name ========"
    $trace = Join-Path $out "$name.trace"
    $env:COREHOST_TRACE = "1"
    $env:COREHOST_TRACEFILE = $trace
    $env:DOTNET_MULTILEVEL_LOOKUP = "0"
    try {
        & $body
    } catch {
        Write-Host "EXCEPTION: $($_.Exception.Message)"
    } finally {
        Remove-Item Env:COREHOST_TRACE -ErrorAction SilentlyContinue
        Remove-Item Env:COREHOST_TRACEFILE -ErrorAction SilentlyContinue
        Remove-Item Env:DOTNET_MULTILEVEL_LOOKUP -ErrorAction SilentlyContinue
        Remove-Item Env:DOTNET_ROOT -ErrorAction SilentlyContinue
        Remove-Item Env:DOTNET_ROOT_X64 -ErrorAction SilentlyContinue
    }
    Write-Host ""
}

function Show-ProbeOutput($stdout) {
    foreach ($line in $stdout) { Write-Host "  $line" }
    $filever = ($stdout | Where-Object { $_ -like "filever   *" }) -replace '^filever   ', ''
    $corelib = ($stdout | Where-Object { $_ -like "corelib   *" }) -replace '^corelib   ', ''
    if ($filever -eq $osuFileVer) {
        Write-Host "  RESULT: HIT osu runtime (filever $filever)"
    } elseif ($corelib -match 'osulazer') {
        Write-Host "  RESULT: HIT osu path ($corelib)"
    } elseif ($filever) {
        Write-Host "  RESULT: MISS (filever $filever, expected $osuFileVer)"
    } else {
        Write-Host "  RESULT: NO PROBE OUTPUT"
    }
}

Reset-Out

Write-Host "=== publish probe FDD ==="
$fdd = Join-Path $out "probe-fdd"
dotnet publish (Join-Path $here "Probe\Probe.csproj") -c Release -r win-x64 --self-contained false -o $fdd --nologo
if ($LASTEXITCODE -ne 0) { throw "probe FDD publish failed" }
$probeDll = Join-Path $fdd "probe.dll"
$probeExe = Join-Path $fdd "probe.exe"

Write-Host "=== make Microsoft.NETCore.App.deps.json from osu!.deps.json ==="
$fxDeps = Join-Path $out "Microsoft.NETCore.App.deps.json"
& $probeExe --make-fx-deps (Join-Path $OsuDir "osu!.deps.json") $fxDeps
if ($LASTEXITCODE -ne 0) { throw "make-fx-deps failed" }

function New-FakeDotnetRoot([bool]$IncludeFxDeps) {
    $root = Join-Path $out ("dotnet-root-" + $(if ($IncludeFxDeps) { "deps" } else { "nodeps" }))
    if (Test-Path $root) { Remove-Item $root -Recurse -Force }
    $fxrDir = Join-Path $root "host\fxr\$osuRuntimeVer"
    $fxDir = Join-Path $root "shared\Microsoft.NETCore.App\$osuRuntimeVer"
    New-Item -ItemType Directory -Path $fxrDir -Force | Out-Null
    New-Item -ItemType Directory -Path $fxDir -Force | Out-Null
    New-Item -ItemType HardLink -Path (Join-Path $fxrDir "hostfxr.dll") -Target (Join-Path $OsuDir "hostfxr.dll") | Out-Null
    Get-ChildItem $OsuDir -File | ForEach-Object {
        $dest = Join-Path $fxDir $_.Name
        New-Item -ItemType HardLink -Path $dest -Target $_.FullName | Out-Null
    }
    if ($IncludeFxDeps) {
        Copy-Item $fxDeps (Join-Path $fxDir "Microsoft.NETCore.App.deps.json") -Force
        Set-Content -Path (Join-Path $fxDir "Microsoft.NETCore.App.runtimeconfig.json") -Value '{"runtimeOptions":{"tfm":"net8.0"}}'
    }
    return $root
}

Invoke-Logged "A-apphost-machine-dotnet" {
    Write-Host "control: FDD apphost with no DOTNET_ROOT (uses Program Files if present)"
    $p = Start-Process -FilePath $probeExe -Wait -PassThru -NoNewWindow -RedirectStandardOutput (Join-Path $out "A.stdout") -RedirectStandardError (Join-Path $out "A.stderr")
    Write-Host "exit $($p.ExitCode)"
    Show-ProbeOutput (Get-Content (Join-Path $out "A.stdout") -ErrorAction SilentlyContinue)
    Get-Content (Join-Path $out "A.stderr") -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  stderr $_" }
}

$rootNoDeps = New-FakeDotnetRoot $false
Invoke-Logged "B-apphost-fake-root-no-depsjson" {
    $env:DOTNET_ROOT = $rootNoDeps
    $env:DOTNET_ROOT_X64 = $rootNoDeps
    $p = Start-Process -FilePath $probeExe -Wait -PassThru -NoNewWindow -RedirectStandardOutput (Join-Path $out "B.stdout") -RedirectStandardError (Join-Path $out "B.stderr")
    Write-Host "exit $($p.ExitCode) DOTNET_ROOT=$rootNoDeps"
    Show-ProbeOutput (Get-Content (Join-Path $out "B.stdout") -ErrorAction SilentlyContinue)
    Get-Content (Join-Path $out "B.stderr") -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  stderr $_" }
}

$rootDeps = New-FakeDotnetRoot $true
Invoke-Logged "C-apphost-fake-root-with-depsjson" {
    $env:DOTNET_ROOT = $rootDeps
    $env:DOTNET_ROOT_X64 = $rootDeps
    $p = Start-Process -FilePath $probeExe -Wait -PassThru -NoNewWindow -RedirectStandardOutput (Join-Path $out "C.stdout") -RedirectStandardError (Join-Path $out "C.stderr")
    Write-Host "exit $($p.ExitCode) DOTNET_ROOT=$rootDeps"
    Show-ProbeOutput (Get-Content (Join-Path $out "C.stdout") -ErrorAction SilentlyContinue)
    Get-Content (Join-Path $out "C.stderr") -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  stderr $_" }
}

Write-Host "=== compile native-host ==="
$cc = $null
foreach ($candidate in @("clang", "gcc")) {
    $cmd = Get-Command $candidate -ErrorAction SilentlyContinue
    if ($cmd) { $cc = $cmd.Source; break }
}
if (-not $cc) { throw "no gcc/clang" }
$nativeExe = Join-Path $out "native-host.exe"
& $cc -O2 -municode -o $nativeExe (Join-Path $here "native-host.c")
if ($LASTEXITCODE -ne 0) { throw "native-host compile failed" }
Write-Host "compiled $nativeExe with $cc ($((Get-Item $nativeExe).Length) bytes)"
Write-Host ""

Invoke-Logged "D-native-host-fake-root" {
    $p = Start-Process -FilePath $nativeExe -ArgumentList @($rootDeps, $probeDll) -Wait -PassThru -NoNewWindow -RedirectStandardOutput (Join-Path $out "D.stdout") -RedirectStandardError (Join-Path $out "D.stderr")
    Write-Host "exit $($p.ExitCode)"
    Show-ProbeOutput (Get-Content (Join-Path $out "D.stdout") -ErrorAction SilentlyContinue)
    Get-Content (Join-Path $out "D.stderr") -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  stderr $_" }
}

Invoke-Logged "E-native-host-osu-dir-as-root" {
    Write-Host "negative: osu install is SCD-flat, not a DOTNET_ROOT layout"
    $p = Start-Process -FilePath $nativeExe -ArgumentList @($OsuDir, $probeDll) -Wait -PassThru -NoNewWindow -RedirectStandardOutput (Join-Path $out "E.stdout") -RedirectStandardError (Join-Path $out "E.stderr")
    Write-Host "exit $($p.ExitCode)"
    Show-ProbeOutput (Get-Content (Join-Path $out "E.stdout") -ErrorAction SilentlyContinue)
    Get-Content (Join-Path $out "E.stderr") -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  stderr $_" }
}

Write-Host "done. traces under $out"
