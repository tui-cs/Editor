# Stop hook: run code cleanup on .cs files modified in this session, then re-stage.
# Cheap when nothing changed; bounded when something did. Output is ≤5 lines so we
# don't pollute the agent's transcript.
#
# Order matters:
#   1. dotnet format (fast, .editorconfig-driven)
#   2. dotnet jb cleanupcode (slower, ReSharper-driven; catches what dotnet format misses —
#      var preferences, expression-bodied members, XML doc spacing, using sorting)
#
# Skips itself entirely if the working tree has no modified .cs files outside third_party/.

$ErrorActionPreference = 'Stop'

$repo = & git rev-parse --show-toplevel 2>$null
if (-not $repo) { exit 0 }
Set-Location $repo

# Modified .cs files (staged + unstaged + untracked), excluding lifted upstream code.
$changed = & git status --porcelain |
    ForEach-Object { ($_ -replace '^...', '').Trim('"') } |
    Where-Object { $_ -like '*.cs' -and $_ -notlike 'third_party/*' }

if (-not $changed) { exit 0 }

# Restore tools quietly. Idempotent; first run downloads jb.
& dotnet tool restore --tool-manifest .config/dotnet-tools.json *> $null

# dotnet format (whitespace + style + analyzers) on the whole solution. Single pass is faster
# than per-file invocations because the workspace loads once.
& dotnet format Terminal.Gui.Text.slnx --no-restore --exclude third_party/ *> $null

# ReSharper code cleanup. Uses the built-in profile name because jb cleanupcode does not
# always discover custom profile names from team-shared .DotSettings files reliably; the
# .DotSettings file's style settings (var preferences, expression-bodied, ConvertToAutoProperty
# suppression for the C# 13 `field` keyword, etc.) are still honored by the built-in profile.
# --include narrows the work to changed files.
$includes = ($changed | ForEach-Object { "--include=$_" }) -join ' '
if ($includes) {
    & dotnet jb cleanupcode Terminal.Gui.Text.slnx --profile="Built-in: Full Cleanup" $includes.Split(' ') --no-build *> $null
}

# Surface the net effect so the agent sees its own drift.
$after = & git status --porcelain | Where-Object { $_ -like '* *.cs' -and $_ -notlike '* third_party/*' }
if ($after) {
    Write-Host "Code cleanup adjusted $($after.Count) file(s) to match house style."
}
exit 0
