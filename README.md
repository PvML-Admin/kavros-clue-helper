# kavros-clue-helper

Clue scroll helper overlay for Kavros. Version 0.7.22 is a Windows .NET Framework 4.8 application. The full guide is in [README.txt](README.txt).

v0.7.22: Master scans in Prifddinas and the Heart of Gielinor are read from the scroll text, the gnomeball referee card includes its challenge answer, and a helper window behind the game is no longer painted over the capture. See [CHANGELOG.md](CHANGELOG.md).

The helper reads the game window locally and does not send game input. Maps and clue references are bundled offline. The only runtime network request is to GitHub, for update checks and update downloads, and only when **Check for updates at startup** is enabled or you press **Check for updates** in Settings.

## Releases

Push a tag that matches the first three parts of the version in `source/app.manifest`. The current release tag is `v0.7.22`. The Windows workflow builds with `source/build.ps1`, packs the executable and the `assets` folder with Velopack, and publishes these files to GitHub Releases:

- `KavrosClueHelper-win-Setup.exe`
- `KavrosClueHelper-win-Portable.zip`
- `KavrosClueHelper-<version>-full.nupkg` (and any later delta packages)
- `releases.win.json`
