# Server source and build notes

This folder contains an unfinished Free Realms Reconnected server solution.

## Solution

Open `FreeRealmsServer.sln` in Visual Studio. The projects are legacy, non-SDK-style C# projects targeting **.NET Framework 4.5**:

- `FreeRealms` — shared server-side classes
- `Gateway` — gateway/login code; references `FreeRealms`
- `Decompress` — a small archive/decompression utility
- `CrapLauncher` — a launcher utility

The solution file identifies Visual Studio 2013-era tooling. A newer Visual Studio may work, but it needs the .NET Framework 4.5 targeting/reference assemblies; the runtime alone is not enough to compile this target.

## Dependencies and known build caveats

The projects use older NuGet dependencies, including log4net 2.0.3, Newtonsoft.Json 7.0.1, and DotNetZip 1.9.6. Restore packages from the checked-in `packages.config` files if they are not already available locally.

**Dependency security:** the NuGet restore performed by GitHub Actions reported known security advisories for these pinned versions of log4net, Newtonsoft.Json, and DotNetZip. These warnings do not by themselves establish exploitability in this server, but the versions should be reviewed and upgraded where compatibility permits. Do not update dependencies blindly: first compile and test the existing code, then make and verify focused upgrades.

**LibSOE is supplied as a prebuilt dependency.** The `FreeRealms` and `Gateway` project references point to `PrebuiltServer/Daemon/LibSOE.dll`, included in this repository, rather than the original author's machine-specific path. This removes that particular path dependency, but does not establish that the bundled assembly is API-compatible with every source file.

## Automated build

The GitHub Actions workflow at `.github/workflows/legacy-server-build.yml` restores NuGet dependencies and builds the solution in Release configuration on Windows. It downloads the .NET Framework 4.5 reference-assembly package because the hosted runner may not include this legacy targeting pack.

The first CI attempt stopped before C# compilation because the .NET Framework 4.5 reference assemblies were missing. The targeting-pack workaround now works: the build reaches C# compilation, where `FreeRealms/Accounts/AccountManager.cs` fails because the bundled `LibSOE.dll` does not expose `SOEConnectionManager.OnDisconnect`, which this source subscribes to. The public `Joshsora/LibSOE` source also has no such event on `SOEConnectionManager`. Do not simply remove the subscription to make CI pass: it is intended to log accounts out on disconnect, so removing it would change server behavior. Resolve the dependency/API mismatch (for example, identify the intended LibSOE revision or provide a compatible source/build) before treating the server build as healthy. The solution remains **not yet verified to build**.

## Repository hygiene

Generated Visual Studio state and .NET build outputs (`.vs/`, `bin/`, and `obj/`) are excluded by the repository-level `.gitignore`. Previously committed copies are being removed from the cleanup branch; build outputs can be recreated locally.
