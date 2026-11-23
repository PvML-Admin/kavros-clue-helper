# kavros-clue-helper

Clue scroll helper overlay for Kavros. Version 0.7.23 is a Windows .NET Framework 4.8 application. The full guide is in [README.txt](README.txt).

v0.7.23: The next slider step shows without a full re-read of every clue, and Windows 125% is read the same way as 150% and 200% for both the modern and the legacy interface. See [CHANGELOG.md](CHANGELOG.md).

The helper reads the game window locally and does not send game input. Maps and clue references are bundled offline. The only runtime network request is to GitHub, for update checks and update downloads, and only when **Check for updates at startup** is enabled or you press **Check for updates** in Settings.

## Releases

Push a tag that matches the first three parts of the version in `source/app.manifest`. The current release tag is `v0.7.23`. The Windows workflow builds with `source/build.ps1`, packs the executable and the `assets` folder with Velopack, and publishes these files to GitHub Releases:

- `KavrosClueHelper-win-Setup.exe`
- `KavrosClueHelper-win-Portable.zip`
- `KavrosClueHelper-<version>-full.nupkg` (and any later delta packages)
- `releases.win.json`
