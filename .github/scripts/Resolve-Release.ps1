param(
    [Parameter(Mandatory)]
    [ValidateSet('push', 'pull_request', 'release', 'workflow_dispatch')]
    [string] $EventName,
    [Parameter(Mandatory)] [string] $EventRef,
    [Parameter(Mandatory)] [string] $EventSha,
    [string] $ReleaseTag,
    [string] $Repository = $env:GITHUB_REPOSITORY
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($EventName -eq 'workflow_dispatch' -and $EventRef -cne 'refs/heads/main') {
    throw 'Run manual release recovery from main to use the corrected workflow.'
}

$sourceSha = $EventSha
$version = ''
$shouldRun = $true
$isRelease = $EventName -in @('release', 'workflow_dispatch')

if ($isRelease) {
    # Accept canonical SemVer release tags, including preview/rc identifiers.
    # Build metadata is deliberately excluded because NuGet ignores it for identity.
    $number = '(0|[1-9][0-9]*)'
    $identifier = '(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)'
    if ($ReleaseTag -cnotmatch "^v$number\.$number\.$number(-$identifier(\.$identifier)*)?$") {
        throw 'Select a release tag such as v2.1.0 or v2.2.0-preview.1; a branch or untagged commit cannot be published.'
    }
    $version = $ReleaseTag.Substring(1)

    $sourceSha = git rev-parse --verify "refs/tags/$ReleaseTag^{commit}"
    if ($LASTEXITCODE -ne 0 -or $sourceSha -cnotmatch '^[0-9a-f]{40,64}$') {
        throw "Release tag '$ReleaseTag' does not resolve to a commit."
    }
    if ($EventName -eq 'release' -and $sourceSha -cne $EventSha) {
        throw "Release tag '$ReleaseTag' no longer matches the commit that triggered this run."
    }

    $releaseJson = gh release view $ReleaseTag --repo $Repository --json tagName,isDraft,isPrerelease,publishedAt
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to verify the published GitHub release '$ReleaseTag'."
    }
    $release = $releaseJson | ConvertFrom-Json
    if ($release.tagName -cne $ReleaseTag -or $release.isDraft -or -not $release.publishedAt) {
        throw "Release '$ReleaseTag' must be published in this repository."
    }
    if ($release.isPrerelease -ne $version.Contains('-')) {
        Write-Warning 'The GitHub prerelease checkbox differs from the tag. The tag determines the NuGet package version.'
    }

    # The flat-container index includes unlisted versions. Search results do not.
    # Fail closed for network errors, unexpected status codes, or malformed responses.
    $response = Invoke-WebRequest -Uri 'https://api.nuget.org/v3-flatcontainer/tailwind.msbuild/index.json' -SkipHttpErrorCheck -TimeoutSec 30
    if ($response.StatusCode -eq 200) {
        $index = $response.Content | ConvertFrom-Json
        if (-not ($index.PSObject.Properties.Name -contains 'versions') -or $index.versions -isnot [array]) {
            throw 'NuGet returned an invalid version index; publication is stopped.'
        }
        $versionPattern = "^$number\.$number\.$number(\.$number)?(-$identifier(\.$identifier)*)?$"
        foreach ($publishedVersion in $index.versions) {
            if ($publishedVersion -isnot [string] -or $publishedVersion -cnotmatch $versionPattern) {
                throw 'NuGet returned an invalid version entry; publication is stopped.'
            }
        }
        if ($index.versions -contains $version.ToLowerInvariant()) {
            $shouldRun = $false
            Write-Host "Tailwind.MSBuild $version is already published. Stopping without testing, packing, or publishing another version."
        }
    }
    elseif ($response.StatusCode -ne 404) {
        throw "NuGet version lookup failed with HTTP $($response.StatusCode); publication is stopped."
    }
}

if ($sourceSha -cnotmatch '^[0-9a-f]{40,64}$') {
    throw 'The source commit SHA is invalid.'
}

@(
    "source_sha=$sourceSha"
    "version=$version"
    "should_run=$($shouldRun.ToString().ToLowerInvariant())"
    "publish=$(($isRelease -and $shouldRun).ToString().ToLowerInvariant())"
) | Add-Content -LiteralPath $env:GITHUB_OUTPUT

if ($env:GITHUB_STEP_SUMMARY) {
    @(
        '### Build source'
        "- Workflow revision: $EventSha"
        "- Source revision: $sourceSha"
        "- Release tag: $ReleaseTag"
        "- Package version: $version"
        "- Continue: $shouldRun"
    ) | Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY
}
