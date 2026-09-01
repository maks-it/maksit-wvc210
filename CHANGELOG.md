# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.1] - 2026-09-01

### Fixed

- **Go**, **Patrol**, and **User home** use camera `preset=move` again. 1.2.0 sent stored X,Y as `position=` click offsets, so patrol did not reach the saved poses.
- AppData still keeps occupancy and last X,Y. On connect, NVRAM is written only when those slots are empty (camera reboot); an intact camera is left alone. **Save** still uses `preset=set`; **Delete** clears that slot and updates the patrol sequence in the same write.

## [1.2.0] - 2026-09-01

### Added

- Live presets and user home are stored in `%AppData%/MaksIT/WVC210/settings.json` (WiX install folder name, not the shipped `appsettings.json`). **Go**, **Patrol**, and **User home** use those X,Y coordinates via `position=`, so they still work after a camera reboot wipes NVRAM. Camera slots are still written on Save as a best-effort cache.

### Changed

- App icon is a faceted PTZ camera in MAKS.IT origami blues (`#006199` primary, `#33A5CF` highlight), not the brand M. Wordmark/black text is not used.
- Windows setup is per-machine: install path `C:\Program Files\MaksIT\WVC210`.
- Synced RepoUtils utils: ContainerRegistry JSON catalog (PascalCase Harbor / InCluster keys).

## [1.1.0] - 2026-08-27

### Added

- Live **Patrol** toggles a loop through occupied presets at the camera `PatrolInterval` (5–60 s, default 8 s). Pad, Go, Home, or Auto-pan stops it.
- Each preset row has **Delete**, enabled when the slot is occupied: `preset=all` name, PTZ `PresetNPosition` coordinates (not blank / `0,0` / `-1,-1`), or a successful **Save** if the camera omits the name.

### Changed

- GitHub release assets are siblings: portable `maksit-wvc210-{version}.zip` (win-x64 only), Windows setup `maksit-wvc210-{version}.exe`, and `maksit-wvc210-{version}.flatpak`. The installer and Flatpak are not packed inside the zip. On Windows the Flatpak bundle is built via WSL Debian.
- About is only in the side nav. Copyright uses the current year (no range).
- Live **Audio** follows camera Operation (simplex listen / talk, half duplex, full duplex) as status text; Operation is set in Setup. Speak and speaker test share one talk CGI for the Live session.

### Fixed

- Live preset **Refresh** no longer throws `MethodAccessException` (compiled binding vs private command method).
- Refresh no longer overlaps the Presets heading on hover.
- Preset **Delete** clears only that slot (load PTZ group, empty `PresetNName` / `PresetNPosition`, write the group back). `preset=del` and a partial `set_group` were wiping every preset.

## [1.0.0] - 2026-08-27

Initial release: desktop operator UI for a single **Cisco WVC210** on the LAN (HTTP CGI), without the vendor ActiveX console.

### Added

- **Avalonia UI (`MaksIT.Wvc210.UI`):** Windows and Linux host with Live / Setup / Status, connection bar, **About** (MaksIT, Maksym Sadovnychyy, version), and AppData settings (`MaksIT.Wvc210`; legacy `Wvc210Control` still loaded).
- **Live view:** stream dropdown — **ASF MPEG-4 + audio in-process** (bundled libVLC, no VLC install; frames drawn on the Avalonia image so click-to-center works without a native overlay), RTSP-TCP, MJPEG (no audio), snapshots. PTZ pad / WASD. **Day/night:** Auto watches live brightness (hysteresis + 2.5s dwell) and switches **black & white** (`VIDEO` `color=5`) when the scene stays dark; Day/Night force color or B/W. WVC210 has no IR lamp or light-sensor CGI. Live **Audio** shows the camera operation (simplex listen / talk, half duplex, full duplex) and its capabilities; Operation is set in Setup. The vendor OCX JPEG mux is not offered.
- **Setup:** camera CGI catalog (device, network, wireless, video, users, audio) without Internet Explorer.
- **Talkback:** G.711 from a local microphone (Windows NAudio / Linux capture) when Speaker out is on and Operation allows talk. **Speaker test** and **Speak** share one talk CGI POST for the Live session (quiet keepalive when idle). Simplex listen disables speak. Simplex talk mutes live listen. Half duplex mutes listen while Speak or speaker CGI is held. Full duplex keeps ASF/RTSP audio up. Later tests only switch beeps on the same socket. Leaving Live closes it.
- **Status:** firmware dump and device summary.
- **Client / Shared:** CGI, streams, G.711 talkback, G.726 listen decode, WaveOut PCM playback on Windows, models, and `appsettings.json` seed defaults.
- **Tests:** unit coverage for PTZ click map, G.711, G.726, audio operation CGI, and Sercomm base64 (no live camera). **xunit.v3** + Microsoft Testing Platform; RepoUtils test engine rewrites README coverage badges.
- Community RepoUtils (`utils/`), Apache 2.0 hygiene, and target **net10.0**.

