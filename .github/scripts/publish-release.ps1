# CI only (.github/workflows/ci.yml). Uploads the build artifacts to the GitHub release for a v* tag.
param(
    [Parameter(Mandatory = $true)]
    [string]$Dist
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false

if ([string]::IsNullOrWhiteSpace($env:TAG)) { throw "TAG is required." }
if ($env:TAG -notmatch '^v') { throw "TAG '$($env:TAG)' does not start with v." }
if ([string]::IsNullOrWhiteSpace($env:GH_TOKEN)) { throw "GH_TOKEN is required." }
if ([string]::IsNullOrWhiteSpace($env:GITHUB_REPOSITORY)) { throw "GITHUB_REPOSITORY is required." }
if (-not (Test-Path $Dist)) { throw "Dist directory not found: $Dist" }

$assets = @(Get-ChildItem -Path $Dist -Recurse -File | ForEach-Object { $_.FullName })
if ($assets.Count -eq 0) { throw "No files in $Dist." }

$tag = $env:TAG
$repo = $env:GITHUB_REPOSITORY
gh release view $tag --repo $repo | Out-Null
if ($LASTEXITCODE -eq 0) {
    gh release upload $tag @assets --clobber --repo $repo
    if ($LASTEXITCODE -ne 0) { throw "gh release upload failed with $LASTEXITCODE" }
    exit 0
}

gh release create $tag @assets --title $tag --verify-tag --repo $repo
if ($LASTEXITCODE -ne 0) { throw "gh release create failed with $LASTEXITCODE" }
