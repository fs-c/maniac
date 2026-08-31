param(
    [string]$Tag = $env:OSU_TAG
)

$ErrorActionPreference = "Stop"
$headers = @{
    "User-Agent" = "maniac-lazer-ci"
    "Accept"     = "application/vnd.github+json"
}
if ($env:GH_TOKEN) {
    $headers["Authorization"] = "Bearer $($env:GH_TOKEN)"
}

if ([string]::IsNullOrWhiteSpace($Tag)) {
    $releases = Invoke-RestMethod -Headers $headers -Uri "https://api.github.com/repos/ppy/osu/releases?per_page=20"
    $stable = @(
        $releases |
            Where-Object { -not $_.prerelease } |
            Sort-Object { Get-Date $_.published_at } -Descending
    )
    if ($stable.Count -eq 0) { throw "No stable ppy/osu releases found." }
    $release = $stable[0]
    $Tag = $release.tag_name
} else {
    $release = Invoke-RestMethod -Headers $headers -Uri "https://api.github.com/repos/ppy/osu/releases/tags/$Tag"
}

$asset = $release.assets | Where-Object { $_.name -eq "install.exe" } | Select-Object -First 1
if (-not $asset) { throw "Release $Tag has no install.exe asset." }

Write-Host "osu!lazer tag $Tag"
if ($env:GITHUB_OUTPUT) {
    "tag=$Tag" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}
if ($env:GITHUB_ENV) {
    "OSU_TAG=$Tag" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8
    "OSU_INSTALL_URL=$($asset.browser_download_url)" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8
}
if ($env:GITHUB_STEP_SUMMARY) {
    "Resolved osu!lazer **$Tag**" | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
}
