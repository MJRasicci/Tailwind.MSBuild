# Offline regression tests. Git, GitHub, and NuGet are mocked; nothing is published.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scriptPath = Join-Path $PSScriptRoot 'Resolve-Release.ps1'
$workflowSha = 'a' * 40
$releaseSha = 'b' * 40
$cases = @(
    @{ Name = 'main push builds without publishing'; Event = 'push'; Publish = 'false' }
    @{ Name = 'pull request tests without publishing'; Event = 'pull_request'; Publish = 'false' }
    @{ Name = 'stable release publishes'; Event = 'release'; Tag = 'v2.1.0' }
    @{ Name = 'preview release publishes'; Event = 'release'; Tag = 'v2.2.0-preview.1' }
    @{ Name = 'manual recovery uses release SHA, not workflow SHA'; Tag = 'v2.1.0' }
    @{ Name = 'manual preview recovery publishes'; Tag = 'v2.2.0-preview.1' }
    @{ Name = 'tag controls prerelease despite GitHub checkbox'; Tag = 'v2.2.0-preview.1'; Prerelease = $false }
    @{ Name = 'existing stable version stops'; Tag = 'v2.1.0'; Versions = @('2.1.0'); Continue = 'false'; Publish = 'false' }
    @{ Name = 'existing preview version stops case-insensitively'; Tag = 'v2.2.0-Preview.1'; Versions = @('2.2.0-preview.1'); Continue = 'false'; Publish = 'false' }
    @{ Name = 'missing package index permits first publication'; Tag = 'v2.1.0'; Status = 404 }
    @{ Name = 'manual workflow outside main fails'; Tag = 'v2.1.0'; Ref = 'refs/heads/other'; Error = '*from main*' }
    @{ Name = 'untagged SHA cannot publish'; Tag = $releaseSha; Error = '*Select a release tag*' }
    @{ Name = 'branch cannot publish'; Tag = 'main'; Error = '*Select a release tag*' }
    @{ Name = 'empty tag cannot publish'; Tag = ''; Error = '*Select a release tag*' }
    @{ Name = 'leading-zero version fails'; Tag = 'v02.1.0'; Error = '*Select a release tag*' }
    @{ Name = 'leading-zero preview identifier fails'; Tag = 'v2.2.0-preview.01'; Error = '*Select a release tag*' }
    @{ Name = 'build metadata cannot bypass duplicates'; Tag = 'v2.1.0+build.7'; Error = '*Select a release tag*' }
    @{ Name = 'missing tag fails'; Tag = 'v2.1.0'; GitExit = 128; Error = '*does not resolve*' }
    @{ Name = 'moved release tag fails'; Event = 'release'; Tag = 'v2.1.0'; SourceSha = $workflowSha; Error = '*no longer matches*' }
    @{ Name = 'missing GitHub release fails'; Tag = 'v2.1.0'; GhExit = 1; Error = '*Unable to verify*' }
    @{ Name = 'draft release fails'; Tag = 'v2.1.0'; Draft = $true; Error = '*must be published*' }
    @{ Name = 'unpublished release fails'; Tag = 'v2.1.0'; Published = $null; Error = '*must be published*' }
    @{ Name = 'GitHub release tag mismatch fails'; Tag = 'v2.1.0'; ActualTag = 'v2.0.3'; Error = '*must be published*' }
    @{ Name = 'NuGet outage fails closed'; Tag = 'v2.1.0'; Status = 503; Error = '*HTTP 503*' }
    @{ Name = 'NuGet access error fails closed'; Tag = 'v2.1.0'; Status = 403; Error = '*HTTP 403*' }
    @{ Name = 'NuGet connection error fails closed'; Tag = 'v2.1.0'; NetworkError = $true; Error = '*network unavailable*' }
    @{ Name = 'malformed NuGet response fails closed'; Tag = 'v2.1.0'; Json = '{"unexpected":[]}'; Error = '*invalid version index*' }
    @{ Name = 'null version entry fails closed'; Tag = 'v2.1.0'; Json = '{"versions":[null]}'; Error = '*invalid version entry*' }
    @{ Name = 'numeric version entry fails closed'; Tag = 'v2.1.0'; Json = '{"versions":[21]}'; Error = '*invalid version entry*' }
    @{ Name = 'empty version entry fails closed'; Tag = 'v2.1.0'; Json = '{"versions":[""]}'; Error = '*invalid version entry*' }
    @{ Name = 'invalid version string fails closed'; Tag = 'v2.1.0'; Json = '{"versions":["not-a-version"]}'; Error = '*invalid version entry*' }
    @{ Name = 'invalid JSON fails closed'; Tag = 'v2.1.0'; Json = 'not JSON'; Error = '*JSON*' }
    @{ Name = 'null versions fail closed'; Tag = 'v2.1.0'; Json = '{"versions":null}'; Error = '*invalid version index*' }
)

