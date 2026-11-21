#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Writes a fight log: one JSON file per fight, holding its settings, its fight statistics, a timeline
/// sampled through the fight, the player's movement input, and frame times. The designer's fights in the
/// editor are logged to playtests/human/, so their results can be analysed and pilots calibrated against
/// them. Editor only, like the cheats: an exported game writes no logs.
///
/// The file is written when the fight ends, and again if an endless run then ends, so it always holds the
/// whole fight. A fight left unfinished, by starting a new one or closing the game, is written as unfinished.
/// Its settings are in res://scripts/cheats/playtesting/fight_log.tres.
/// </summary>
public partial class FightLog : Node
{
	/// <summary>
	/// For the next fight only: the folder to log it to instead of the usual one, or "" to not log it. The
	/// scenario harness and batches use this, so their fights don't land among the designer's.
	/// </summary>
	public static string NextFolder;

	/// <summary>For the next fight only: who is playing, written in the log. The designer, unless set.</summary>
	public static string NextPlayer;

	public static FightLogSettings Settings => _settings ??= LoadSettings();

	private static FightLogSettings _settings;
	private static string _gitVersion;
	private static string _tuningFingerprint;

	private Fight _fight;
	private string _folder;
	private string _player;
	private string _path;
	private readonly DateTime _startedAt = DateTime.Now;
	private double _nextSample;
	private double _nextInput;
	private bool _recordInput;
	private List<string> _startingWeapons;
	private string _startingDroneWeapon;
	private readonly List<Dictionary<string, object>> _timeline = new();
	private readonly List<float[]> _input = new();
	private readonly List<double> _frameMs = new();
	private int _mostEnemies;
	private int _mostDrones;
	private Label _note;
	private ulong _frameStart;
	private ulong _frameStartFrame;

	/// <summary>
	/// Marks the start of each frame's fight update. The log itself runs last and marks the end, so the time
	/// between is the game's own work, without drawing or waiting for the screen.
	/// </summary>
	private sealed partial class FrameStart : Node
	{
		public FightLog Log;

		public override void _Process(double delta)
		{
			Log._frameStart = Time.GetTicksUsec();
			Log._frameStartFrame = Engine.GetProcessFrames();
		}
	}

	/// <summary>Adds a fight log to a fight, unless logs are off or this fight asked not to be logged.</summary>
	public static FightLog Attach(Fight fight)
	{
		string folder = NextFolder ?? Settings.Folder;
		string player = NextPlayer ?? "designer";
		NextFolder = null;
		NextPlayer = null;
		if (!Settings.WriteLogs || string.IsNullOrEmpty(folder))
			return null;

		var log = new FightLog { Name = "FightLog", _fight = fight, _folder = folder, _player = player };
		// Before everything else in the fight, and the log after everything, to time the fight's update.
		fight.AddChild(new FrameStart { Name = "FightLogFrameStart", Log = log, ProcessPriority = int.MinValue });
		log.ProcessPriority = int.MaxValue;
		// Only a person's movement is worth recording: it is the data for making pilots play more like one.
		log._recordInput = player == "designer";
		fight.AddChild(log);
		return log;
	}

