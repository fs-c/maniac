# Shared by the other scripts in this directory. CI only (.github/workflows/ci.yml).

function Get-PackedBinaryName([string]$rid) {
    if ($rid -like "win-*") { return "maniac.exe" }
    if ($rid -eq "osx-arm64" -or $rid -eq "osx-x64" -or $rid -eq "linux-x64") { return "maniac" }
    throw "Unsupported RuntimeIdentifier '$rid'. Use win-x64, osx-arm64, osx-x64, or linux-x64."
}

function Get-PushShortSha {
    if ($env:GITHUB_EVENT_NAME -ne "push") { return $null }
    $sha = $env:GITHUB_SHA
    if ($sha -notmatch '^[0-9a-fA-F]{7,}$') {
        throw "Push build requires GITHUB_SHA (full commit hash). Got '$sha'."
    }
    return $sha.Substring(0, 7).ToLowerInvariant()
}

# Push artifacts are renamed so two downloads do not share a filename.
# Other runs (local pack, workflow_dispatch) keep the built filename.
function Get-PackedPublishName([string]$rid, [string]$binary) {
    $short = Get-PushShortSha
    if (-not $short) { return $binary }
    $ext = [System.IO.Path]::GetExtension($binary)
    return "maniac-lazer-$rid-$short$ext"
}

function Get-StablePublishName {
    $binary = "maniac-stable.exe"
    $short = Get-PushShortSha
    if (-not $short) { return $binary }
    return "maniac-stable-win-x86-$short.exe"
}

function Get-OsuNupkgName([string]$tag, [string]$rid) {
    switch ($rid) {
        "win-x64" { return "osulazer-$tag-full.nupkg" }
        "osx-arm64" { return "osulazer-$tag-osx-arm64-full.nupkg" }
        "osx-x64" { return "osulazer-$tag-osx-x64-full.nupkg" }
        "linux-x64" { return "osulazer-$tag-linux-x64-full.nupkg" }
        default { throw "Unsupported RuntimeIdentifier '$rid'. Use win-x64, osx-arm64, osx-x64, or linux-x64." }
    }
}
