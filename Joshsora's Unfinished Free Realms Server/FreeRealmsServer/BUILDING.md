# Server source and build notes

This folder contains an unfinished Free Realms Reconnected server solution.

## Solution

Open `FreeRealmsServer.sln` in Visual Studio. The projects are legacy, non-SDK-style C# projects targeting **.NET Framework 4.5**:

- `FreeRealms` — shared server-side classes
- `Gateway` — gateway/login code; references `FreeRealms`
- `Decompress` — a small archive/decompression utility
- `CrapLauncher` — a launcher utility

The solution file identifies Visual Studio 2013-era tooling. A newer Visual Studio with the .NET Framework 4.5 targeting pack may work, but that has not been verified.

## Dependencies and known build caveats

The project files refer to older NuGet dependencies, including log4net 2.0.3, Newtonsoft.Json 7.0.1, and DotNetZip 1.9.6. Restore packages from the checked-in `packages.config` files if they are not already available locally.

**LibSOE is supplied as a prebuilt dependency.** The `FreeRealms` and `Gateway` project references now point to `PrebuiltServer/Daemon/LibSOE.dll`, which is included in this repository, rather than the original author's machine-specific path. This removes that particular path dependency, but does not establish that the source projects build successfully or that the bundled assembly is API-compatible with every source file.

No successful clean build has been confirmed as part of this cleanup. Treat this as documentation of the current project structure, not a claim that the server currently compiles or runs.

## Repository hygiene

Generated Visual Studio state and .NET build outputs (`.vs/`, `bin/`, and `obj/`) are excluded by the repository-level `.gitignore`. Previously committed copies are being removed from the cleanup branch; build outputs can be recreated locally.
