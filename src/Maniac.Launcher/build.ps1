param(
    [string]$Configuration = "Release",
    [Parameter(Mandatory = $true)]
    [string]$OutputDir,
    [string]$PluginDir,
    [string]$ObjDir,
    [string]$Expiry = "",
    [switch]$Embed,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$OutputDir = [IO.Path]::GetFullPath(($OutputDir.Trim().TrimEnd('\', '/', '"')))
if (-not $ObjDir) { $ObjDir = [IO.Path]::Combine($here, "obj", $Configuration, "native") }
$ObjDir = [IO.Path]::GetFullPath($ObjDir)
if (-not $PluginDir) { $PluginDir = Join-Path $OutputDir "plugin" }
$PluginDir = [IO.Path]::GetFullPath(($PluginDir.Trim().TrimEnd('\', '/', '"')))

New-Item -ItemType Directory -Force $OutputDir | Out-Null
New-Item -ItemType Directory -Force $ObjDir | Out-Null
New-Item -ItemType Directory -Force $PluginDir | Out-Null

$common = @(
    (Join-Path $here "main.c"),
    (Join-Path $here "cli.c"),
    (Join-Path $here "osu.c"),
    (Join-Path $here "payload.c"),
    (Join-Path $here "expiry.c"),
    (Join-Path $here "util.c"),
    (Join-Path $here "sha256.c"),
    (Join-Path $here "inject_win.c"),
    (Join-Path $here "inject_diag.c")
)
$testSrc = $common | Where-Object { [IO.Path]::GetFileName($_) -ne "main.c" }
$testSrc += Join-Path $here "test.c"

function Write-PayloadArchive([string]$plugin, [string]$dest) {
    $files = @(Get-ChildItem $plugin -File | Where-Object { $_.Extension -ne ".pdb" } | Sort-Object Name)
    if (-not ($files | Where-Object { $_.Name -eq "Maniac.dll" })) {
        throw "Maniac.dll missing from $plugin"
    }
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([byte][char]'M')
    $bw.Write([byte][char]'N')
    $bw.Write([byte][char]'P')
    $bw.Write([byte][char]'1')
    $bw.Write([uint32]$files.Count)
    foreach ($f in $files) {
        $name = [System.Text.Encoding]::UTF8.GetBytes($f.Name)
        $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
        $bw.Write([uint16]$name.Length)
        $bw.Write([uint32]$bytes.Length)
        $bw.Write($name)
        $bw.Write($bytes)
    }
    $bw.Flush()
    [System.IO.File]::WriteAllBytes($dest, $ms.ToArray())
}

$isWindows = $env:OS -eq "Windows_NT"

$cflags = @()
if ($Expiry) { $cflags += "-DMANIAC_BUILD_EXPIRY=`"$Expiry`"" }
if ($Embed) { $cflags += "-DMANIAC_EMBED" }

$payloadBin = Join-Path $ObjDir "payload.bin"
if ($Embed -and -not (Test-Path (Join-Path $PluginDir "Maniac.dll"))) {
    throw "Cannot embed: $PluginDir has no Maniac.dll"
}

if ($isWindows) {
    $vcvars = $null
    $vswherePaths = @(
        [IO.Path]::Combine([Environment]::GetFolderPath("ProgramFilesX86"), "Microsoft Visual Studio", "Installer", "vswhere.exe"),
        [IO.Path]::Combine($env:ProgramFiles, "Microsoft Visual Studio", "Installer", "vswhere.exe"),
        "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
    )
    $vswhere = $vswherePaths | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if ($vswhere) {
        $found = & $vswhere -latest -products * -find "VC\Auxiliary\Build\vcvars64.bat" 2>$null
        if ($found) { $vcvars = @($found) | Where-Object { Test-Path $_ } | Select-Object -First 1 }
    }
    if (-not $vcvars) {
        throw "MSVC x64 tools not found. Install the Visual Studio C++ workload (cl.exe / vcvars64.bat)."
    }

    $opt = "/O2"
    if ($Configuration -eq "Debug") { $opt = "/Od /Zi" }
    $baseDefs = "/DWIN32 /DUNICODE /D_UNICODE /D_CRT_SECURE_NO_WARNINGS"
    $expiryDef = ""
    if ($Expiry) { $expiryDef = " /DMANIAC_BUILD_EXPIRY=`"$Expiry`"" }
    $exeDefs = $baseDefs + $expiryDef
    if ($Embed) { $exeDefs += " /DMANIAC_EMBED" }
    $testDefs = $baseDefs + $expiryDef

    $exe = Join-Path $OutputDir "maniac.exe"
    $stub = Join-Path $PluginDir "maniac-stub.dll"
    $tests = Join-Path $ObjDir "maniac-launcher-tests.exe"
    New-Item -ItemType Directory -Force $PluginDir | Out-Null

    $srcQuoted = ($common | ForEach-Object { "`"$_`"" }) -join " "
    $testQuoted = ($testSrc | ForEach-Object { "`"$_`"" }) -join " "
    $stubSrc = Join-Path $here "stub.c"

    $script = @"
call `"$vcvars`" >nul
cd /d `"$ObjDir`"
cl /nologo /W3 /utf-8 /O1 /LD /GS- /I `"$here`" `"$stubSrc`" /Fe`"$stub`" /link /NOLOGO kernel32.lib
if errorlevel 1 exit /b 1
"@
    $batFile = Join-Path $ObjDir "build-stub.bat"
    Set-Content -Path $batFile -Value $script -Encoding ascii
    cmd.exe /c $batFile
    if ($LASTEXITCODE -ne 0) { throw "maniac-stub.dll build failed with $LASTEXITCODE" }

    $resArg = ""
    if ($Embed) {
        Write-PayloadArchive $PluginDir $payloadBin
        $rc = Join-Path $ObjDir "payload.rc"
        "1 RCDATA `"payload.bin`"" | Set-Content -Path $rc -Encoding ascii
        $script = @"
call `"$vcvars`" >nul
cd /d `"$ObjDir`"
rc /nologo /fo payload.res payload.rc
if errorlevel 1 exit /b 1
"@
        Set-Content -Path (Join-Path $ObjDir "build-rc.bat") -Value $script -Encoding ascii
        cmd.exe /c (Join-Path $ObjDir "build-rc.bat")
        if ($LASTEXITCODE -ne 0) { throw "payload.rc compile failed" }
        $resArg = "payload.res"
    }

    $script = @"
call `"$vcvars`" >nul
cd /d `"$ObjDir`"
cl /nologo /W3 /utf-8 $opt /GS $exeDefs /I `"$here`" $srcQuoted $resArg /Fe`"$exe`" /link /SUBSYSTEM:CONSOLE user32.lib shell32.lib ole32.lib uuid.lib psapi.lib advapi32.lib
if errorlevel 1 exit /b 1
cl /nologo /W3 /utf-8 $opt /GS $testDefs /I `"$here`" $testQuoted /Fe`"$tests`" /link /SUBSYSTEM:CONSOLE user32.lib shell32.lib ole32.lib uuid.lib psapi.lib advapi32.lib
if errorlevel 1 exit /b 1
"@
    Set-Content -Path (Join-Path $ObjDir "build-exe.bat") -Value $script -Encoding ascii
    cmd.exe /c (Join-Path $ObjDir "build-exe.bat")
    if ($LASTEXITCODE -ne 0) { throw "native launcher build failed with $LASTEXITCODE" }

    Get-ChildItem $ObjDir -Filter "*.obj" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem $OutputDir -Filter "*.obj" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem $PluginDir -Filter "*.obj" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem $PluginDir -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -in ".exp", ".lib", ".ilk" } | Remove-Item -Force -ErrorAction SilentlyContinue
} else {
    $cc = $null
    foreach ($c in @("cc", "clang", "gcc")) {
        $cmd = Get-Command $c -ErrorAction SilentlyContinue
        if ($cmd) { $cc = $cmd.Source; break }
    }
    if (-not $cc) { throw "C compiler not found (cc/clang/gcc)." }

    $opt = "-O2"
    if ($Configuration -eq "Debug") { $opt = "-O0 -g" }
    $exe = Join-Path $OutputDir "maniac"
    $tests = Join-Path $ObjDir "maniac-launcher-tests"
    $unixSrc = $common | Where-Object { [IO.Path]::GetFileName($_) -ne "inject_win.c" }

    $embedObj = @()
    if ($Embed) {
        Write-PayloadArchive $PluginDir $payloadBin
        Copy-Item (Join-Path $here "payload_embed.S") (Join-Path $ObjDir "payload_embed.S") -Force
        $asOut = Join-Path $ObjDir "payload_embed.o"
        Push-Location $ObjDir
        try {
            & $cc -c "payload_embed.S" -o $asOut
            if ($LASTEXITCODE -ne 0) { throw "payload_embed.S assemble failed" }
        } finally { Pop-Location }
        $embedObj = @($asOut)
    }

    $exeFlags = @($cflags)
    $testFlags = @()
    if ($Expiry) { $testFlags += "-DMANIAC_BUILD_EXPIRY=`"$Expiry`"" }
    & $cc $opt -Wall -Wextra -I $here @exeFlags @unixSrc @embedObj -o $exe
    if ($LASTEXITCODE -ne 0) { throw "native launcher build failed with $LASTEXITCODE" }
    $unixTest = @(
        (Join-Path $here "test.c"),
        (Join-Path $here "cli.c"),
        (Join-Path $here "osu.c"),
        (Join-Path $here "payload.c"),
        (Join-Path $here "expiry.c"),
        (Join-Path $here "util.c"),
        (Join-Path $here "sha256.c"),
        (Join-Path $here "inject_diag.c")
    )
    & $cc $opt -Wall -I $here @testFlags @unixTest -o $tests
    if ($LASTEXITCODE -ne 0) { throw "native launcher tests failed to compile" }
    & chmod +x $exe
}

$sidecar = Join-Path $OutputDir "plugin"
if ([IO.Path]::GetFullPath($PluginDir) -ne [IO.Path]::GetFullPath($sidecar)) {
    New-Item -ItemType Directory -Force $sidecar | Out-Null
    try {
        Copy-Item (Join-Path $PluginDir "*") $sidecar -Force
    } catch {
        if (-not $Embed) { throw }
        Write-Host "plugin sidecar not refreshed ($($_.Exception.Message))"
    }
}

if (-not $SkipTests) {
    $testExe = if ($isWindows) { Join-Path $ObjDir "maniac-launcher-tests.exe" } else { Join-Path $ObjDir "maniac-launcher-tests" }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw "native launcher tests failed" }
}

Write-Host "Launcher $(Join-Path $OutputDir $(if ($isWindows) { 'maniac.exe' } else { 'maniac' }))"
