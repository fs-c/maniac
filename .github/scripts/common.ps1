# Shared by the other scripts in this directory. CI only (.github/workflows/ci.yml).

function Get-PackedBinaryName([string]$rid) {
    if ($rid -like "win-*") { return "maniac.exe" }
    if ($rid -eq "osx-arm64" -or $rid -eq "osx-x64" -or $rid -eq "linux-x64") { return "maniac" }
    throw "Unsupported RuntimeIdentifier '$rid'. Use win-x64, osx-arm64, osx-x64, or linux-x64."
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
