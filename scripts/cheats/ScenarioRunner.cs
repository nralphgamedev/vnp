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
///   frames=N           how long to run (default 600, 10 seconds at 60 fps; with maxtime, long enough to reach it)
///   report=N           print a status line every N frames of fight time (default 1800: every 30 seconds of
///                      fight, not counting pauses). Each level-up,
///                      and each reroll, is printed as it happens, whatever this is.
///   shot=PATH[@FRAME]  save a screenshot (default: the last frame). Needs a window, not --headless.
///   seed=N             play the fight from this seed, so it can be repeated
///   pilot=ID           a pilot flies the VNP, with this skill level from res://pilots/skill_levels/. It
///                      picks its own upgrades, and after a win carries on into endless mode.
///   build=ID           the pilot's build, from res://pilots/builds/ (default greedy)
///   upgrades=first|none   without a pilot: answer level-up offers by taking the first option, half a second after each
///                      opens (the default), or leave the fight paused on the offer
///   trace=N            every N frames, print a line describing the fight's exact state: where the VNP, the
///                      enemies and the drones are, and how many random numbers have been drawn. Two runs of
///                      the same fight should print the same lines; the first that differs shows where they parted.
///   explain=FROM-TO    print the pilot's reasoning at each of its decisions between these fight times in
///                      seconds, such as explain=110-131: the threat it felt against what it could afford, and
///                      what it chose to do. For finding out why it did something odd.
///   maps=yes           draw the run review maps when the fight log is written: an overview of the fight and a
///                      strip map of each flagged moment, in a maps folder beside the log
///   strip=T1,T2        also save strips around these fight times in seconds, such as a moment spotted on an
///                      overview map; with maps=yes they are drawn too. Each entry can be T (pictures around that
///                      exact time, one at it), T:S (S seconds apart, such as 362:0.25), FROM-TO (a stretch,
///                      such as 355-364) or FROM-TO:S. A strip running past the VNP's destruction ends with a
///                      picture of that moment.
///   maxtime=SECONDS    stop the fight at this fight time, counted from its start, and log it as "capped"
///                      (it survived to the cap). Batches use it to stop endless runs.
///   logname=NAME       the fight log's file name, without .json, in place of its start time and seed
///   variant=NAME       apply the tuning variant tools/variants/NAME.txt before the fight starts (see TuningVariant)
///   mute=yes           silence all sound, as batches do whenever they run more than one fight
///   close=yes          in a window, quit when the fight finishes instead of staying open on the final screen.
///                      Headless runs, and runs taking a screenshot, always quit when finished.
///   log=FOLDER         write a fight log there (res:// or a full path), or log=none for no log. By default,
///                      a pilot fight is logged to res://playtests/pilot, and a fight without a pilot isn't
///                      logged. Scenario fights never land among the designer's own logs.
///
/// It reads the swarms' private lists by reflection, so the game's code needs nothing added for testing.
/// </summary>
public partial class ScenarioRunner : Node
{
	private Fight _fight;
	private int _frame;
	private int _frames = 600;
	private bool _framesGiven;
	private int _report = 1800;
	// Level-ups already printed, and rerolls already printed for the latest one.
	private int _levelUpsPrinted;
	private double _nextReport = 30.0;
	private double _lastStatusTime = -1;
	private int _rerollsPrinted;
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
	private int? _seed;
	private int _overFrame;
	private int _trace;
	private bool _finished;
	private bool _close;
	private double _maxTime = -1;
	private string _logName;
	private bool _mute;
	private string _variant;
	private bool _capped;
	private float _explainFrom = -1f;
	private float _explainTo = -1f;
	private int _decisionsExplained;
	private bool _takeUpgrades = true;
	private string _pilotId;
	private string _buildId = "greedy";
	private int _offerOpenFor;
	private LevelUpMenu _levelUpMenu;
	private string _logFolder;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		ProcessPriority = -100; // Before the fight, so the frame timing covers the whole fight update.
		ParseArguments();
		Fight.NextSeed = _seed;
		FightLog.NextFolder = _logFolder == "none" ? ""
			: _logFolder ?? (_pilotId != null ? "res://playtests/pilot" : "");
		// A pilot fight logged to playtests/pilot gets a folder of its own, like the designer's fights.
		FightLog.NextOwnFolder = _logFolder == null && _pilotId != null;
		FightLog.NextPlayer = "scenario";
		FightLog.NextFileName = _logName;
		if (_variant != null && _variant != "base")
		{
			// Before the fight is made, so everything in it reads the changed values.
			FightLog.NextVariant = _variant;
			FightLog.NextVariantText = TuningVariant.Apply(_variant);
		}
		if (_mute)
		{
			AudioServer.SetBusMute(AudioServer.GetBusIndex("Master"), true);
			GD.Print("SCENARIO: sound muted");
		}
		// Release the scene once it is instantiated, so it isn't reported as still in use when the game quits.
		using (PackedScene scene = GD.Load<PackedScene>("res://scenes/Fight.tscn"))
			_fight = scene.Instantiate<Fight>();
		// The runner keeps running while the game is paused, and a child copies that unless told otherwise, so
		// the fight would never pause: not for level-up offers, and not at the result screen. Pausable makes it
		// pause as it does in a normal game.
		_fight.ProcessMode = ProcessModeEnum.Pausable;
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
				case "seed": _seed = int.Parse(value); break;
				case "log": _logFolder = value; break;
				case "trace": _trace = Math.Max(1, int.Parse(value)); break;
				case "close": _close = value != "no"; break;
				case "maxtime": _maxTime = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
				case "maps": FightLog.NextDrawMaps = value == "yes"; break;
				case "strip":
					FightLog.NextStripTimes = value.Split(',').Select(ReviewRecorder.StripRequest.Parse).ToList();
					break;
				case "logname": _logName = value; break;
				case "mute": _mute = value != "no"; break;
				case "variant": _variant = value; break;
				case "explain":
				{
					string[] window = value.Split('-');
					_explainFrom = float.Parse(window[0], System.Globalization.CultureInfo.InvariantCulture);
					_explainTo = float.Parse(window[1], System.Globalization.CultureInfo.InvariantCulture);
					break;
				}
				case "upgrades": _takeUpgrades = value != "none"; break;
				case "pilot": _pilotId = value; break;
				case "build": _buildId = value; break;
				case "frames": _frames = int.Parse(value); _framesGiven = true; break;
				case "report": _report = Math.Max(1, int.Parse(value)); _nextReport = _report / 60.0; break;
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
		// A fight time to stop at, without a frame limit, should reach it: allow three times its frames, since
		// level-up offers pause the fight but still count frames. Without this, the 10-second default stopped it.
		if (_maxTime > 0 && !_framesGiven)
			_frames = (int)(_maxTime * 60 * 3);
	}

	public override void _Process(double delta)
	{
		if (_quitting)
		{
			// Clean up before quitting, as closing the game's window does (see ShutdownCleanup).
			FightLog.WaitForWrites();
			ShutdownCleanup.Run();
			GetTree().Quit();
			return;
		}
		if (_finished)
			return; // Staying open on the final screen until the window is closed.
		_frame++;
		ulong now = Time.GetTicksUsec();
		if (_frame > 3 && _lastTicks != 0)
			_frameMs.Add((now - _lastTicks) / 1000.0);
		_lastTicks = now;

		if (_frame == 2)
			SetUp();
		TakeUpgrade();
		PrintLevelUps();
		ExplainPilot();
		// Status lines on fight time, so pauses for level-up offers don't shift them.
		if (_frame > 2 && _fight.Elapsed >= _nextReport)
		{
			PrintStatus();
			_lastStatusTime = _fight.Elapsed;
			_nextReport += _report / 60.0;
		}
		if (_trace > 0 && _frame % _trace == 0)
			PrintTrace();
		// When the fight ends, wait a moment so the result screen is drawn, then finish. A screenshot with no
		// frame given is taken then, so it shows the result screen.
		// A pilot never stops at a win: it carries on into endless mode, as the spec asks.
		if (_fight.IsOver && _fight.Pilot != null && _fight.LastOutcome == Fight.Outcome.Win && !_fight.IsEndless)
		{
			if (_fight.GetNodeOrNull<Control>("Hud/ResultScreen") is Control resultScreen)
				resultScreen.Visible = false;
			_fight.ContinueEndless();
			GD.Print($"SCENARIO: won at {_fight.Elapsed:0.0}s, carrying on into endless mode");
		}
		if (_fight.IsOver && _overFrame == 0)
			_overFrame = _frame;
		bool overLongEnough = _overFrame > 0 && _frame - _overFrame >= (_shotPath != null ? 45 : 1);
		// A set fight time reached before the fight ended: it survived to the cap.
		if (!_capped && _maxTime > 0 && !_fight.IsOver && _fight.Elapsed >= _maxTime)
		{
			_capped = true;
			_fight.FightLog?.FightCapped();
			GD.Print($"SCENARIO: reached the cap of {_maxTime:0}s of fight time");
		}
		bool finishing = _frame >= _frames || overLongEnough || _capped;
		if (_shotPath != null && (_frame == _shotFrame || (_shotFrame <= 0 && finishing)))
			SaveShot();

		if (finishing)
		{
			if (_fight.Elapsed != _lastStatusTime)
				PrintStatus();
			PrintTiming();
			// Watched in a window, it stays open on the final screen until the window is closed: the result screen
			// if the fight ended, or the fight paused where the frame limit stopped it.
			if (StaysOpen())
			{
				_finished = true;
				if (!_fight.IsOver)
					GetTree().Paused = true;
				GD.Print("SCENARIO: finished; close the window to quit");
				return;
			}
			// Free the fight first and quit on the next frame, so it isn't reported as still in use at exit.
			// A headless run still reports the sounds that played as leaked at exit: its dummy audio driver
			// never finishes them. A windowed run exits clean, so those two exit messages can be ignored.
			_fight.QueueFree();
			_quitting = true;
		}
	}

	/// <summary>
	/// Takes the first option of an open level-up offer once it has been open half a second, so a scenario
	/// keeps going through level-ups instead of stopping at the first. The pause while it is open is real, as
	/// in play.
	/// </summary>
	private void TakeUpgrade()
	{
		if (!_takeUpgrades || _fight.IsOver || _fight.Pilot != null)
			return;
		_levelUpMenu ??= _fight.GetNodeOrNull<LevelUpMenu>("Hud/LevelUpMenu");
		if (_levelUpMenu == null || !_levelUpMenu.IsOpen)
		{
			_offerOpenFor = 0;
			return;
		}
		if (++_offerOpenFor >= 30)
		{
			_levelUpMenu.Choose(0);
			_offerOpenFor = 0;
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

		if (_pilotId != null)
		{
			var skill = GD.Load<PilotDefinition>($"res://pilots/skill_levels/{_pilotId}.tres");
			var build = GD.Load<BuildDefinition>($"res://pilots/builds/{_buildId}.tres");
			if (skill == null || build == null)
				GD.PushError($"Scenario: no pilot skill level {_pilotId} or build {_buildId} in res://pilots/.");
			else
				_fight.Pilot = Pilot.Fly(_fight, _fight.Vnp, _fight.Drones, skill, build);
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
		GD.Print($"SCENARIO: seed {_fight.Seed}, pilot {(_fight.Pilot == null ? "none" : $"{_fight.Pilot.Skill.Id}/{_fight.Pilot.Build.Id}")}, weapon {weapon?.Id}, drones {_fight.Drones.Count} ({_fight.Drones.Weapon.Id}), " +
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
			$"vnp {_fight.Vnp.Health:0}/{_fight.Vnp.MaxHealth:0}  " +
			$"gateway {_fight.Vnp.PlanePosition.DistanceTo(_fight.Gateway.PlanePosition):0} away  " +
			$"kills {_fight.Kills}  level {_fight.Level}  " +
			$"particles {_fight.Effects.Count}");
	}

	/// <summary>
	/// Prints each level-up once its upgrade is taken: when, what was offered and what was taken. Each reroll is
	/// printed as it happens, with the offer before and after. Times are given as fight time and as the clock on
	/// screen, which counts down from the time limit.
	/// </summary>
	private void PrintLevelUps()
	{
		var levelUps = _fight.Stats.LevelUps;
		if (levelUps.Count == 0)
			return;
		int latest = levelUps.Count - 1;
		if (latest >= _levelUpsPrinted)
		{
			FightStats.LevelUp levelUp = levelUps[latest];
			for (; _rerollsPrinted < levelUp.Rerolls; _rerollsPrinted++)
			{
				GD.Print($"  rerolled{(levelUp.FreeRerolls > 0 ? " (free)" : "")} at level {levelUp.Level}: " +
					$"{Names(levelUp.Offers[_rerollsPrinted])}  ->  {Names(levelUp.Offers[_rerollsPrinted + 1])}");
			}
			if (levelUp.Taken != null)
			{
				GD.Print($"LEVEL {levelUp.Level} at {When(levelUp.Time)}: took {NameOf(levelUp.Taken)}" +
					$"   (offered {Names(levelUp.Offers[^1])})");
				_levelUpsPrinted = latest + 1;
				_rerollsPrinted = 0;
			}
		}
	}

	/// <summary>The pilot's reasoning at each new decision inside the explain window.</summary>
	private void ExplainPilot()
	{
		Pilot pilot = _fight.Pilot;
		if (pilot == null || _explainFrom < 0f || pilot.Decisions == _decisionsExplained)
			return;
		_decisionsExplained = pilot.Decisions;
		double time = _fight.Elapsed;
		if (time >= _explainFrom && time <= _explainTo)
			GD.Print($"  {time:0.00}s  {pilot.Reasoning}");
	}

	private static string When(double time)
	{
		double clock = Tuning.Fight.TimeLimitSeconds - time;
		string onScreen = clock >= 0 ? Ui.Clock(clock) : $"+{Ui.Clock(-clock)}";
		return $"{Ui.Clock(time)} (clock {onScreen})";
	}

	private static string NameOf(string upgradeId)
	{
		foreach (UpgradeDefinition upgrade in Tuning.Levels.Upgrades)
		{
			if (upgrade != null && upgrade.Id == upgradeId)
				return upgrade.Name;
		}
		return upgradeId;
	}

	private static string Names(List<string> upgradeIds) => string.Join(", ", upgradeIds.Select(NameOf));

	/// <summary>Whether to stay open on the final screen: when watched in a window, unless asked to close or taking
	/// a screenshot (which other tools wait on).</summary>
	private bool StaysOpen() => DisplayServer.GetName() != "headless" && !_close && _shotPath == null;

	/// <summary>The fight's exact state, for comparing two runs that should match.</summary>
	private void PrintTrace()
	{
		EnemySwarm swarm = _fight.Enemies;
		var enemies = (EnemySwarm.Enemy[])typeof(EnemySwarm)
			.GetField("_enemies", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(swarm);
		double enemySum = 0;
		for (int i = 0; i < swarm.Count; i++)
			enemySum += enemies[i].Position.X * 1.3 + enemies[i].Position.Y + enemies[i].Health * 0.01;
		Vector2 vnp = _fight.Vnp.PlanePosition;
		GD.Print($"TRACE {_frame} t {_fight.Elapsed:0.000} paused {GetTree().Paused} draws {Rng.Draws} " +
			$"vnp {vnp.X:0.0000},{vnp.Y:0.0000} enemies {swarm.Count} {enemySum:0.0000} drones {_fight.Drones.Count} " +
			$"kills {_fight.Kills} level {_fight.Level}");
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
