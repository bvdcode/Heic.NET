# Publishing Heic.NET

## Automatic releases

Every push to `main` calculates a package version with GitVersion, builds and tests
on Linux, Windows and macOS, then publishes the same package artifact to NuGet.org
and GitHub Packages in parallel. The browser package is compiled to AOT WebAssembly,
packed and checked in Chromium against the same independent pixel references,
then published to npm as `heic-net` with the same GitVersion version. Symbols are published to NuGet.org. Publication
starts only after all three platforms pass. A manual workflow run on
`main` follows the same process; pull requests build and test without publishing.

`GitVersion.yml` sets the initial version to `0.1.0`. Successful publication creates
a matching GitHub release and tag after all three registries succeed, starting with `v0.1.0`. Subsequent changes on
`main` advance the patch version. Major and minor changes follow GitVersion's
version-increment rules. Do not create release tags before publishing.

Workflow runs for the same branch are serialized. If publication fails, correct
the reported error and rerun the failed job. Existing package versions in either registry and
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

## GitHub Packages

The GitHub Packages job uses the workflow's temporary `GITHUB_TOKEN` with
`packages: write`. It uploads the same `.nupkg` produced by the Linux build to
`https://nuget.pkg.github.com/bvdcode/index.json`. The package's repository metadata
links it to `bvdcode/Heic.NET`. No additional repository secret is required.
Symbols remain available through NuGet.org and the GitHub release assets.

GitHub Packages requires authentication to install NuGet packages, including public
packages. NuGet.org remains the default installation source.
See the [GitHub NuGet registry documentation](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry).

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

## npm

The `publish-npm` job uses GitHub Actions OIDC with `id-token: write`.
The trusted publisher belongs to `heic-net` and authorizes owner `bvdcode`,
repository `Heic.NET`, workflow `publish-release.yml`, with direct publishing
enabled and no environment restriction. No npm token is stored in repository
secrets. The same GitVersion release is published to npm, NuGet.org and GitHub
Packages after the .NET matrix and browser tests pass. New releases require no
interactive npm sign-in or two-factor confirmation.

The `browser` job publishes `Heic.Net.Browser` with AOT and invariant
globalization, compiles the TypeScript API and packs an immutable `.tgz` artifact.
Tests unpack that tarball and compare visible dimensions and complete RGBA SHA-256
hashes with `corpus/manifest.json`, including the 200-MP image. Worker API tests,
lint and TypeScript checks also gate publication.

The npm package includes the decoder, Web Worker, .NET runtime, MIT license and
runtime third-party notices. Consumers copy the prebuilt assets with
`heic-net-assets`; they do not install a .NET workload.

Build a local package:

```sh
npm ci --prefix npm
dotnet workload restore Heic.Net.Browser/Heic.Net.Browser.csproj
export PACKAGE_VERSION="$(dotnet gitversion /showvariable SemVer)"
npm --prefix npm run lint
npm --prefix npm test
npm --prefix npm run build
npm --prefix npm run test:browser
cd artifacts/npm/package
npm pack --pack-destination ..
```

Set `HEIC_LARGE_IMAGE=true` for the full browser corpus, including the 200-MP
image. The default local browser test omits images over 20 MP. CI runs all entries.
Install the test browser with `npx playwright install chromium` in `npm/`,
or point `PLAYWRIGHT_CHROMIUM_EXECUTABLE` at a compatible Chromium executable.
