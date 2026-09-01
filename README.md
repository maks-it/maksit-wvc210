# MaksIT.Wvc210

![Line Coverage](https://img.shields.io/badge/Line%20Coverage-19.6%25-orange)
![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-27.2%25-yellow)
![Method Coverage](https://img.shields.io/badge/Method%20Coverage-21.8%25-yellow)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/License-Apache%202.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux-0078D6)

Desktop console for the **Cisco WVC210** IP camera. Native Avalonia app on Windows and Linux: live MJPEG, PTZ, setup pages, talkback, and status — without the vendor ActiveX UI.

See [LICENSE.md](LICENSE.md) (Apache 2.0). Changes: [CHANGELOG.md](CHANGELOG.md). Contributing: [CONTRIBUTING.md](CONTRIBUTING.md).

If you find this project useful, please consider supporting its development:

[<img src="https://cdn.buymeacoffee.com/buttons/v2/default-blue.png" alt="Buy Me A Coffee" style="height: 60px; width: 217px;">](https://www.buymeacoffee.com/maksitcom)

## Highlights

- **Live view** — MJPEG and OCX-style JPEG frames in-process
- **PTZ** — pan/tilt steps from the live pane
- **Setup** — camera CGI catalog (users, network, video) without Internet Explorer
- **Talkback** — G.711 from a local microphone (Windows NAudio / Linux capture)
- **Status** — firmware dump and device summary

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A Cisco WVC210 on the LAN (HTTP CGI)
- Windows or Linux

## Getting started

From `src/` so `global.json` applies:

```powershell
cd src
dotnet build MaksIT.Wvc210.slnx
dotnet run --project MaksIT.Wvc210.UI
```

Operator settings (host, pan step, live stream, preset occupancy/backup, user home) are written to `%AppData%/MaksIT/WVC210/settings.json` (same product folder as WiX: `Program Files\MaksIT\WVC210`). Preset poses are stored on the camera; AppData is used to restore empty NVRAM after a reboot. `src/MaksIT.Wvc210.Shared/appsettings.json` is seed defaults only (copied next to the exe, never written).

## Tests

```powershell
utils\Invoke-TestEngine.bat
```

From `src/`:

```powershell
dotnet test MaksIT.Wvc210.Tests
```

Tests do not require a live camera. Coverage shields at the top of this file are maintained by the test engine (**CoverageBadges**).

## Release

1. Update [CHANGELOG.md](CHANGELOG.md) and bump `<Version>` in [src/Directory.Build.props](src/Directory.Build.props).
2. Tag `v{version}` on `main` when a remote exists.
3. Run `utils\Invoke-ReleasePackage.bat`.

## Solution layout

```text
utils/                     # RepoUtils test and release engines (Community)
src/
  MaksIT.Wvc210.slnx
  MaksIT.Wvc210.Client/    # CGI, MJPEG/OCX, G.711, talkback
  MaksIT.Wvc210.Shared/    # models + appsettings
  MaksIT.Wvc210.UI/        # Avalonia desktop host
  MaksIT.Wvc210.Tests/
```

## Scope

This is a desktop operator UI for **one camera model**. It is not a NVR, ONVIF stack, or general Cisco camera manager.

## License

Apache 2.0 — see [LICENSE.md](LICENSE.md).

© Maksym Sadovnychyy (MAKS-IT)
