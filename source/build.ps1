$ErrorActionPreference = 'Stop'
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
    # Pin references to the installed Windows .NET Framework. A compiler's
    # newer default mscorlib can emit methods absent from the target runtime.
    $references = @('mscorlib.dll','System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xml.dll','System.Xml.Linq.dll','System.Runtime.WindowsRuntime.dll') |
        ForEach-Object { '/reference:' + (Join-Path $frameworkDirectory $_) }
    $references += '/reference:' + (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Runtime\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Runtime.dll')
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
    } finally { [IO.File]::Delete($metadata) }
    Write-Output $outputFile
} finally {
    Pop-Location
}