	public override void _Process(double delta)
	{
		// The fight clock only runs while the fight does, so this skips the paused starting weapon menu.
		double now = _fight.Elapsed;
		// The starting weapons, once they're equipped: by the fight itself, by the starting weapon cheat before
		// the clock starts, or by the scenario harness a frame in.
		if (_startingWeapons == null && (_fight.WeaponCount > 0 || now > 1.0))
		{
			_startingWeapons = _fight.Weapons.Select(w => w.Definition.Id).ToList();
			_startingDroneWeapon = _fight.Drones.Weapon.Id;
		}

		// Samples keep to a fixed cadence, rather than being scheduled from when the last one happened to land,
		// which would let every sample slip a frame later.
		if (now >= _nextSample)
		{
			Sample(now);
			_nextSample = NextOnCadence(_nextSample, now, Math.Max(0.05f, Settings.TimelineSeconds));
		}
		if (_recordInput && now >= _nextInput)
		{
			Vector2 move = Input.GetVector("move_left", "move_right", "move_up", "move_down", Tuning.Vnp.StickDeadzone);
			_input.Add(new[] { (float)Math.Round(now, 2), (float)Math.Round(move.X, 2), (float)Math.Round(move.Y, 2) });
			_nextInput = NextOnCadence(_nextInput, now, 1.0 / Math.Max(1f, Settings.InputSamplesPerSecond));
		}

		// Only when the start was marked this same frame. When a level-up offer is chosen, the fight resumes
		// partway through a frame whose start was never marked, and the last mark is from before the offer,
		// so measuring from it would count the time spent choosing as one long frame.
		if (_frameStart != 0 && _frameStartFrame == Engine.GetProcessFrames())
			_frameMs.Add((Time.GetTicksUsec() - _frameStart) / 1000.0);
		_mostEnemies = Math.Max(_mostEnemies, _fight.Enemies.Count);
		_mostDrones = Math.Max(_mostDrones, _fight.Drones.Count);
	}

	/// <summary>The next time on a fixed cadence after <paramref name="now"/>.</summary>
	private static double NextOnCadence(double scheduled, double now, double step)
	{
		double next = scheduled + step;
		return next > now ? next : now + step;
	}

	private void Sample(double now)
	{
		int[] counts = _fight.Enemies.CountByType();
		var enemies = new Dictionary<string, object>();
		for (int t = 0; t < counts.Length; t++)
			enemies[_fight.Enemies.Types[t].Definition.Id] = counts[t];
		_timeline.Add(new Dictionary<string, object>
		{
			["time"] = Math.Round(now, 1),
			["health"] = Math.Round(_fight.Vnp.Health, 1),
			["maxHealth"] = Math.Round(_fight.Vnp.MaxHealth, 1),
			["level"] = _fight.Level,
			["experience"] = Math.Round(_fight.Experience, 1),
			["drones"] = _fight.Drones.Count,
			["enemies"] = enemies,
			["kills"] = _fight.Kills,
		});
	}

	/// <summary>Writes the log for a fight that has just ended. Called by the fight.</summary>
	public void FightEnded(Fight.Outcome outcome)
	{
		Sample(_fight.Elapsed);
		Write(outcome.ToString().ToLowerInvariant());
		ShowNote();
	}

	public override void _ExitTree()
	{
		// Left without ending, by starting a new fight or closing the game, it is still worth keeping.
		if (!_fight.IsOver && _fight.Elapsed > 1.0)
		{
			// A last sample, so the timeline ends where the fight did, as it does for a fight that ended.
			Sample(_fight.Elapsed);
			Write("unfinished");
		}
	}

	private void Write(string result)
	{
		try
		{
			string folder = ProjectSettings.GlobalizePath(_folder);
			Directory.CreateDirectory(folder);
			KeepOutOfImport(folder);
			_path ??= Path.Combine(folder, $"{_startedAt:yyyy-MM-dd_HH-mm-ss}_seed{_fight.Seed}.json");
			File.WriteAllText(_path, JsonSerializer.Serialize(Build(result), new JsonSerializerOptions { WriteIndented = true }));
		}
		catch (Exception error)
		{
			GD.PushError($"Could not write the fight log: {error.Message}");
		}
	}

	/// <summary>A .gdignore file in playtests/ keeps Godot from importing the logs as project files.</summary>
	private static void KeepOutOfImport(string folder)
	{
		string root = ProjectSettings.GlobalizePath("res://playtests");
		if (!folder.Replace('\\', '/').StartsWith(root.Replace('\\', '/')))
			return;
		Directory.CreateDirectory(root);
		string ignore = Path.Combine(root, ".gdignore");
		if (!File.Exists(ignore))
			File.WriteAllText(ignore, "");
	}

