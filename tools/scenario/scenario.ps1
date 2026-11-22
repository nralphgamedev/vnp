<#
.SYNOPSIS
Plays a fight set up by the scenario harness (res://scripts/cheats/scenario.tscn), from PowerShell.

.DESCRIPTION
Builds the game, then runs one scenario fight and prints what happens. It runs headless unless -Window is
given, so add -Window to watch it. Every argument after the switches goes to the harness; ScenarioRunner.cs
lists them all. The most useful:

  pilot=average          a pilot flies the VNP: novice, average or expert (pilots/skill_levels/)
  build=greedy           its build (pilots/builds/): greedy, random, designer, drone_swarm, weapon_focus,
                         area or tank
  variant=NAME           apply a tuning variant from tools/variants/, such as gentle_levels
  seed=N                 repeat a fight exactly
  weapon=ID              the starting VNP Weapon, such as scatter_shot
  drone_weapon=ID        the starting Drone Weapon: projectile, beam or ram
  spawn=goo:3            spawn enemy groups around the VNP (goo, basic_enemy, or all)
  frames=N               how long to run, at 60 frames a second (default 600, 10 seconds)
  log=res://playtests/x  write the fight log to that folder instead (log=none for no log). A pilot
                         fight is logged to playtests/pilot/ unless told otherwise.
  shot=C:\path\shot.png  save a screenshot (needs -Window)
  explain=110-131        print the pilot's reasoning at each decision between these fight times, in seconds
                         (fight time, not the clock: clock 3:30 is 90 seconds in)
  close=yes              with -Window, quit when the fight finishes; by default the window stays open on the
                         final screen until you close it

Editor-only testing tool: nothing here is part of the game.

.EXAMPLE
.\tools\scenario.cmd -Window pilot=average build=greedy frames=20000

.EXAMPLE
.\tools\scenario.cmd pilot=average build=random seed=3 frames=20000 report=1800
#>
param(
	# Show the fight in a window, at normal speed, instead of running it headless.
	[switch]$Window,
	# Arguments for the harness, such as pilot=average seed=3.
	[Parameter(ValueFromRemainingArguments = $true)]
	[string[]]$ScenarioArgs
)

$project = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$godot = if ($env:GODOT) { $env:GODOT } else { 'F:\GameDev\godot\bin\godot.windows.editor.x86_64.mono.console.exe' }
if (-not (Test-Path $godot)) {
	Write-Error "Godot not found at $godot. Set the GODOT environment variable to the editor's console executable."
	exit 1
}

Push-Location $project
try {
	dotnet build VNP.sln -nologo -v q | Out-Null
	if ($LASTEXITCODE -ne 0) {
		Write-Error 'The build failed; fix it first (dotnet build VNP.sln shows why).'
		exit 1
	}

	$godotArgs = @()
	if (-not $Window) { $godotArgs += '--headless' }
	$godotArgs += @('--path', '.', '--fixed-fps', '60', 'res://scripts/cheats/scenario.tscn', '--')
	if ($ScenarioArgs) { $godotArgs += $ScenarioArgs }

	# A headless run reports the sounds that played as leaked when it quits, because its dummy audio driver
	# never finishes them. Those lines, and Godot's banner, are left out.
	$noise = 'ObjectDB instances were leaked at exit|resources still in use at exit|ObjectDB::cleanup|ResourceCache::clear|^Godot Engine'
	& $godot @godotArgs 2>&1 | ForEach-Object { "$_" } | Where-Object { $_ -and $_ -notmatch $noise }
}
finally {
	Pop-Location
}
