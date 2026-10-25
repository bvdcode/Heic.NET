# Publishing Heic.NET

## Automatic releases

Every push to `main` calculates a package version with GitVersion, builds and tests
on Linux, Windows and macOS, and publishes the package and symbols to NuGet.org.
Publication starts only after all three platforms pass. A manual workflow run on
`main` follows the same process; pull requests build and test without publishing.

`GitVersion.yml` sets the initial version to `0.1.0`. Successful publication creates
a matching GitHub release and tag, starting with `v0.1.0`. Subsequent changes on
`main` advance the patch version. Major and minor changes follow GitVersion's
version-increment rules. Do not create release tags before publishing.

Workflow runs for the same branch are serialized. If publication fails, correct
the reported error and rerun the failed job. Existing NuGet package versions and
GitHub releases are skipped; published package versions are immutable.

## Trusted publishing

The `bvd` NuGet.org account uses a GitHub Actions trusted publishing policy:

| Field | Value |
| --- | --- |
| Policy | `nuget-Heic.NET` |
| Repository owner | `bvdcode` |
| Repository | `Heic.NET` |
| Workflow file | `publish-release.yml` |
| Environment | None |
| Package scope | `Heic.NET` |

The publication job requests `id-token: write` and exchanges the GitHub identity
for a short-lived key using `NuGet/login@v1`. No long-lived API key is required.
See the [NuGet trusted publishing documentation](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing).

## Local package validation

Restore the pinned GitVersion tool and inspect the calculated version:

```sh
dotnet tool restore
dotnet gitversion /showvariable SemVer
```

Use the calculated version for the build and pack commands:

```sh
version="$(dotnet gitversion /showvariable SemVer)"
dotnet run --file UpdateLicense.cs -- . --check
dotnet restore Heic.Net.slnx
dotnet build Heic.Net.slnx --configuration Release --no-restore -p:Version="$version" -p:ContinuousIntegrationBuild=true
dotnet test Heic.Net.slnx --configuration Release --no-build --no-restore
dotnet pack Heic.Net/Heic.Net.csproj --configuration Release --no-build --no-restore -p:Version="$version" -p:ContinuousIntegrationBuild=true --output artifacts/packages
```

The `nuget-packages` workflow artifact contains the `.nupkg` and `.snupkg` files.
The library package contains `lib/netstandard2.1/Heic.Net.dll`, XML documentation,
`README.md`, `LICENSE`, and `icon.png`. The symbol package contains portable PDBs.
Source Link support is provided by the .NET SDK. Runtime dependencies are empty;
the analyzer package is a private build dependency.