	private Dictionary<string, object> Build(string result)
	{
		FightStats stats = _fight.Stats;
		var sorted = _frameMs.Where(ms => ms > 0).OrderBy(ms => ms).ToList();
		double Percentile(double p) => sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)(p * sorted.Count))];

		return new Dictionary<string, object>
		{
			["logVersion"] = 1,
			["settings"] = new Dictionary<string, object>
			{
				["seed"] = _fight.Seed,
				["player"] = _player,
				["pilot"] = _fight.Pilot?.Skill.Id,
				["build"] = _fight.Pilot?.Build.Id,
				["startingWeapons"] = _startingWeapons ?? new List<string>(),
				["startingDroneWeapon"] = _startingDroneWeapon,
				["tuningVariant"] = null,
				["tuningFingerprint"] = TuningFingerprint(),
				["gitVersion"] = GitVersion(),
				["startedAt"] = _startedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
				["cheats"] = new Dictionary<string, object>
				{
					["chooseStartingWeapon"] = PlaytestCheats.ChoosesStartingWeapon,
					["freeRerolls"] = stats.LevelUps.Sum(l => l.FreeRerolls),
				},
			},
			["outcome"] = new Dictionary<string, object>
			{
				["result"] = result,
				["endless"] = _fight.IsEndless,
				["time"] = Math.Round(_fight.Elapsed, 1),
				["kills"] = _fight.Kills,
				["killTarget"] = Tuning.Fight.KillTarget,
				["killTargetTime"] = _fight.IsEndless || result == "win" ? Math.Round(_fight.VictoryTime, 1) : (object)null,
				["level"] = _fight.Level,
				["experience"] = Math.Round(_fight.Experience, 1),
				["weaponsAtEnd"] = _fight.Weapons.Select(w => w.Definition.Id).ToList(),
				["droneWeaponAtEnd"] = _fight.Drones.Weapon.Id,
				["upgradesTaken"] = _fight.TakenUpgrades.Select(u => u.Id).ToList(),
			},
			["stats"] = new Dictionary<string, object>
			{
				["totalDamage"] = Math.Round(stats.TotalDamage, 1),
				["damage"] = stats.Damage.Select(d => new Dictionary<string, object>
				{
					["source"] = d.Source.Id,
					["kind"] = d.Source.Kind.ToString(),
					["total"] = Math.Round(d.Total, 1),
					["byEnemy"] = d.ByEnemy.ToDictionary(e => e.Key, e => (object)Math.Round(e.Value, 1)),
				}).ToList(),
				["drones"] = new Dictionary<string, object>
				{
					["started"] = stats.DronesStarted,
					["built"] = stats.DronesBuilt,
					["destroyed"] = stats.DronesDestroyed,
					["absorbed"] = stats.DronesAbsorbed,
				},
				["shotsBlocked"] = stats.ShotsBlocked,
				["killsByEnemy"] = stats.KillsByEnemy(),
				["kills"] = stats.Kills.Select(k => new object[] { Math.Round(k.Time, 1), k.Enemy }).ToList(),
				["levelUps"] = stats.LevelUps.Select(l => new Dictionary<string, object>
				{
					["time"] = Math.Round(l.Time, 1),
					["level"] = l.Level,
					["offers"] = l.Offers,
					["rerolls"] = l.Rerolls,
					["freeRerolls"] = l.FreeRerolls,
					["taken"] = l.Taken,
				}).ToList(),
				["damageTaken"] = stats.DamageTaken.ToDictionary(e => e.Key, e => (object)Math.Round(e.Value, 1)),
				["experience"] = new Dictionary<string, object>
				{
					["asteroids"] = Math.Round(stats.ExperienceFromAsteroids, 1),
					["debris"] = Math.Round(stats.ExperienceFromDebris, 1),
				},
			},
			["timeline"] = _timeline,
			["input"] = _recordInput
				? new Dictionary<string, object> { ["samplesPerSecond"] = Settings.InputSamplesPerSecond, ["samples"] = _input }
				: null,
			["frames"] = new Dictionary<string, object>
			{
				["averageMs"] = sorted.Count == 0 ? 0 : Math.Round(sorted.Average(), 2),
				["p95Ms"] = Math.Round(Percentile(0.95), 2),
				["worstMs"] = sorted.Count == 0 ? 0 : Math.Round(sorted[^1], 2),
				["mostEnemies"] = _mostEnemies,
				["mostDrones"] = _mostDrones,
			},
		};
	}

	/// <summary>A line on the result screen saying where the log went.</summary>
	private void ShowNote()
	{
		if (_path == null || _fight.GetNodeOrNull<Control>("Hud/ResultScreen") is not Control screen)
			return;
		if (_note == null)
		{
			_note = Ui.Label("", 18, new Color(1f, 1f, 1f, 0.45f));
			_note.HorizontalAlignment = HorizontalAlignment.Left;
			_note.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
			_note.OffsetLeft = 24f;
			_note.OffsetTop = -40f;
			_note.GrowVertical = Control.GrowDirection.Begin;
			screen.AddChild(_note);
		}
		string relative = Path.GetRelativePath(ProjectSettings.GlobalizePath("res://"), _path).Replace('\\', '/');
		_note.Text = $"Fight log saved: {relative}   (editor only)";
	}

	/// <summary>A short hash of every tuning and content file, so fights can be checked to share their tuning.</summary>
	private static string TuningFingerprint()
	{
		if (_tuningFingerprint != null)
			return _tuningFingerprint;
		string root = ProjectSettings.GlobalizePath("res://");
		string[] skip = { "placeholder/", "scripts/cheats/", "playtests/", ".godot/", ".claude/", "addons/" };
		var files = Directory.EnumerateFiles(root, "*.tres", SearchOption.AllDirectories)
			.Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
			.Where(f => !skip.Any(f.StartsWith))
			.OrderBy(f => f, StringComparer.Ordinal);
		using var hash = SHA256.Create();
		foreach (string file in files)
		{
			byte[] name = Encoding.UTF8.GetBytes(file + "\n");
			hash.TransformBlock(name, 0, name.Length, null, 0);
			byte[] content = File.ReadAllBytes(Path.Combine(root, file)).Where(b => b != (byte)'\r').ToArray();
			hash.TransformBlock(content, 0, content.Length, null, 0);
		}
		hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
		_tuningFingerprint = Convert.ToHexString(hash.Hash)[..12].ToLowerInvariant();
		return _tuningFingerprint;
	}

	/// <summary>The git commit the game was built from, marked if there were uncommitted changes.</summary>
	private static string GitVersion()
	{
		if (_gitVersion != null)
			return _gitVersion;
		string root = ProjectSettings.GlobalizePath("res://");
		using var output = new Godot.Collections.Array();
		_gitVersion = "unknown";
		if (OS.Execute("git", new[] { "-C", root, "rev-parse", "--short", "HEAD" }, output) == 0 && output.Count > 0)
		{
			_gitVersion = output[0].AsString().Trim();
			using var status = new Godot.Collections.Array();
			if (OS.Execute("git", new[] { "-C", root, "status", "--porcelain" }, status) == 0
				&& status.Count > 0 && status[0].AsString().Trim().Length > 0)
				_gitVersion += "+changes";
		}
		return _gitVersion;
	}

	private static FightLogSettings LoadSettings()
	{
		const string path = "res://scripts/cheats/playtesting/fight_log.tres";
		var settings = GD.Load<FightLogSettings>(path);
		if (settings == null)
		{
			GD.PushError($"Could not load the fight log settings {path}.");
			settings = new FightLogSettings();
		}
		return settings;
	}
}
#endif
