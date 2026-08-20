# Fish Social Unity Desktop

Unity **2021.3.29f1c1** project for Steam Windows desktop shell (`STEAM-DESKTOP-04`).

## Open

1. Unity Hub → Add → `fish-social-unity/`
2. Open scene `Assets/Scenes/DesktopMain.unity` (or Play; bootstrap auto-creates)

## Build

Menu: **Fish Social → Build Windows Development Player**  
Or batchmode (see `Docs/STEAM-DESKTOP-04-smoke.md`).

Output: `Builds/Windows64/FishSocialDesktop.exe`

For local Steam login testing, keep the Steam client running and launch the
generated executable with `Builds/Windows64/steam_appid.txt` beside it.
The build menu copies the project-root `steam_appid.txt` automatically after a
successful build. For a real Steam distribution build, launch through Steam
instead of relying on the local AppID file.

## Server URL (`STEAM-DESKTOP-10`)

Steam login, REST, and Socket.IO share one `serverBaseUrl`. Resolve order:

1. Environment variable `FISH_SOCIAL_SERVER_URL`
2. `server.json` beside the EXE (Standalone) or Unity project root (Editor)
3. Default `http://localhost:3001`

Example `server.json`:

```json
{
  "serverBaseUrl": "http://192.168.1.100:3001"
}
```

The build menu copies `server.json.example` into `Builds/Windows64/`.
Rename/copy to `server.json` and edit the URL for LAN or public-server tests.
The Settings panel can edit/save the server address, test `/health`, and shows the
active URL; bootstrap also logs `[DesktopShell] serverBaseUrl=... source=...`.
For home public-IP联调 (STEAM-DESKTOP-10A), run root `本机公网联调检查.bat`.

## Scope

- Window modes, tray hide/exit, hub UI, notification prefs
- Steamworks.NET initialization, Steam Ticket login and short-lived in-memory JWT
- Configurable Node server URL for local / LAN / public联调
- Authenticated REST and Socket.IO pond session
