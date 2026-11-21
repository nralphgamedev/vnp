#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// A testing aid: plays a fight set up by command-line arguments, prints what happens, and quits. It sets up
/// a situation that would take a long time to reach by playing, such as a crowd of Goos around the VNP, so
/// a change can be checked without writing a throwaway test. Editor only, like every cheat.
///
/// Run it from the project folder; add --headless unless you want to watch or take a screenshot:
///   godot --path . --fixed-fps 60 res://scripts/cheats/scenario.tscn -- spawn=goo:4 drones=40 frames=900
///
/// Arguments, all optional:
///   weapon=ID          equip this VNP Weapon instead of the first starting weapon (any from cheats.tres)
///   drone_weapon=ID    switch the drones to this Drone Weapon (any from cheats.tres)
///   spawn=ID:GROUPS[:DISTANCE]   spawn groups of this enemy type around the VNP, hunting it, at about this
///                      distance (default 15). Repeat for several types. ID "all" spawns that many groups
///                      of every enemy type.
///   drones=N           add N drones, raising the cap to fit
///   invulnerable       keep the VNP alive, for measuring
///   frames=N           how long to run (default 600, 10 seconds at 60 fps)
///   report=N           print a status line every N frames (default 60)
///   shot=PATH[@FRAME]  save a screenshot (default: the last frame). Needs a window, not --headless.
///
/// It reads the swarms' private lists by reflection, so the game's code needs nothing added for testing.
/// </summary>
public partial class ScenarioRunner : Node
{
	private Fight _fight;
	private int _frame;
	private int _frames = 600;
	private int _report = 60;
	private bool _invulnerable;
	private string _shotPath;
	private int _shotFrame = -1;
	private readonly List<(string Id, int Groups, float Distance)> _spawns = new();
	private int _extraDrones;
	private string _weaponId;
	private string _droneWeaponId;
	private ulong _lastTicks;
	private readonly List<double> _frameMs = new();
	private bool _quitting;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		ProcessPriority = -100; // Before the fight, so the frame timing covers the whole fight update.
		ParseArguments();
		// Release the scene once it is instantiated, so it isn't reported as still in use when the game quits.
		using (PackedScene scene = GD.Load<PackedScene>("res://scenes/Fight.tscn"))
			_fight = scene.Instantiate<Fight>();
		AddChild(_fight);
	}

	private void ParseArguments()
	{
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			string[] parts = argument.Split('=', 2);
			string value = parts.Length > 1 ? parts[1] : "";
			switch (parts[0])
			{
				case "weapon": _weaponId = value; break;
				case "drone_weapon": _droneWeaponId = value; break;
				case "drones": _extraDrones = int.Parse(value); break;
				case "invulnerable": _invulnerable = true; break;
				case "frames": _frames = int.Parse(value); break;
				case "report": _report = Math.Max(1, int.Parse(value)); break;
				case "spawn":
				{
					string[] spawn = value.Split(':');
					_spawns.Add((spawn[0], spawn.Length > 1 ? int.Parse(spawn[1]) : 1,
						spawn.Length > 2 ? float.Parse(spawn[2], System.Globalization.CultureInfo.InvariantCulture) : 15f));
					break;
				}
				case "shot":
				{
					string[] shot = value.Split('@');
					_shotPath = shot[0];
					_shotFrame = shot.Length > 1 ? int.Parse(shot[1]) : -1;
					break;
				}
				default:
					GD.PushWarning($"Scenario: unknown argument {argument}");
					break;
			}
		}
	}

	public override void _Process(double delta)
	{
		if (_quitting)
		{
			GetTree().Quit();
			return;
		}
		_frame++;
		ulong now = Time.GetTicksUsec();
		if (_frame > 3 && _lastTicks != 0)
			_frameMs.Add((now - _lastTicks) / 1000.0);
		_lastTicks = now;

		if (_frame == 2)
			SetUp();
		if (_frame > 2 && _frame % _report == 0)
			PrintStatus();
		if (_shotPath != null && _frame == (_shotFrame > 0 ? _shotFrame : _frames))
			SaveShot();

		if (_frame >= _frames || _fight.IsOver)
		{
			if (_frame % _report != 0)
				PrintStatus();
			PrintTiming();
			// Free the fight first and quit on the next frame, so it isn't reported as still in use at exit.
			// A headless run still reports the sounds that played as leaked at exit: its dummy audio driver
			// never finishes them. A windowed run exits clean, so those two exit messages can be ignored.
			_fight.QueueFree();
			_quitting = true;
		}
	}

	private void SetUp()
	{
		// The starting weapon cheat pauses the fight on its menu. Close it and equip a weapon, as a fight
		// without the cheat would.
		GetTree().Paused = false;
		if (_fight.GetNodeOrNull<Control>("Hud/StartingWeaponMenu") is Control menu)
			menu.Visible = false;
		CheatsTuning cheats = PlaytestCheats.Settings;
		WeaponDefinition weapon = _weaponId == null
			? Tuning.Vnp.StartingWeapons.FirstOrDefault()
			: cheats.StartingWeaponChoices.FirstOrDefault(w => w.Id == _weaponId);
		if (weapon == null)
			GD.PushError($"Scenario: no weapon {_weaponId} in cheats.tres StartingWeaponChoices.");
		if (_fight.WeaponCount == 0)
			_fight.Equip(weapon);
		if (_droneWeaponId != null)
		{
			DroneWeaponDefinition droneWeapon = cheats.StartingDroneWeaponChoices.FirstOrDefault(w => w.Id == _droneWeaponId);
			if (droneWeapon == null)
				GD.PushError($"Scenario: no Drone Weapon {_droneWeaponId} in cheats.tres StartingDroneWeaponChoices.");
			_fight.Drones.SwitchWeapon(droneWeapon);
		}

		// Armor's multiple on damage taken: at 0 the VNP still flashes when hit but loses no health.
		if (_invulnerable)
			_fight.Vnp.DamageTaken = 0f;

		if (_extraDrones > 0)
		{
			_fight.Drones.Cap = Math.Max(_fight.Drones.Cap, _fight.Drones.Count + _extraDrones);
			_fight.Drones.Add(_extraDrones, _fight.Vnp.PlanePosition);
		}

		MethodInfo spawnGroup = typeof(EnemySwarm).GetMethod("SpawnGroupAt", BindingFlags.NonPublic | BindingFlags.Instance);
		EnemySwarm swarm = _fight.Enemies;
		var spawns = _spawns.SelectMany(s => s.Id == "all"
			? swarm.Types.Select(t => (t.Definition.Id, s.Groups, s.Distance))
			: new[] { s }).ToList();
		foreach ((string id, int groups, float distance) in spawns)
		{
			int type = -1;
			for (int t = 0; t < swarm.Types.Count; t++)
			{
				if (swarm.Types[t].Definition.Id == id)
					type = t;
			}
			if (type < 0)
			{
				GD.PushError($"Scenario: no enemy type {id}. Types: {string.Join(", ", swarm.Types.Select(t => t.Definition.Id))}");
				continue;
			}
			for (int g = 0; g < groups; g++)
			{
				Vector2 home = _fight.Vnp.PlanePosition + Vector2.FromAngle(Mathf.Tau * g / groups) * distance;
				spawnGroup.Invoke(swarm, new object[] { home, type, true });
			}
		}
		GD.Print($"SCENARIO: weapon {weapon?.Id}, drones {_fight.Drones.Count} ({_fight.Drones.Weapon.Id}), " +
			$"spawned {string.Join(", ", spawns.Select(s => $"{s.Item2} {s.Item1} groups"))}");
	}

	private void PrintStatus()
	{
		EnemySwarm swarm = _fight.Enemies;
		var enemies = ((EnemySwarm.Enemy[])typeof(EnemySwarm)
			.GetField("_enemies", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(swarm))
			.Take(swarm.Count).Where(e => e.Health > 0f).ToArray();
		string byType = string.Join(" ", swarm.Types.Select((t, i) =>
		{
			var ofType = enemies.Where(e => e.Type == i).ToArray();
			string growth = ofType.Length > 0 && ofType.Any(e => e.Bulk > 0f)
				? $" (bulk max {ofType.Max(e => e.Bulk):0.0}, absorbed {ofType.Sum(e => e.Absorbed):0}, splitting {ofType.Count(e => e.SplitTimer > 0f)})"
				: "";
			return $"{t.Definition.Id} {ofType.Length}{growth}";
		}));
		GD.Print($"t {_fight.Elapsed:0.0}s  {byType}  drones {_fight.Drones.Count}/{_fight.Drones.Cap}  " +
			$"vnp {_fight.Vnp.Health:0}/{_fight.Vnp.MaxHealth:0}  kills {_fight.Kills}  level {_fight.Level}  " +
			$"particles {_fight.Effects.Count}");
	}

	private void PrintTiming()
	{
		if (_frameMs.Count == 0)
			return;
		var sorted = _frameMs.OrderBy(ms => ms).ToList();
		double Percentile(double p) => sorted[Math.Min(sorted.Count - 1, (int)(p * sorted.Count))];
		bool headless = DisplayServer.GetName() == "headless";
		GD.Print($"TIMING: {sorted.Count} frames, average {sorted.Average():0.00} ms, 95th percentile {Percentile(0.95):0.00} ms, " +
			$"worst {sorted[^1]:0.00} ms" + (headless
				? " (headless: game logic only, no rendering)"
				: " (windowed: includes rendering and waiting for vsync)"));
	}

	private void SaveShot()
	{
		Error error = GetViewport().GetTexture().GetImage().SavePng(_shotPath);
		GD.Print(error == Error.Ok ? $"SCREENSHOT: {_shotPath}" : $"SCREENSHOT failed: {error}");
	}
}
#endif
