# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-08-27

Initial release: desktop operator UI for a single **Cisco WVC210** on the LAN (HTTP CGI), without the vendor ActiveX console.

### Added

- **Avalonia UI (`MaksIT.Wvc210.UI`):** Windows and Linux host with Live / Setup / Status, connection bar, and AppData settings (`MaksIT.Wvc210`; legacy `Wvc210Control` still loaded).
- **Live view:** in-process MJPEG and OCX-style JPEG with G.726 16 kbit/s listen audio; PTZ via WASD/arrows, home, click-to-center, pan step, and presets.
- **Setup:** camera CGI catalog (device, network, wireless, video, users, audio) without Internet Explorer.
- **Talkback:** G.711 from a local microphone (Windows NAudio / Linux capture) when the camera speaker and talk mode are ready.
- **Status:** firmware dump and device summary.
- **Client / Shared:** CGI, streams, G.711 talkback, G.726 listen decode, models, and `appsettings.json` seed defaults.
- **Tests:** unit coverage for PTZ click map, G.711, G.726, and Sercomm base64 (no live camera). **xunit.v3** + Microsoft Testing Platform; RepoUtils test engine rewrites README coverage badges.
- Community RepoUtils (`utils/`), Apache 2.0 hygiene, and target **net10.0**.
