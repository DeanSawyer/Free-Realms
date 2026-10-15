# FreeRealmsLocal bootstrap

This folder is the first local-launch milestone for the archived Free Realms client/server. It adds a one-click launcher and a local-only development login; it does not make the unfinished world server playable yet.

## Start it

1. Keep this folder at the root of the repository, beside the Free Realms Reconnected Client folder and the Joshsora's Unfinished Free Realms Server folder.
2. Install Visual Studio Build Tools with MSBuild and the .NET desktop build tools.
3. In PowerShell, run:

   Set-ExecutionPolicy -Scope Process Bypass
   .\FreeRealmsLocal\Start-Local.ps1

The script builds the edited Gateway, copies its DLL dependencies into the bundled server's runtime roles folder, starts the existing server console, waits for TCP port 20260, and launches FreeRealms.exe against 127.0.0.1:20260.

To launch only the client after the server is already running, use .\FreeRealmsLocal\Launch-Client.ps1.

## What local login does

- Uses the fixed development account ID 1 / username LocalPlayer.
- Sends the local ticket LOCAL-ONLY.
- The Gateway accepts that ticket only when LocalMode is enabled in PrebuiltServer/Daemon/Configs/cluster.cfg.
- Does not contact the retired remote login API.

## Important limits

- Keep this mode local-only. The bundled configuration enables LocalMode for development. Do not expose this Gateway to the internet or use this configuration for a public server.
- This milestone targets Gateway login and the existing environment/zone announcements. The original project notes that character data and the world-entry handshake are unfinished; the client may still hang at the loading screen after login. That is the next server-side milestone, not a launcher failure.
- The server's world role expects MongoDB on localhost port 2199. Its availability may affect world startup even though this first test is focused on the Gateway.
- This is a bootstrap folder inside the source archive, not yet a self-contained release bundle. The client assets, server runtime, and build tools remain in their existing locations.