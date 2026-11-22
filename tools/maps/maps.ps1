<#
.SYNOPSIS
Draws run review maps of a fight, from PowerShell: its overview, its flagged moments, or any moment.

.DESCRIPTION
The designer's fights no longer draw maps when they end. They record the picture around the VNP all fight
long instead, into a compressed file beside the log, so any moment can be drawn afterwards with this.

The first argument is the fight: its folder in playtests/human/, its log, or latest (the default) for the
designer's most recent fight. The rest go to the map drawer (scripts/cheats/playtesting/DrawMaps.cs):

  strip=356              a strip around fight time 356 seconds (clock +0:56); several, comma-separated
  strip=350-358          a stretch of the fight, every picture from 350 to 358 seconds
  strip=356:1            pictures 1 second apart instead of the recording's half second
  overview=yes           the overview map too (drawn by default when no strip= is given)
  flagged=all            every flagged moment's strip too (or none, or indices such as 0,3)

Fight times are seconds from the start; the clock counts down from 5:00, so clock 2:00 is 180, and
clock +0:56 (past the limit) is 356. The maps go in the fight's folder.

Editor-only testing tool: nothing here is part of the game.

.EXAMPLE
.\tools\maps.cmd latest strip=356

.EXAMPLE
.\tools\maps.cmd playtests\human\2026-11-22_13-32-34_seed1281990368
#>
param(
	[Parameter(ValueFromRemainingArguments = $true)]
	[string[]]$DrawArgs
)

$project = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$godot = if ($env:GODOT) { $env:GODOT } else { 'F:\GameDev\godot\bin\godot.windows.editor.x86_64.mono.console.exe' }
if (-not (Test-Path $godot)) {
	Write-Error "Godot not found at $godot. Set the GODOT environment variable to the editor's console executable."
	exit 1
}

Push-Location $project
try {
	# Which fight: the first argument unless it is an option, else the designer's latest.
	$target = 'latest'
	$rest = @()
	if ($DrawArgs) {
		if ($DrawArgs[0] -notmatch '=') { $target = $DrawArgs[0]; $rest = @($DrawArgs | Select-Object -Skip 1) }
		else { $rest = $DrawArgs }
	}
	if ($target -eq 'latest') {
		$folder = Get-ChildItem 'playtests\human' -Directory | Sort-Object Name | Select-Object -Last 1
		if (-not $folder) { Write-Error 'No fights in playtests\human.'; exit 1 }
		$target = $folder.FullName
	}
	$target = (Resolve-Path $target -ErrorAction SilentlyContinue).Path
	if (-not $target) { Write-Error "No fight at $($DrawArgs[0])."; exit 1 }
	$log = if (Test-Path $target -PathType Container) {
		Get-ChildItem $target -Filter '*.json' | Where-Object { $_.Name -notlike '*.recording.*' } | Select-Object -First 1 -ExpandProperty FullName
	} else { $target }
	if (-not $log) { Write-Error "No fight log in $target."; exit 1 }

	dotnet build VNP.sln -nologo -v q | Out-Null
	if ($LASTEXITCODE -ne 0) {
		Write-Error 'The build failed; fix it first (dotnet build VNP.sln shows why).'
		exit 1
	}
	$godotArgs = @('--headless', '--path', '.', 'res://scripts/cheats/playtesting/draw_maps.tscn', '--', "log=$log") + $rest
	$noise = 'ObjectDB instances were leaked at exit|resources still in use at exit|ObjectDB::cleanup|ResourceCache::clear|^Godot Engine'
	& $godot @godotArgs 2>&1 | ForEach-Object { "$_" } | Where-Object { $_ -and $_ -notmatch $noise }
	$code = $LASTEXITCODE
}
finally {
	Pop-Location
}
exit $code
