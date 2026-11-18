$ErrorActionPreference = 'Stop'

function Get-PinnedPackage($packages, $id, $version, $dllRelativePath) {
    $dest = Join-Path $packages "$id.$version"
    $dll = Join-Path $dest $dllRelativePath
    if (Test-Path -LiteralPath $dll) { return $dll }
    New-Item -ItemType Directory -Force -Path $packages | Out-Null
    if (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest -Recurse -Force }
    $nupkg = Join-Path $packages "$id.$version.nupkg"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/$id/$version" -OutFile $nupkg
    $zip = Join-Path $packages "$id.$version.zip"
    Copy-Item -LiteralPath $nupkg -Destination $zip -Force
    Expand-Archive -LiteralPath $zip -DestinationPath $dest -Force
    Remove-Item -LiteralPath $zip -Force
    if (-not (Test-Path -LiteralPath $dll)) { throw "Package $id $version did not contain $dllRelativePath." }
    return $dll
}

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$frameworkDirectory = Split-Path -Parent $compiler
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The Windows .NET Framework C# compiler was not found. Enable .NET Framework 4.8 or build with a compatible C# compiler.'
}
Push-Location $PSScriptRoot
try {
    $outputDirectory = $PSScriptRoot
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'assets')) -and (Test-Path -LiteralPath (Join-Path $PSScriptRoot '..\assets'))) {
        $outputDirectory = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
    }
    $outputFile = Join-Path $outputDirectory 'KavrosClueHelper.exe'
    # Velopack 1.2.161 targets net472 and is the update/installer library.
    # Keep this version aligned with the vpk tool in pack-release.ps1.
    $packages = Join-Path $PSScriptRoot 'packages'
    $velopackDll = Get-PinnedPackage $packages 'Velopack' '1.2.161' 'lib\net472\Velopack.dll'
    $jsonDll = Get-PinnedPackage $packages 'Newtonsoft.Json' '13.0.4' 'lib\net45\Newtonsoft.Json.dll'
    # Pin references to the installed Windows .NET Framework. A compiler's
    # newer default mscorlib can emit methods absent from the target runtime.
    $references = @('mscorlib.dll','System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xml.dll','System.Xml.Linq.dll','System.Runtime.WindowsRuntime.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Web.dll') |
        ForEach-Object { '/reference:' + (Join-Path $frameworkDirectory $_) }
    $references += '/reference:' + (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Runtime\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Runtime.dll')
    # Add these as two arguments. A comma inside @() binds tighter than +,
    # so one array literal concatenates both paths into a single /reference.
    $references += '/reference:' + $velopackDll
    $references += '/reference:' + $jsonDll
    $references += @('Windows.Foundation','Windows.Media','Windows.Graphics','Windows.Storage','Windows.Globalization') | ForEach-Object { '/reference:' + (Join-Path $env:WINDIR ('System32\WinMetadata\'+$_+'.winmd')) }
    $identity = ([xml](Get-Content -LiteralPath 'app.manifest' -Raw)).assembly.assemblyIdentity
    $version = [Version]::Parse([string]$identity.version).ToString(4)
    $metadata = Join-Path ([IO.Path]::GetTempPath()) ('Kavros-Version-' + [Guid]::NewGuid().ToString('N') + '.cs')
    $art = Join-Path $outputDirectory 'assets\branding'
    $scroll = Join-Path $art 'clue-scroll.ico'
    $crest = Join-Path $art 'kavros-crest.png'
    $resources = @("/win32icon:$scroll", "/resource:$scroll,Kavros.Branding.clue-scroll.ico", "/resource:$crest,Kavros.Branding.kavros-crest.png")
    try {
        [IO.File]::WriteAllText($metadata, ('[assembly:System.Reflection.AssemblyVersion("{0}")] [assembly:System.Reflection.AssemblyFileVersion("{0}")]' -f $version))
        & $compiler /nologo /noconfig /nostdlib+ /langversion:5 /codepage:65001 /target:winexe /platform:anycpu /optimize+ "/out:$outputFile" /win32manifest:app.manifest $references $resources $metadata *.cs
        if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
        Copy-Item -LiteralPath $velopackDll -Destination (Join-Path $outputDirectory 'Velopack.dll') -Force
        Copy-Item -LiteralPath $jsonDll -Destination (Join-Path $outputDirectory 'Newtonsoft.Json.dll') -Force
    } finally { [IO.File]::Delete($metadata) }
    Write-Output $outputFile
} finally {
    Pop-Location
}
