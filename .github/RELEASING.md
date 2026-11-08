# Releasing Tailwind.MSBuild

## Normal releases

Publish a GitHub release with a canonical version tag, such as `v2.1.1` or
`v2.2.0-preview.1`. Both stable and prerelease publication events use the same
NuGet.org pipeline. The tag determines the package version and prerelease status;
the GitHub prerelease checkbox does not override it. Tags containing build metadata
(`+...`) are rejected because NuGet ignores that metadata when comparing versions.

The workflow validates the published release, resolves its tag to one commit SHA,
and checks whether the package version already exists on NuGet.org, including
unlisted versions. If it exists, the run stops without creating another version.
A failed or malformed lookup stops the run rather than assuming the version is free.

The Ubuntu unit/integration tests must pass before Windows packaging. Both jobs
check out the same resolved SHA with full Git history. MinVer must produce the
selected release version; a mismatch stops publication. Only the publishing job
can obtain a short-lived NuGet credential through Trusted Publishing.

PRs run tests. Pushes to main run tests and produce a package artifact but do not
publish it. Release preparation safeguards are tested offline on every run.

## Recovering a failed publication

1. Review and merge the workflow fix to main.
2. Open **Actions → Build, Test & Release → Run workflow**.
3. Select **main** for the workflow and enter the existing published release tag
   in `release_tag`.

This uses main's corrected workflow while testing and packing the exact tagged
source. It never substitutes main's application code. The run summary identifies
both workflow and source revisions. A branch, untagged commit, missing/draft
release, or already-published package cannot create a publication. No version is
automatically incremented. A new version needs its own intentional release tag.

The tagged source's tests must still pass. Recovery cannot fix a source-code defect
without a new release. If multiple version tags point at the same commit and MinVer
selects another version, the package-version guard stops the run for review.

Use GitHub's normal **Re-run jobs** for transient failures when the original
workflow is still correct. It does not pick up a later edit to this workflow.
If another run publishes the version after the duplicate check, NuGet rejects the
conflicting push; the workflow does not silently skip it or issue a new version.

## Local checks

```sh
pwsh -NoProfile -File .github/scripts/Test-ReleasePreparation.ps1
dotnet restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --no-restore
actionlint
```

The PowerShell safeguard tests mock Git, GitHub, and NuGet. They perform no remote
writes or publication. The regular .NET suite includes network-based download tests.
