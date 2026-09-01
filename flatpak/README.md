# Flathub packaging (com.maks_it.wvc210)

[Flathub](https://flathub.org/) builds from **source**. The GitHub `.flatpak` asset is a sideload bundle and is **not** what you submit.

Flathub [requirements](https://docs.flathub.org/docs/for-app-authors/requirements): no network at build time, no precompiled app binaries in the PR, metadata lives in this repository (`data/`). [Submission](https://docs.flathub.org/docs/for-app-authors/submission) is a GitHub PR against `flathub/flathub` **`new-pr`**, titled `Add com.maks_it.wvc210`.

**Do not open that PR with an AI agent.** Flathub forbids AI-generated submission PRs, commit messages, and review replies. A human must copy these files, update the git commit, run the linter, and open the PR.

## Files to copy into the Flathub submission

Place these at the root of a branch from `new-pr` (not `master`):

| This repo | Submission root |
|-----------|-----------------|
| `flatpak/com.maks_it.wvc210.yml` | `com.maks_it.wvc210.yml` |
| `flatpak/nuget-sources.json` | `nuget-sources.json` |

Do not add application source, `releases/*.flatpak`, or build artifacts to the Flathub repo.

## Before you open the PR

1. Merge `data/`, `flatpak/`, and the launcher/metainfo into `main` and tag (or amend `v1.2.1` if you are still on that release).
2. Set `modules.wvc210.sources[0].commit` in the YAML to that tag’s full git SHA.
3. Put at least two **Linux window** screenshots in `data/screenshots/live.png` and `data/screenshots/setup.png` (app window only, ≤1000×700, with decoration). Point the `<image>` URLs at a **tag or commit**, not a branch: `https://raw.githubusercontent.com/maks-it/maksit-wvc210/<tag>/data/screenshots/live.png`.
4. Regenerate NuGet sources (needs Flatpak + `org.freedesktop.Sdk.Extension.dotnet10` on Linux):

   ```bash
   wget -O flatpak-dotnet-generator.py \
     https://raw.githubusercontent.com/flatpak/flatpak-builder-tools/master/dotnet/flatpak-dotnet-generator.py
   python3 flatpak-dotnet-generator.py --dotnet 10 --freedesktop 25.08 \
     --runtime linux-x64 --runtime linux-arm64 \
     nuget-sources.json ../src/MaksIT.Wvc210.UI/MaksIT.Wvc210.UI.csproj
   ```

5. Local build and lint ([submission](https://docs.flathub.org/docs/for-app-authors/submission)):

   ```bash
   flatpak install -y flathub org.flatpak.Builder
   flatpak run --command=flatpak-builder-lint org.flatpak.Builder manifest com.maks_it.wvc210.yml
   flatpak run --command=flatpak-builder-lint org.flatpak.Builder appstream ../data/com.maks_it.wvc210.metainfo.xml
   flatpak run --command=flathub-build org.flatpak.Builder --install com.maks_it.wvc210.yml
   flatpak run com.maks_it.wvc210
   ```

6. Confirm `https://maks-it.com/` is reachable over HTTPS (ID `com.maks_it.wvc210`). Verification later uses `https://maks-it.com/.well-known/org.flathub.VerifiedApps.txt`.

## Sandbox

Finish-args grant network (camera CGI), X11 (GNOME app icon; Avalonia 12.1.2 native Wayland still hangs on configure(0,0)), PulseAudio (talkback), DRI, and `xdg-documents` / `xdg-pictures` (config export and snapshots). There is no `--filesystem=home`; settings use XDG config inside the app dir.

## AI disclosure

The first draft of this packaging (manifest, metainfo, desktop file, launch wrapper) was produced with an AI coding assistant and then reviewed. The application C# was not generated for this submission. The Flathub PR itself must be opened and described by a human.