function git {
    if (($args -join ' ') -cne "rev-parse --verify refs/tags/$($global:ReleasePreparationTestScenario.Tag)^{commit}") {
        throw "Unexpected git invocation: $args"
    }
    $global:LASTEXITCODE = $global:ReleasePreparationTestScenario.GitExit
    $global:ReleasePreparationTestScenario.SourceSha
}
function gh {
    if (($args[0..5] -join ' ') -cne "release view $($global:ReleasePreparationTestScenario.Tag) --repo fixture/repo --json" -or
        ($args[6] -join ',') -cne 'tagName,isDraft,isPrerelease,publishedAt') {
        throw "Unexpected GitHub invocation: $args"
    }
    $global:LASTEXITCODE = $global:ReleasePreparationTestScenario.GhExit
    @{
        tagName = $global:ReleasePreparationTestScenario.ActualTag
        isDraft = $global:ReleasePreparationTestScenario.Draft
        isPrerelease = $global:ReleasePreparationTestScenario.Prerelease
        publishedAt = $global:ReleasePreparationTestScenario.Published
    } | ConvertTo-Json -Compress
}
function Invoke-WebRequest {
    param([string] $Uri, [switch] $SkipHttpErrorCheck, [int] $TimeoutSec)
    if ($Uri -cne 'https://api.nuget.org/v3-flatcontainer/tailwind.msbuild/index.json' -or -not $SkipHttpErrorCheck -or $TimeoutSec -ne 30) {
        throw "Unexpected NuGet request: $Uri"
    }
    if ($global:ReleasePreparationTestScenario.NetworkError) { throw 'network unavailable' }
    [pscustomobject]@{ StatusCode = $global:ReleasePreparationTestScenario.Status; Content = $global:ReleasePreparationTestScenario.Json }
}

$oldOutput = $env:GITHUB_OUTPUT
$oldSummary = $env:GITHUB_STEP_SUMMARY
$temp = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    foreach ($case in $cases) {
        $global:ReleasePreparationTestScenario = @{
            Event = 'workflow_dispatch'; Ref = 'refs/heads/main'; Tag = ''; ActualTag = ''
            SourceSha = $releaseSha; GitExit = 0; GhExit = 0; Draft = $false
            Published = '2026-11-07T00:00:00Z'; Prerelease = $false
            Status = 200; Versions = @(); Json = ''; NetworkError = $false
            Continue = 'true'; Publish = 'true'; Error = ''
        }
        foreach ($key in $case.Keys) { $global:ReleasePreparationTestScenario[$key] = $case[$key] }
        if (-not $case.ContainsKey('ActualTag')) { $global:ReleasePreparationTestScenario.ActualTag = $global:ReleasePreparationTestScenario.Tag }
        if (-not $case.ContainsKey('Prerelease')) { $global:ReleasePreparationTestScenario.Prerelease = $global:ReleasePreparationTestScenario.Tag.Contains('-') }
        if (-not $case.ContainsKey('Json')) { $global:ReleasePreparationTestScenario.Json = @{ versions = $global:ReleasePreparationTestScenario.Versions } | ConvertTo-Json -Compress }
        $env:GITHUB_OUTPUT = Join-Path $temp 'output'
        $env:GITHUB_STEP_SUMMARY = Join-Path $temp 'summary'
        Set-Content -LiteralPath $env:GITHUB_OUTPUT -Value ''
        Set-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value ''
        $eventSha = if ($global:ReleasePreparationTestScenario.Event -eq 'release') { $releaseSha } else { $workflowSha }
        $caught = $null
        try {
            & $scriptPath -EventName $global:ReleasePreparationTestScenario.Event -EventRef $global:ReleasePreparationTestScenario.Ref -EventSha $eventSha -ReleaseTag $global:ReleasePreparationTestScenario.Tag -Repository 'fixture/repo'
        }
        catch { $caught = $_ }
        if ($global:ReleasePreparationTestScenario.Error) {
            if ($null -eq $caught -or $caught.Exception.Message -notlike $global:ReleasePreparationTestScenario.Error) {
                throw "$($case.Name): expected '$($global:ReleasePreparationTestScenario.Error)', got '$caught'"
            }
            if (Get-Content -LiteralPath $env:GITHUB_OUTPUT | Where-Object { $_ -eq 'publish=true' }) {
                throw "$($case.Name): failed validation authorized publication"
            }
        }
        else {
            if ($caught) { throw "$($case.Name): $caught" }
            $outputs = (Get-Content -LiteralPath $env:GITHUB_OUTPUT -Raw) | ConvertFrom-StringData
            $expectedSha = if ($global:ReleasePreparationTestScenario.Event -in @('push', 'pull_request')) { $workflowSha } else { $releaseSha }
            $expectedVersion = if ($global:ReleasePreparationTestScenario.Tag) { $global:ReleasePreparationTestScenario.Tag.Substring(1) } else { '' }
            if ($outputs.source_sha -cne $expectedSha -or $outputs.version -cne $expectedVersion -or
                $outputs.should_run -cne $global:ReleasePreparationTestScenario.Continue -or $outputs.publish -cne $global:ReleasePreparationTestScenario.Publish) {
                throw "$($case.Name): unexpected outputs $(ConvertTo-Json $outputs -Compress)"
            }
        }
        Write-Host "PASS: $($case.Name)"
    }
    Write-Host "All $($cases.Count) release preparation tests passed."
}
finally {
    $env:GITHUB_OUTPUT = $oldOutput
    $env:GITHUB_STEP_SUMMARY = $oldSummary
    Remove-Item -LiteralPath $temp -Recurse -Force
}
