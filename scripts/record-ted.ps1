<#
.SYNOPSIS
    Records a ted session with TUIcast using a caller-supplied keystroke script.

.DESCRIPTION
    Generic recording wrapper for the examples/ted app. An AI agent (or human)
    supplies the --Keystrokes parameter describing what to demonstrate, and this
    script handles tool resolution, building, and invoking tuicast record.

    Output goes to artifacts/tuicast/ by default. If tuicast or agg are not on
    PATH, they are automatically downloaded from
    https://github.com/gui-cs/TUIcast/releases and installed into ~/tools.

    See scripts/RECORDING-AGENT.md for the keystroke syntax reference and
    guidance on composing keystroke scripts for ted.

.PARAMETER Keystrokes
    TUIcast keystroke script (required). Comma-separated sequence of keys,
    text literals, and wait directives. See RECORDING-AGENT.md for syntax.

.PARAMETER Name
    Short identifier for the recording (used in output filenames).
    Default: "demo"

.PARAMETER Title
    Human-readable title burned into the cast metadata.
    Default: "ted demo"

.PARAMETER Output
    GIF output path. Default: artifacts/tuicast/ted-<Name>.gif

.PARAMETER CastOutput
    Asciinema .cast output path. Default: artifacts/tuicast/ted-<Name>.cast

.PARAMETER TedExePath
    Path to the ted executable. Default: auto-detected from build output.

.PARAMETER Cols
    Recording columns. Default: 120

.PARAMETER Rows
    Recording rows. Default: 36

.PARAMETER ShowCommand
    Synthetic shell prompt/command pre-roll shown in the GIF before the app
    starts (e.g. '$ ted foo.cs'). Omit for no pre-roll.

.PARAMETER StartupDelay
    Milliseconds to wait after the target process starts before copying its
    output and playing keystrokes. Default: 0 (no extra delay).

.PARAMETER InputDelay
    Default pause in milliseconds before the scripted keys begin (after
    startup-delay has elapsed). Default: 0.

.PARAMETER MaxDuration
    Maximum recording duration in seconds. Default: 60

.PARAMETER DrainMs
    Milliseconds to wait after last keystroke before stopping. Default: 1500

.PARAMETER Verbosity
    TUIcast verbosity level: low, medium, high. 'high' logs key tokens and
    pacing to stderr for troubleshooting. Default: not set.

.PARAMETER SkipBuild
    Skip dotnet build of examples/ted before recording.

.PARAMETER TuicastVersion
    TUIcast release version to download if not found. Default: 0.1.2
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string] $Keystrokes,

    [string] $Name = 'demo',
    [string] $Title = 'ted demo',
    [string] $Output,
    [string] $CastOutput,
    [string] $TedExePath,
    [int]    $Cols = 120,
    [int]    $Rows = 36,
    [int]    $MaxDuration = 60,
    [int]    $DrainMs = 1500,
    [string] $ShowCommand,
    [int]    $StartupDelay = 0,
    [int]    $InputDelay = 0,
    [string] $Verbosity,
    [switch] $SkipBuild,
    [string] $TuicastVersion = '0.1.2'
)

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$RepoRoot = git -C $ScriptDir rev-parse --show-toplevel
$RepoRoot = $RepoRoot.Trim()

$ToolsDir = Join-Path $HOME 'tools'

if (-not $Output) { $Output = Join-Path $RepoRoot "artifacts/tuicast/ted-${Name}.gif" }
if (-not $CastOutput) { $CastOutput = Join-Path $RepoRoot "artifacts/tuicast/ted-${Name}.cast" }

function Get-TuicastAssetName {
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

    $exeExt = if ($IsWindows -or ($PSVersionTable.PSVersion.Major -le 5)) { '.exe' } else { '' }
    foreach ($tool in @('tuicast', 'agg')) {
        $src = Get-ChildItem -Path $tempExtract -Recurse -Filter "${tool}${exeExt}" | Select-Object -First 1
        if ($src) {
            Copy-Item -Path $src.FullName -Destination (Join-Path $ToolsDir "${tool}${exeExt}") -Force
            Write-Host "  Installed: ~/tools/${tool}${exeExt}"
        }
    }

    Remove-Item -Recurse -Force $tempFile -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $tempExtract -ErrorAction SilentlyContinue
}

function Resolve-TuicastTool {
    param ([string] $ToolName)

    $exeExt = if ($IsWindows -or ($PSVersionTable.PSVersion.Major -le 5)) { '.exe' } else { '' }

    $found = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($found) { return $found.Source }

    $toolPath = Join-Path $ToolsDir "${ToolName}${exeExt}"
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

# Build if needed
if (-not $SkipBuild) {
    Write-Host 'Building examples/ted...'
    $buildResult = & dotnet build (Join-Path $RepoRoot 'examples/ted/ted.csproj') --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        $buildResult | ForEach-Object { Write-Host $_ }
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
}

if (-not (Test-Path $TedExePath)) {
    throw "ted executable not found at: $TedExePath`nBuild it first with: dotnet build examples/ted/ted.csproj"
}
$TedBin = (Resolve-Path $TedExePath).Path

# Ensure output directories exist
$null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Output)
$null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $CastOutput)

Set-Location $RepoRoot

Write-Host "Recording: $Title"
Write-Host "  Keystrokes: $Keystrokes"
Write-Host "  Output:     $Output"

$recordArgs = @(
    'record',
    '--binary', $TedBin,
    '--keystrokes', $Keystrokes,
    '--output', $Output,
    '--cast-output', $CastOutput,
    '--agg-path', $AggBin,
    '--cols', $Cols,
    '--rows', $Rows,
    '--max-duration', $MaxDuration,
    '--drain', $DrainMs,
    '--title', $Title
)

if ($ShowCommand)       { $recordArgs += '--show-command';   $recordArgs += $ShowCommand }
if ($StartupDelay -gt 0){ $recordArgs += '--startup-delay'; $recordArgs += $StartupDelay }
if ($InputDelay -gt 0)  { $recordArgs += '--input-delay';   $recordArgs += $InputDelay }
if ($Verbosity)         { $recordArgs += '--verbosity';     $recordArgs += $Verbosity }

& $TuicastBin @recordArgs

if ($LASTEXITCODE -ne 0) { throw "tuicast record failed with exit code $LASTEXITCODE" }

Write-Host ''
Write-Host "Recording complete:"
Write-Host "  GIF:  $Output"
Write-Host "  cast: $CastOutput"

$GifPath = (Resolve-Path $Output).Path

try
{
    Set-Clipboard -Value $GifPath
    Write-Host "  GIF path copied to clipboard."
}
catch
{
    Write-Host "  (Set-Clipboard unavailable: $($_.Exception.Message))"
}

try
{
    Invoke-Item -Path $GifPath
}
catch
{
    Write-Host "  (Could not launch GIF: $($_.Exception.Message))"
}
