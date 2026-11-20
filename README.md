# kavros-clue-helper

Clue scroll helper overlay for Kavros. Version 0.7.8 is a Windows .NET Framework 4.8 application. The full guide is in [README.txt](README.txt).

v0.7.8: puzzle numbers land on the tiles at 1440p and 4K, towers step through blank, and Save debug stores a screenshot with the detection log. See [CHANGELOG.md](CHANGELOG.md).

The helper reads the game window locally and does not send game input. Maps and clue references are bundled offline. The only runtime network request is to GitHub, for update checks and update downloads, and only when **Check for updates at startup** is enabled or you press **Check for updates** in Settings.

## Releases

Push a tag that matches the first three parts of the version in `source/app.manifest`. The current release tag is `v0.7.8`. The Windows workflow builds with `source/build.ps1`, packs the executable and the `assets` folder with Velopack, and publishes these files to GitHub Releases:

- `KavrosClueHelper-win-Setup.exe`
- `KavrosClueHelper-win-Portable.zip`
- `KavrosClueHelper-<version>-full.nupkg` (and any later delta packages)
- `releases.win.json`
