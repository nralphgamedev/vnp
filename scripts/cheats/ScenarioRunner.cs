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
///   weapon=ID          equip this VNP Weapon instead of the first starting weapon (any from in_game_cheats.tres)
///   drone_weapon=ID    switch the drones to this Drone Weapon (any from in_game_cheats.tres)
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
///   (without a pilot, level-up offers are answered by taking the first option, half a second after each opens)
///   trace=N            every N frames, print a line describing the fight's exact state: where the VNP, the
///                      enemies and the drones are, and how many random numbers have been drawn. Two runs of
///                      the same fight should print the same lines; the first that differs shows where they parted.
///   explain=FROM-TO    print the pilot's reasoning at each of its decisions between these fight times in
///                      seconds, such as explain=110-131: the threat it felt against what it could afford, and
///                      what it chose to do. For finding out why it did something odd.
///   maps=yes           draw the run review maps when the fight log is written: an overview of the fight and a
///                      strip map of each flagged moment, in a maps folder beside the log
///   perf=plain|detail  measure against the performance requirement (performance.tres), in a window: vsync off and
///                      no frame cap, so frames run as fast as the computer allows. plain times whole frames by the
///                      clock with nothing extra switched on, and is what the requirement is judged on; detail also
///                      switches on Godot's CPU and GPU render timing and reports logic, render CPU and GPU times,
///                      which costs a little itself. Prints a PERF line with the slowest 1%, the worst frames with
///                      their fight times, and whether the requirement is met. Paused frames (menus) and the first
///                      SkipSeconds (loading) are left out.
///   profile=yes        time each part of the fight's update (UpdateOrder: the pilot, the VNP, harvestables, enemies,
///                      drones, projectiles, weapons, effects, ...) and print each part's average and slowest 1% at
///                      the end, from perfstart= on if given. Works headless: it times game logic only.
///   perfstart=SECONDS  with perf= or profile=, measure only from this fight time, such as perfstart=360 for a
///                      late, escalated fight: an invulnerable pilot plays there first, fast forwarded
///                      (FastForwardScale in performance.tres)
///   record=yes         record the whole fight's picture around the VNP, as the designer's fights do, into a
///                      compressed file beside the log, so tools\maps.cmd can draw any moment (for testing it)
///   strip=T1,T2        also save strips around these fight times in seconds, such as a moment spotted on an
///                      overview map; with maps=yes they are drawn too. Each entry can be T (pictures around that
///                      exact time, one at it), T:S (S seconds apart, such as 362:0.25), FROM-TO (a stretch,
///                      such as 355-364) or FROM-TO:S. A strip running past the VNP's destruction ends with a
///                      picture of that moment.
///   maxtime=SECONDS    stop the fight at this fight time, counted from its start, and log it as "capped"
///                      (it survived to the cap). Batches use it to stop endless runs.
///   logname=NAME       the fight log's file name, without .json, in place of its start time and seed
///   variant=NAME       apply the tuning variant tools/variants/NAME.txt before the fight starts (see TuningVariant)
///   take=ID,ID         take these upgrades from the pool (levels.tres) as the fight starts, after the weapons,
///                      such as take=orbital_shards_harvesting,orbital_shards_count, for testing an upgrade.
///                      Each is applied as if chosen at a level up, whether or not it would be offered.
///   asteroids=ID       the fight's asteroid amount (sparse, normal, rich: tuning/asteroid_amounts/), in place of
///                      the one picked by its seed
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
	private string _pilotId;
	private string _buildId = "greedy";
	private int _offerOpenFor;
	private LevelUpMenu _levelUpMenu;
	private string _logFolder;
	private string _asteroidAmount;
	private string[] _take;
	private int _exitCode;

	// perf=: each counted frame's clock time, fight time, whether the fight was paused, and in detail its logic,
	// render CPU and GPU times.
	private string _perf;
	// perf=detail: when the last frame's updates began and ended, marked by two nodes that run first and last.
	private ulong _logicStart, _logicEnd;
	private double _lastLogicMs;
	private double _lastTestingMs;
	// perf=: the fight time measuring starts at (perfstart=), such as 360 to measure a late, escalated fight only.
	private double _perfStart;
	private bool _fastForwarding;

	// profile=: one marker per update priority, run after every node of that priority (they come after the fight
	// in the tree), so the time between two markers is one part of the fight's update.
	private bool _profile;
	private static readonly (int Priority, string Name)[] ProfileParts =
	{
		(UpdateOrder.Fight, "fight"), (UpdateOrder.Pilots, "pilot"), (UpdateOrder.Vnp, "VNP and default-priority nodes"),
		(UpdateOrder.Harvestables, "harvestables"), (UpdateOrder.Enemies, "enemies"), (UpdateOrder.Drones, "drones"),
		(UpdateOrder.Projectiles, "projectiles"), (UpdateOrder.Weapons, "weapons"), (UpdateOrder.Effects, "effects"),
		(UpdateOrder.Audio, "audio"), (UpdateOrder.Camera, "camera"), (UpdateOrder.Ground, "ground"), (UpdateOrder.Hud, "HUD"),
	};
	private long _profileStart;
	private readonly long[] _profileMarks = new long[ProfileParts.Length];
	private readonly List<double>[] _profileTimes = ProfileParts.Select(_ => new List<double>()).ToArray();

	/// <summary>profile=: marks the end of one update priority's nodes, or with Index -1 the start of the frame.</summary>
	private sealed partial class ProfileMark : Node
	{
		public ScenarioRunner Runner;
		public int Index;

		public override void _Process(double delta)
		{
			long now = System.Diagnostics.Stopwatch.GetTimestamp();
			if (Index < 0)
			{
				Runner.CollectProfile();
				Runner._profileStart = now;
			}
			else
				Runner._profileMarks[Index] = now;
		}
	}

	/// <summary>The last frame's time in each part, from its markers, while the fight was running.</summary>
	private void CollectProfile()
	{
		if (_profileStart == 0 || _fight == null || GetTree().Paused || _fight.Elapsed < Math.Max(2, _perfStart))
			return;
		long previous = _profileStart;
		for (int i = 0; i < ProfileParts.Length; i++)
		{
			long mark = _profileMarks[i];
			_profileTimes[i].Add(mark > previous ? (mark - previous) * 1000.0 / System.Diagnostics.Stopwatch.Frequency : 0);
			if (mark > previous)
				previous = mark;
		}
	}

	private void StartProfile()
	{
		AddChild(new ProfileMark { Runner = this, Index = -1, ProcessPriority = int.MinValue + 1, ProcessMode = ProcessModeEnum.Always });
		for (int i = 0; i < ProfileParts.Length; i++)
			AddChild(new ProfileMark { Runner = this, Index = i, ProcessPriority = ProfileParts[i].Priority, ProcessMode = ProcessModeEnum.Always });
	}

	private void PrintProfile()
	{
		if (_profileTimes[0].Count == 0)
			return;
		Say($"PROFILE: {_profileTimes[0].Count} frames" + (_perfStart > 0 ? $" from {_perfStart:0}s" : "") +
			", each part of the fight's update: average / slowest 1% (ms)");
		for (int i = 0; i < ProfileParts.Length; i++)
		{
			var sorted = _profileTimes[i].OrderBy(v => v).ToList();
			double average = sorted.Average(), slowest = sorted[Math.Min(sorted.Count - 1, (int)(0.99 * sorted.Count))];
			if (slowest >= 0.01)
				Say($"PROFILE:   {ProfileParts[i].Name,-30} {average,6:0.00} / {slowest,6:0.00}");
		}
	}

	/// <summary>Marks a moment in each frame for perf=detail's logic timing: the first node to update, or the last.</summary>
	private sealed partial class FrameMark : Node
	{
		public ScenarioRunner Runner;
		public bool IsEnd;

		public override void _Process(double delta)
		{
			if (IsEnd)
				Runner._logicEnd = Time.GetTicksUsec();
			else
			{
				// The frame before: its updates ran from its start mark to its end mark, and testing code spent
				// TestingCost's total within them.
				Runner._lastLogicMs = Runner._logicEnd > Runner._logicStart ? (Runner._logicEnd - Runner._logicStart) / 1000.0 : 0;
				Runner._lastTestingMs = TestingCost.FrameMs;
				TestingCost.FrameMs = 0;
				Runner._logicStart = Time.GetTicksUsec();
			}
		}
	}
	private readonly List<(double Ms, double Time, bool Paused, double Logic, double Cpu, double Gpu, double Testing)> _perfFrames = new();

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		ProcessPriority = -100; // Before the fight, so the frame timing covers the whole fight update.
		ParseArguments();
		if (_argumentErrors.Count > 0)
		{
			foreach (string error in _argumentErrors)
				GD.PrintErr($"SCENARIO ERROR: {error}");
			GD.PrintErr($"SCENARIO ERROR: not playing; fix the arguments above (options: {string.Join(", ", Options.Keys)}).");
			_exitCode = 2;
			_quitting = true;
			return;
		}
		Fight.NextSeed = _seed;
		Fight.NextAsteroidAmount = _asteroidAmount;
		if (_perf != null)
			StartPerf();
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
			Say("SCENARIO: sound muted");
		}
		// Release the scene once it is instantiated, so it isn't reported as still in use when the game quits.
		using (PackedScene scene = GD.Load<PackedScene>("res://scenes/Fight.tscn"))
			_fight = scene.Instantiate<Fight>();
		// The runner keeps running while the game is paused, and a child copies that unless told otherwise, so
		// the fight would never pause: not for level-up offers, and not at the result screen. Pausable makes it
		// pause as it does in a normal game.
		_fight.ProcessMode = ProcessModeEnum.Pausable;
		AddChild(_fight);
		// After the fight, so each marker comes after that priority's fight nodes in the tree, and runs after them.
		if (_profile)
			StartProfile();
		// perfstart=: play fast up to the measuring start (see PerformanceSettings.FastForwardScale).
		if (_perfStart > 0 && (_perf != null || _profile))
		{
			var settings = GD.Load<PerformanceSettings>("res://scripts/cheats/playtesting/performance.tres") ?? new PerformanceSettings();
			Engine.TimeScale = settings.FastForwardScale;
			_fastForwarding = true;
		}
	}

	// What each option takes, for the message when one is mistyped or given a value it can't use.
	private static readonly Dictionary<string, string> Options = new()
	{
		["weapon"] = "a VNP Weapon ID from StartingWeaponChoices in in_game_cheats.tres",
		["drone_weapon"] = "a Drone Weapon ID from StartingDroneWeaponChoices in in_game_cheats.tres",
		["drones"] = "a whole number of extra drones",
		["invulnerable"] = "no value",
		["seed"] = "a whole number",
		["log"] = "a folder (res:// or a full path), or none",
		["trace"] = "a whole number of frames",
		["close"] = "yes or no",
		["maxtime"] = "a fight time in seconds",
		["maps"] = "yes or no",
		["record"] = "yes or no",
		["perf"] = "plain or detail, in a window",
		["perfstart"] = "a fight time in seconds to start measuring at, with perf= or profile=",
		["profile"] = "yes or no",
		["strip"] = "comma-separated T, T:S, FROM-TO or FROM-TO:S, in seconds",
		["logname"] = "a file name without .json",
		["mute"] = "yes or no",
		["variant"] = "a tuning variant's name, from tools/variants/NAME.txt, or base",
		["asteroids"] = "an asteroid amount ID from tuning/asteroid_amounts/",
		["take"] = "comma-separated upgrade IDs from levels.tres",
		["explain"] = "FROM-TO, fight times in seconds",
		["pilot"] = "a skill level from res://pilots/skill_levels/",
		["build"] = "a build from res://pilots/builds/",
		["frames"] = "a whole number of frames",
		["report"] = "a whole number of frames",
		["spawn"] = "ID[:GROUPS[:DISTANCE]], with an enemy ID or all",
		["shot"] = "PATH[@FRAME]",
	};

	private readonly List<string> _argumentErrors = new();

	/// <summary>
	/// Reads the arguments after --. Any that can't be used, such as a mistyped option, a value of the wrong
	/// kind or an ID that doesn't exist, is collected as an error, and the harness then quits without playing,
	/// since a run that ignored it would play a different fight from the one asked for.
	/// </summary>
	private void ParseArguments()
	{
		var culture = System.Globalization.CultureInfo.InvariantCulture;
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			string[] parts = argument.Split('=', 2);
			string name = parts[0];
			string value = parts.Length > 1 ? parts[1] : "";
			if (!Options.ContainsKey(name))
			{
				string near = Options.Keys.OrderBy(o => EditDistance(o, name)).First();
				_argumentErrors.Add($"unknown option {argument}" +
					(EditDistance(near, name) <= 3 ? $"; did you mean {near}?" : ""));
				continue;
			}
			try
			{
				switch (name)
				{
					case "weapon": _weaponId = value; break;
					case "drone_weapon": _droneWeaponId = value; break;
					case "drones": _extraDrones = int.Parse(value); break;
					case "invulnerable":
						if (value != "" && value != "yes")
							throw new FormatException();
						_invulnerable = true;
						break;
					case "seed": _seed = int.Parse(value); break;
					case "log": _logFolder = value; break;
					case "trace": _trace = Math.Max(1, int.Parse(value)); break;
					case "close": _close = YesNo(value); break;
					case "maxtime": _maxTime = double.Parse(value, culture); break;
					case "maps": FightLog.NextDrawMaps = YesNo(value); break;
					case "record": FightLog.NextRecord = YesNo(value); break;
					case "perfstart": _perfStart = double.Parse(value, culture); break;
					case "profile": _profile = YesNo(value); break;
					case "perf":
						if (value != "plain" && value != "detail")
							throw new FormatException();
						_perf = value;
						break;
					case "strip":
						FightLog.NextStripTimes = value.Split(',').Select(ReviewRecorder.StripRequest.Parse).ToList();
						break;
					case "logname": _logName = value; break;
					case "mute": _mute = YesNo(value); break;
					case "variant": _variant = value; break;
					case "asteroids": _asteroidAmount = value; break;
					case "take": _take = value.Split(',', StringSplitOptions.RemoveEmptyEntries); break;
					case "explain":
					{
						string[] window = value.Split('-');
						if (window.Length != 2)
							throw new FormatException();
						_explainFrom = float.Parse(window[0], culture);
						_explainTo = float.Parse(window[1], culture);
						break;
					}
					case "pilot": _pilotId = value; break;
					case "build": _buildId = value; break;
					case "frames": _frames = int.Parse(value); _framesGiven = true; break;
					case "report": _report = Math.Max(1, int.Parse(value)); _nextReport = _report / 60.0; break;
					case "spawn":
					{
						string[] spawn = value.Split(':');
						if (spawn.Length > 3 || spawn[0] == "")
							throw new FormatException();
						_spawns.Add((spawn[0], spawn.Length > 1 ? int.Parse(spawn[1]) : 1,
							spawn.Length > 2 ? float.Parse(spawn[2], culture) : 15f));
						break;
					}
					case "shot":
					{
						string[] shot = value.Split('@');
						if (shot[0] == "" || shot.Length > 2)
							throw new FormatException();
						_shotPath = shot[0];
						_shotFrame = shot.Length > 1 ? int.Parse(shot[1]) : -1;
						break;
					}
				}
			}
			catch (Exception e) when (e is FormatException or OverflowException or IndexOutOfRangeException)
			{
				_argumentErrors.Add($"{argument}: expected {Options[name]}");
			}
		}
		// A fight time to stop at, without a frame limit, should reach it: allow three times its frames, since
		// level-up offers pause the fight but still count frames. Without this, the 10-second default stopped it.
		// Measuring runs uncapped, with many more frames a second, so it sets no frame limit of its own.
		if (_maxTime > 0 && !_framesGiven)
			_frames = _perf != null || _profile ? int.MaxValue : (int)(_maxTime * 60 * 3);
		CheckIds();
	}

	private static bool YesNo(string value) => value switch
	{
		"yes" => true,
		"no" => false,
		_ => throw new FormatException(),
	};

	/// <summary>Checks every ID the arguments name against what exists, so a mistyped one stops the run.</summary>
	private void CheckIds()
	{
		void Check(string option, string id, IEnumerable<string> known)
		{
			List<string> ids = known.Where(k => k != null).ToList();
			if (id == null || ids.Contains(id))
				return;
			string near = ids.OrderBy(k => EditDistance(k, id)).FirstOrDefault();
			string hint = near != null && EditDistance(near, id) <= Math.Max(3, id.Length / 3) ? $"; did you mean {near}?" : "";
			// A long list, such as every upgrade, is left out in favour of where to find it.
			string choices = ids.Count <= 12 ? $"; choices: {string.Join(", ", ids)}" : "";
			_argumentErrors.Add($"{option}={id}: no such ID{hint}{choices}");
		}

		InGameCheatSettings cheats = InGameCheats.Settings;
		Check("weapon", _weaponId, cheats.StartingWeaponChoices.Select(w => w?.Id));
		Check("drone_weapon", _droneWeaponId, cheats.StartingDroneWeaponChoices.Select(w => w?.Id));
		Check("asteroids", _asteroidAmount, Tuning.Asteroids.Amounts.Select(a => a?.Id));
		foreach (string id in _take ?? Array.Empty<string>())
			Check("take", id, Tuning.Levels.Upgrades.Select(u => u?.Id));
		var enemies = Tuning.EnemySpawning.GroupWeights.Keys.Concat(Tuning.EnemySpawning.WaveGroupWeights.Keys)
			.Select(e => e?.Id).Distinct().Append("all");
		foreach ((string id, _, _) in _spawns)
			Check("spawn", id, enemies);
		if (_pilotId != null && !ResourceLoader.Exists($"res://pilots/skill_levels/{_pilotId}.tres"))
			_argumentErrors.Add($"pilot={_pilotId}: no res://pilots/skill_levels/{_pilotId}.tres");
		if (_pilotId != null && !ResourceLoader.Exists($"res://pilots/builds/{_buildId}.tres"))
			_argumentErrors.Add($"build={_buildId}: no res://pilots/builds/{_buildId}.tres");
		if (_variant != null && _variant != "base"
			&& !System.IO.File.Exists(System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), TuningVariant.Folder, _variant + ".txt")))
			_argumentErrors.Add($"variant={_variant}: no {TuningVariant.Folder}/{_variant}.txt");
		if (_perf != null && DisplayServer.GetName() == "headless")
			_argumentErrors.Add($"perf={_perf}: needs a window (run with -Window or --window)");
		if (_explainFrom > _explainTo)
			_argumentErrors.Add($"explain={_explainFrom}-{_explainTo}: FROM is after TO");
	}

	/// <summary>How many single-letter changes turn one word into another, for suggesting the option meant.</summary>
	private static int EditDistance(string a, string b)
	{
		var row = new int[b.Length + 1];
		for (int j = 0; j <= b.Length; j++)
			row[j] = j;
		for (int i = 1; i <= a.Length; i++)
		{
			int diagonal = row[0];
			row[0] = i;
			for (int j = 1; j <= b.Length; j++)
			{
				int above = row[j];
				row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
				diagonal = above;
			}
		}
		return row[b.Length];
	}

	// Timed as testing work (TestingCost), so the performance requirement is judged on the game's own time.
	public override void _Process(double delta)
	{
		long started = TestingCost.Begin();
		try
		{
			ProcessFrame();
		}
		finally
		{
			TestingCost.End(started);
		}
	}

	private void ProcessFrame()
	{
		if (_fastForwarding && _fight != null && _fight.Elapsed >= _perfStart - 1)
		{
			// A second before the measuring starts, back to normal speed, so measured frames have normal steps.
			Engine.TimeScale = 1;
			_fastForwarding = false;
		}
		if (_quitting)
		{
			// Clean up before quitting, as closing the game's window does (see ShutdownCleanup).
			FightLog.WaitForWrites();
			ShutdownCleanup.Run();
			GetTree().Quit(_exitCode);
			return;
		}
		if (_finished)
			return; // Staying open on the final screen until the window is closed.
		_frame++;
		ulong now = Time.GetTicksUsec();
		if (_frame > 3 && _lastTicks != 0)
		{
			_frameMs.Add((now - _lastTicks) / 1000.0);
			if (_perf != null && _fight != null)
				RecordPerfFrame((now - _lastTicks) / 1000.0);
		}
		_lastTicks = now;

		if (_frame == 2)
			SetUp();
		TakeUpgrade();
		PrintLevelUps();
		ExplainPilot();
		// Status lines on fight time, so pauses for level-up offers don't shift them. Not while perf= measures:
		// building one costs a few milliseconds of the harness's own, which would be measured as a slow frame.
		if (_frame > 2 && _perf == null && _fight.Elapsed >= _nextReport)
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
			Say($"SCENARIO: won at {_fight.Elapsed:0.0}s, carrying on into endless mode");
		}
		if (_fight.IsOver && _overFrame == 0)
			_overFrame = _frame;
		bool overLongEnough = _overFrame > 0 && _frame - _overFrame >= (_shotPath != null ? 45 : 1);
		// A set fight time reached before the fight ended: it survived to the cap.
		if (!_capped && _maxTime > 0 && !_fight.IsOver && _fight.Elapsed >= _maxTime)
		{
			_capped = true;
			_fight.FightLog?.FightCapped();
			Say($"SCENARIO: reached the cap of {_maxTime:0}s of fight time");
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
				Say("SCENARIO: finished; close the window to quit");
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
		if (_fight.IsOver || _fight.Pilot != null)
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
		InGameCheatSettings cheats = InGameCheats.Settings;
		WeaponDefinition weapon = _weaponId == null
			? Tuning.Vnp.StartingWeapons.FirstOrDefault()
			: cheats.StartingWeaponChoices.FirstOrDefault(w => w.Id == _weaponId);
		if (weapon == null)
			GD.PushError($"Scenario: no weapon {_weaponId} in in_game_cheats.tres StartingWeaponChoices.");
		if (_fight.WeaponCount == 0)
			_fight.Equip(weapon);
		if (_droneWeaponId != null)
		{
			DroneWeaponDefinition droneWeapon = cheats.StartingDroneWeaponChoices.FirstOrDefault(w => w.Id == _droneWeaponId);
			if (droneWeapon == null)
				GD.PushError($"Scenario: no Drone Weapon {_droneWeaponId} in in_game_cheats.tres StartingDroneWeaponChoices.");
			_fight.Drones.SwitchWeapon(droneWeapon);
		}

		foreach (string id in _take ?? Array.Empty<string>())
		{
			UpgradeDefinition upgrade = Tuning.Levels.Upgrades.FirstOrDefault(u => u != null && u.Id == id);
			if (upgrade == null)
				GD.PushError($"Scenario: no upgrade {id} in levels.tres.");
			else
				_fight.ApplyUpgrade(upgrade);
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
		Say($"SCENARIO: seed {_fight.Seed}, pilot {(_fight.Pilot == null ? "none" : $"{_fight.Pilot.Skill.Id}/{_fight.Pilot.Build.Id}")}, weapon {weapon?.Id}, drones {_fight.Drones.Count} ({_fight.Drones.Weapon.Id}), " +
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
		Say($"t {_fight.Elapsed:0.0}s  {byType}  drones {_fight.Drones.Count}/{_fight.Drones.Cap}  " +
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
				Say($"  rerolled{(levelUp.FreeRerolls > 0 ? " (free)" : "")} at level {levelUp.Level}: " +
					$"{Names(levelUp.Offers[_rerollsPrinted])}  ->  {Names(levelUp.Offers[_rerollsPrinted + 1])}");
			}
			if (levelUp.Taken != null)
			{
				Say($"LEVEL {levelUp.Level} at {When(levelUp.Time)}: took {NameOf(levelUp.Taken)}" +
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
			Say($"  {time:0.00}s  {pilot.Reasoning}");
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
		Say($"TRACE {_frame} t {_fight.Elapsed:0.000} paused {GetTree().Paused} draws {Rng.Draws} " +
			$"vnp {vnp.X:0.0000},{vnp.Y:0.0000} enemies {swarm.Count} {enemySum:0.0000} drones {_fight.Drones.Count} " +
			$"kills {_fight.Kills} level {_fight.Level}");
	}

	// perf=: the harness's own lines, held until the measurement ends, since a print costs 10 ms or more when
	// the output goes to a console, and would be measured as a hitch.
	private readonly List<string> _heldLines = new();

	/// <summary>Prints a line, or holds it while perf= is measuring.</summary>
	private void Say(string text)
	{
		if (_perf != null && !_finishedMeasuring)
			_heldLines.Add(text);
		else
			GD.Print(text);
	}

	private bool _finishedMeasuring;

	/// <summary>perf=: frames as fast as the computer allows, and in detail Godot's render timing.</summary>
	private void StartPerf()
	{
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
		Engine.MaxFps = 0;
		// The start mark collects each frame's testing time, in both modes; the end mark times logic, in detail.
		AddChild(new FrameMark { Runner = this, ProcessPriority = int.MinValue, ProcessMode = ProcessModeEnum.Always });
		if (_perf == "detail")
		{
			RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
			// Logic is timed from the first node's update to the last's, every node in the tree in between.
			AddChild(new FrameMark { Runner = this, IsEnd = true, ProcessPriority = int.MaxValue, ProcessMode = ProcessModeEnum.Always });
		}
		Vector2I size = DisplayServer.WindowGetSize();
		GD.Print($"PERF: measuring ({_perf}) at {size.X}x{size.Y}, vsync off, no frame cap; the harness's own lines follow afterwards");
		RunReview.Say = Say;
	}

	private void RecordPerfFrame(double ms)
	{
		double logic = 0, cpu = 0, gpu = 0;
		if (_perf == "detail")
		{
			Rid viewport = GetViewport().GetViewportRid();
			// The last frame's updates (this runs early in the next frame, before the marks update again).
			logic = _lastLogicMs;
			cpu = RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport) + RenderingServer.GetFrameSetupTimeCpu();
			gpu = RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport);
		}
		_perfFrames.Add((ms, _fight.Elapsed, GetTree().Paused, logic, cpu, gpu, _lastTestingMs));
	}

	/// <summary>The PERF lines: the slowest 1% of frames against the budget, hitches, the worst frames with their
	/// fight times, and in detail where the time went.</summary>
	private void PrintPerf()
	{
		_finishedMeasuring = true;
		foreach (string line in _heldLines)
			GD.Print(line);
		_heldLines.Clear();
		var settings = GD.Load<PerformanceSettings>("res://scripts/cheats/playtesting/performance.tres") ?? new PerformanceSettings();
		var frames = _perfFrames.Where(f => !f.Paused && f.Time >= Math.Max(settings.SkipSeconds, _perfStart)).ToList();
		if (frames.Count == 0)
		{
			Say("PERF: no frames counted (all paused or within the loading seconds)");
			return;
		}
		double Slowest(IEnumerable<double> values)
		{
			var sorted = values.OrderBy(v => v).ToList();
			return sorted[Math.Min(sorted.Count - 1, (int)((1 - settings.SlowestShare) * sorted.Count))];
		}
		// The game's own time: each frame less the testing code's work in it (TestingCost), which an exported game
		// doesn't do. A frame over budget only because of testing work is tagged, and doesn't fail the requirement.
		double Game(double ms, double testing) => Math.Max(0, ms - testing);
		double slowest = Slowest(frames.Select(f => Game(f.Ms, f.Testing)));
		int overBudget = frames.Count(f => Game(f.Ms, f.Testing) > settings.FrameBudgetMs);
		int hitches = frames.Count(f => Game(f.Ms, f.Testing) > settings.HitchMs);
		int tagged = frames.Count(f => f.Ms > settings.FrameBudgetMs && Game(f.Ms, f.Testing) <= settings.FrameBudgetMs);
		Say($"PERF: {frames.Count} frames" + (_perfStart > 0 ? $" from {_perfStart:0}s" : "") +
			$", game's own time: average {frames.Average(f => Game(f.Ms, f.Testing)):0.00} ms, " +
			$"slowest {settings.SlowestShare * 100:0.#}% {slowest:0.00} ms ({1000 / Math.Max(0.01, slowest):0} fps), " +
			$"worst {frames.Max(f => Game(f.Ms, f.Testing)):0.00} ms; {overBudget} frames over {settings.FrameBudgetMs:0.##} ms, " +
			$"{hitches} over {settings.HitchMs:0.#} ms. Testing code averaged {frames.Average(f => f.Testing):0.00} ms a frame; " +
			$"{tagged} frames over budget only because of it (tagged as testing, not failing)");
		Say("PERF: worst frames at fight time " + string.Join(", ",
			frames.OrderByDescending(f => f.Ms).Take(8).Select(f => $"{f.Time:0.00}s {f.Ms:0.0} ms" +
				(f.Testing >= 0.5 ? $" of which testing {f.Testing:0.0}" : "") +
				(_perf == "detail" ? $" (logic {f.Logic:0.0}, CPU {f.Cpu:0.0}, GPU {f.Gpu:0.0})" : "") +
				(f.Ms > settings.FrameBudgetMs && Game(f.Ms, f.Testing) <= settings.FrameBudgetMs ? " [testing]" : ""))));
		bool met = slowest <= settings.FrameBudgetMs && hitches == 0;
		if (_perf == "detail")
		{
			double logic = Slowest(frames.Select(f => Game(f.Logic, f.Testing)));
			Say($"PERF: slowest {settings.SlowestShare * 100:0.#}% by part: game logic {logic:0.00} ms (budget {settings.LogicBudgetMs:0.##}), " +
				$"render CPU {Slowest(frames.Select(f => f.Cpu)):0.00} ms, GPU {Slowest(frames.Select(f => f.Gpu)):0.00} ms; " +
				$"averages logic {frames.Average(f => f.Logic):0.00}, render CPU {frames.Average(f => f.Cpu):0.00}, GPU {frames.Average(f => f.Gpu):0.00} ms");
			met &= logic <= settings.LogicBudgetMs;
		}
		Say(met ? "PERF: requirement MET" : "PERF: requirement NOT MET");
	}

	private void PrintTiming()
	{
		if (_frameMs.Count == 0)
			return;
		var sorted = _frameMs.OrderBy(ms => ms).ToList();
		double Percentile(double p) => sorted[Math.Min(sorted.Count - 1, (int)(p * sorted.Count))];
		bool headless = DisplayServer.GetName() == "headless";
		if (_perf != null)
			PrintPerf();
		if (_profile)
			PrintProfile();
		Say($"TIMING: {sorted.Count} frames, average {sorted.Average():0.00} ms, 95th percentile {Percentile(0.95):0.00} ms, " +
			$"worst {sorted[^1]:0.00} ms" + (headless
				? " (headless: game logic only, no rendering)"
				: " (windowed: includes rendering and waiting for vsync)"));
	}

	private void SaveShot()
	{
		Error error = GetViewport().GetTexture().GetImage().SavePng(_shotPath);
		Say(error == Error.Ok ? $"SCREENSHOT: {_shotPath}" : $"SCREENSHOT failed: {error}");
	}
}
#endif
