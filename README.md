# Heic.NET

A managed HEIC still image decoder targeting **.NET Standard 2.1**.

The library is under development. The current build establishes image ownership
and resource limits; HEVC decoding is not yet exposed.

Build and test with the .NET 10 SDK:

```sh
dotnet build Heic.Net.slnx -c Release
dotnet test Heic.Net.slnx -c Release --no-build
```
