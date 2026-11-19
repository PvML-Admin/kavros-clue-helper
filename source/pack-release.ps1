$ErrorActionPreference = 'Stop'
# Velopack CLI. Keep this version aligned with the Velopack package in build.ps1.
$vpkVersion = '1.2.161'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$tag = $env:RELEASE_TAG
if ([string]::IsNullOrWhiteSpace($tag)) { throw 'Set RELEASE_TAG to the release tag, for example v0.7.3.' }
if ($tag -notmatch '^v(\d+\.\d+\.\d+)$') { throw "Tag '$tag' must look like v0.7.3." }
$version = $Matches[1]
$identity = [string]([xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot 'app.manifest') -Raw)).assembly.assemblyIdentity.version
$manifestVersion = ([Version]$identity).ToString(3)
if ($version -ne $manifestVersion) { throw "Tag $tag does not match app.manifest version $identity. The pack version stays $manifestVersion." }

$exe = Join-Path $root 'KavrosClueHelper.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'KavrosClueHelper.exe is missing. Run source/build.ps1 first.' }
$stage = Join-Path $root 'publish'
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item -LiteralPath $exe -Destination (Join-Path $stage 'KavrosClueHelper.exe')
foreach ($dll in @('Velopack.dll', 'Newtonsoft.Json.dll')) {
    $path = Join-Path $root $dll
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing $dll beside KavrosClueHelper.exe. Run source/build.ps1 first." }
    Copy-Item -LiteralPath $path -Destination (Join-Path $stage $dll)
}
$config = Join-Path $PSScriptRoot 'KavrosClueHelper.exe.config'
if (-not (Test-Path -LiteralPath $config)) { throw 'Missing source/KavrosClueHelper.exe.config.' }
Copy-Item -LiteralPath $config -Destination (Join-Path $stage 'KavrosClueHelper.exe.config')
Copy-Item -LiteralPath (Join-Path $root 'assets') -Destination (Join-Path $stage 'assets') -Recurse
foreach ($doc in @('README.txt', 'LICENSE.txt', 'THIRD_PARTY.txt', 'PATCH-NOTES.md', 'CHANGELOG.md')) {
    Copy-Item -LiteralPath (Join-Path $root $doc) -Destination (Join-Path $stage $doc)
}
# Velopack and the GitHub release use this version's CHANGELOG section, not the whole history.
$changelog = [IO.File]::ReadAllText((Join-Path $root 'CHANGELOG.md'))
$match = [regex]::Match($changelog, "(?ms)^## $([regex]::Escape($version))\r?\n(.*?)(?=^## |\z)")
if (-not $match.Success) { throw "CHANGELOG.md has no section for $version." }
$notesPath = Join-Path $root 'release-notes.md'
[IO.File]::WriteAllText($notesPath, ("## {0}`r`n{1}`r`n" -f $version, $match.Groups[1].Value.Trim()))

$out = Join-Path $root 'release-out'
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
$vpk = Join-Path $env:USERPROFILE ".dotnet\tools\vpk.exe"
if (-not (Test-Path -LiteralPath $vpk)) { $vpk = 'vpk' }
$splash = if ($env:VPK_SPLASH) { Join-Path $root $env:VPK_SPLASH } else { Join-Path $root 'assets\branding\kavros-splash.png' }
if (-not (Test-Path -LiteralPath $splash)) { throw "Missing splash image $splash." }
& $vpk pack `
    --packId KavrosClueHelper `
    --packVersion $version `
    --packDir $stage `
    --mainExe KavrosClueHelper.exe `
    --packTitle 'Kavros Clue Helper' `
    --packAuthors Zenity `
    --icon (Join-Path $root 'assets\branding\clue-scroll.ico') `
    --splashImage $splash `
    --framework net48 `
    --releaseNotes $notesPath `
    --outputDir $out
if ($LASTEXITCODE -ne 0) { throw "vpk $vpkVersion pack failed." }
Get-ChildItem -LiteralPath $out | ForEach-Object { Write-Output $_.FullName }
