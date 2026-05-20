<#
.SYNOPSIS
    Records ted opening ./examples/ted/TedApp.cs with TUIcast.

.DESCRIPTION
    Records the examples/ted app starting, opens the File menu, chooses Open,
    types ./examples/ted/TedApp.cs into the OpenDialog, and saves:

      artifacts/tuicast/ted-open.gif
      artifacts/tuicast/ted-open.cast

    If tuicast or agg are not found on PATH, they are automatically downloaded
    from https://github.com/gui-cs/TUIcast/releases and installed into ~/tools.

.PARAMETER Output
    GIF output path (default: artifacts/tuicast/ted-open.gif)

.PARAMETER CastOutput
    asciinema cast output path (default: artifacts/tuicast/ted-open.cast)

.PARAMETER TedExePath
    Path to the ted executable (default: examples/ted/bin/Debug/net10.0/ted.exe)

.PARAMETER Cols
    Recording columns (default: 120)

.PARAMETER Rows
    Recording rows (default: 36)

.PARAMETER Keystrokes
    Override the TUIcast keystroke script

.PARAMETER TuicastVersion
    TUIcast release version to download if not found (default: 0.1.1)
#>
[CmdletBinding()]
param (
    [string] $Output,
    [string] $CastOutput,
    [string] $TedExePath,
    [int]    $Cols = 120,
    [int]    $Rows = 36,
    [string] $Keystrokes,
    [string] $TuicastVersion = '0.1.1'
)

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$RepoRoot = git -C $ScriptDir rev-parse --show-toplevel
$RepoRoot = $RepoRoot.Trim()

$ToolsDir = Join-Path $HOME 'tools'

if (-not $Output) { $Output = Join-Path $RepoRoot 'artifacts/tuicast/ted-open.gif' }
if (-not $CastOutput) { $CastOutput = Join-Path $RepoRoot 'artifacts/tuicast/ted-open.cast' }
if (-not $Keystrokes) {
    $Keystrokes = if ($env:KEYSTROKES) { $env:KEYSTROKES }
    else { 'wait:2000,10,CursorDown,CursorDown,Enter,wait:500,./examples/ted/TedApp.cs,Enter,wait:2000,PageDown,wait:2000,Esc' }
}

$MaxDuration = 45
$DrainMs = 1000

function Get-TuicastAssetName {
    # Determine the correct release archive name for the current platform
    if ($IsWindows -or ($PSVersionTable.PSVersion.Major -le 5)) {
        $os = 'windows'
        $ext = 'zip'
    } elseif ($IsMacOS) {
        $os = 'darwin'
        $ext = 'tar.gz'
    } else {
        $os = 'linux'
        $ext = 'tar.gz'
    }

    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLower()
    $goArch = switch ($arch) {
        'x64'   { 'amd64' }
        'arm64' { 'arm64' }
        default { 'amd64' }
    }

    return "tuicast_${TuicastVersion}_${os}_${goArch}.${ext}"
}

function Install-TuicastTools {
    $null = New-Item -ItemType Directory -Force -Path $ToolsDir

    $asset = Get-TuicastAssetName
    $url = "https://github.com/gui-cs/TUIcast/releases/download/v${TuicastVersion}/${asset}"
    $tempFile = Join-Path ([System.IO.Path]::GetTempPath()) $asset

    Write-Host "Downloading TUIcast v${TuicastVersion}: $url"
    Invoke-WebRequest -Uri $url -OutFile $tempFile -UseBasicParsing

    $tempExtract = Join-Path ([System.IO.Path]::GetTempPath()) "tuicast-extract-$([guid]::NewGuid())"
    $null = New-Item -ItemType Directory -Force -Path $tempExtract

    if ($asset.EndsWith('.zip')) {
        Expand-Archive -Path $tempFile -DestinationPath $tempExtract -Force
    } else {
        tar -xzf $tempFile -C $tempExtract
    }

    # Copy tuicast and agg executables to ~/tools
    $exeExt = if ($IsWindows -or ($PSVersionTable.PSVersion.Major -le 5)) { '.exe' } else { '' }
    foreach ($tool in @('tuicast', 'agg')) {
        $src = Get-ChildItem -Path $tempExtract -Recurse -Filter "${tool}${exeExt}" | Select-Object -First 1
        if ($src) {
            Copy-Item -Path $src.FullName -Destination (Join-Path $ToolsDir "${tool}${exeExt}") -Force
            Write-Host "  Installed: ~/tools/${tool}${exeExt}"
        }
    }

    # Cleanup
    Remove-Item -Recurse -Force $tempFile -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $tempExtract -ErrorAction SilentlyContinue
}

function Resolve-TuicastTool {
    param ([string] $Name)

    $exeExt = if ($IsWindows -or ($PSVersionTable.PSVersion.Major -le 5)) { '.exe' } else { '' }

    # Check PATH
    $found = Get-Command $Name -ErrorAction SilentlyContinue
    if ($found) { return $found.Source }

    # Check ~/tools
    $toolPath = Join-Path $ToolsDir "${Name}${exeExt}"
    if (Test-Path $toolPath) { return (Resolve-Path $toolPath).Path }

    return $null
}

# Resolve tuicast and agg — download if missing
$TuicastBin = Resolve-TuicastTool 'tuicast'
$AggBin = Resolve-TuicastTool 'agg'

if (-not $TuicastBin -or -not $AggBin) {
    Write-Host 'tuicast or agg not found on PATH or in ~/tools. Installing...'
    Install-TuicastTools
    $TuicastBin = Resolve-TuicastTool 'tuicast'
    $AggBin = Resolve-TuicastTool 'agg'
    if (-not $TuicastBin) { throw 'Failed to install tuicast' }
    if (-not $AggBin) { throw 'Failed to install agg' }
}

# Resolve ted executable
$exeExt = if ($IsWindows -or ($PSVersionTable.PSVersion.Major -le 5)) { '.exe' } else { '' }
if (-not $TedExePath) {
    $TedExePath = Join-Path $RepoRoot "examples/ted/bin/Debug/net10.0/ted${exeExt}"
}
if (-not (Test-Path $TedExePath)) {
    throw "ted executable not found at: $TedExePath`nBuild it first with: dotnet build examples/ted/ted.csproj"
}
$TedBin = (Resolve-Path $TedExePath).Path

# Ensure output directories exist
$null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Output)
$null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $CastOutput)

Set-Location $RepoRoot

& $TuicastBin record `
    --binary $TedBin `
    --keystrokes $Keystrokes `
    --output $Output `
    --cast-output $CastOutput `
    --agg-path $AggBin `
    --cols $Cols `
    --rows $Rows `
    --max-duration $MaxDuration `
    --drain $DrainMs `
    --title 'ted opens TedApp.cs'

if ($LASTEXITCODE -ne 0) { throw "tuicast record failed with exit code $LASTEXITCODE" }

Write-Host ''
Write-Host 'Recorded ted File.Open flow:'
Write-Host "  GIF:  $Output"
Write-Host "  cast: $CastOutput"
