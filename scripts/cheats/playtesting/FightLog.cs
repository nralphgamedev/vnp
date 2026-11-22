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

	/// <summary>Give the next fight a folder of its own, inside its log folder, holding its log and its maps. The
	/// designer's fights always get one, at the designer's request; the scenario harness asks for one for pilot
	/// fights logged to playtests/pilot. Batches don't: they keep every log in one folder and maps in another.</summary>
	public static bool NextOwnFolder;
	private bool _ownFolder;

	/// <summary>For the next fight only: who is playing, written in the log. The designer, unless set.</summary>
	public static string NextPlayer;

	/// <summary>For the next fight only: the log's file name, without ".json", in place of its start time and
	/// seed. Batches name each fight, so fights started in the same second with the same seed don't overwrite
	/// each other.</summary>
	public static string NextFileName;

	/// <summary>For the next fight only: the tuning variant applied to it, and the variant's text, which goes into
	/// the tuning fingerprint so a variant's fights are never mistaken for the base tuning's.</summary>
	public static string NextVariant;
	public static string NextVariantText;

	public static FightLogSettings Settings => _settings ??= LoadSettings();

	private static FightLogSettings _settings;
	private static string _gitVersion;
	private static string _tuningFingerprint;

	private Fight _fight;
	private string _folder;
	private string _player;
	private string _path;
	private string _fileName;
	private string _variant;
	private string _variantText;
	private readonly DateTime _startedAt = DateTime.Now;
	private double _nextSample;
	private double _nextInput;
	private bool _recordInput;
	private RunReview _review;
	private ReviewRecorder _recorder;

	/// <summary>Fight times at which the next fight saves strips for strip maps, as well as when detectors fire.
	/// Set by the scenario harness's strip= option.</summary>
	public static List<double> NextStripTimes;

	/// <summary>Draw the next fight's run review maps when its log is written, into a maps folder beside it.
	/// The designer's fights always are. Set by the scenario harness's maps=yes option.</summary>
	public static bool NextDrawMaps;
	private bool _drawMaps;
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
		string fileName = NextFileName;
		NextFileName = null;
		string variant = NextVariant, variantText = NextVariantText;
		NextVariant = NextVariantText = null;
		bool ownFolder = NextOwnFolder || NextFolder == null;
		NextOwnFolder = false;
		NextFolder = null;
		NextPlayer = null;
		if (!Settings.WriteLogs || string.IsNullOrEmpty(folder))
			return null;

		var log = new FightLog { Name = "FightLog", _fight = fight, _folder = folder, _player = player, _fileName = fileName, _variant = variant, _variantText = variantText, _ownFolder = ownFolder };
		// Before everything else in the fight, and the log after everything, to time the fight's update.
		fight.AddChild(new FrameStart { Name = "FightLogFrameStart", Log = log, ProcessPriority = int.MinValue });
		log.ProcessPriority = int.MaxValue;
		// Movement is recorded for everyone: a person's is the data for making pilots play more like one, and a
		// pilot's is compared with it.
		log._recordInput = true;
		// The run review's detectors watch every fight, the designer's and pilots' alike.
		ReviewSettings reviewSettings = LoadReviewSettings();
		log._recorder = new ReviewRecorder(fight, reviewSettings);
		if (NextStripTimes != null)
			log._recorder.ExtraStripTimes.AddRange(NextStripTimes);
		NextStripTimes = null;
		log._drawMaps = NextDrawMaps || player == "designer";
		NextDrawMaps = false;
		log._review = new RunReview(fight, reviewSettings) { Recorder = log._recorder };
		fight.AddChild(log);
		return log;
	}

	public override void _Ready()
	{
		// Build the log once and throw it away, as the fight is set up, so .NET compiles that code now: the first
		// real write otherwise spent about 70 ms compiling it on the main thread, a hitch at the win. Deferred, so
		// the fight is ready; it runs even while the starting weapon menu has the game paused.
		Callable.From(() =>
		{
			try
			{
				Build("warm-up");
			}
			catch (Exception)
			{
				// Only compiling matters; a log of a fight not yet started may not add up.
			}
		}).CallDeferred();
	}

	public override void _Process(double delta)
	{
		// The fight clock only runs while the fight does, so this skips the paused starting weapon menu.
		double now = _fight.Elapsed;
		_review?.Update(now);
		_recorder?.Update(now);
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
			Vector2 move = _fight.Vnp.Controller != null
				? _fight.Vnp.Controller.Move.LimitLength(1f)
				: Input.GetVector("move_left", "move_right", "move_up", "move_down", Tuning.Vnp.StickDeadzone);
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
	/// <summary>
	/// Writes the log for a fight stopped at a set fight time, before it ended. Batches stop endless runs this
	/// way, so one strong build can't hold up a batch; the log calls it "capped", meaning it survived to the cap.
	/// </summary>
	public void FightCapped()
	{
		Sample(_fight.Elapsed);
		Write("capped");
		_capped = true;
	}

	private bool _capped;

	public void FightEnded(Fight.Outcome outcome)
	{
		Sample(_fight.Elapsed);
		Write(outcome.ToString().ToLowerInvariant());
		ShowNote();
	}

	public override void _ExitTree()
	{
		// Left without ending, by starting a new fight or closing the game, it is still worth keeping.
		if (!_fight.IsOver && !_capped && _fight.Elapsed > 1.0)
		{
			// A last sample, so the timeline ends where the fight did, as it does for a fight that ended.
			Sample(_fight.Elapsed);
			Write("unfinished");
		}
		// The fight is going away: let any write still running finish, so the file and maps aren't cut short.
		WaitForWrites();
	}

	// Writing the file and drawing the maps happen on a background thread, one write after another, so the
	// end of a fight doesn't hitch: drawing every map of a long fight took over a second on the main thread.
	private static System.Threading.Tasks.Task _writing = System.Threading.Tasks.Task.CompletedTask;
	private static readonly object _writingLock = new();

	/// <summary>Waits for every fight log write still running, such as before quitting.</summary>
	public static void WaitForWrites()
	{
		System.Threading.Tasks.Task writing;
		lock (_writingLock)
			writing = _writing;
		if (!writing.Wait(TimeSpan.FromSeconds(30)))
			GD.PushError("A fight log was still being written after 30 seconds.");
	}

	private void Write(string result)
	{
		try
		{
			string folder = ProjectSettings.GlobalizePath(_folder);
			Directory.CreateDirectory(folder);
			KeepOutOfImport(folder);
			string name = _fileName ?? $"{_startedAt:yyyy-MM-dd_HH-mm-ss}_seed{_fight.Seed}";
			// In a folder of its own, the log and its maps sit together, and the maps need no name of their own.
			string fightFolder = _ownFolder ? Path.Combine(folder, name) : folder;
			Directory.CreateDirectory(fightFolder);
			_path ??= Path.Combine(fightFolder, name + ".json");
			// Gathering the log happens here, on the main thread: Build copies every list that keeps growing, so
			// it is a fixed picture of the fight as it is now, which an endless run carrying on can't change. The
			// rest, turning it into text, writing it and drawing its maps, happens in the background. The
			// designer's logs are indented, to be read by a person; others are compact, since a batch writes
			// hundreds and scripts read them.
			var watch = System.Diagnostics.Stopwatch.StartNew();
			Dictionary<string, object> data = Build(result);
			bool indented = _player == "designer";
			long gathered = watch.ElapsedMilliseconds;
			string path = _path, maps = _ownFolder ? Path.GetDirectoryName(_path) : Path.Combine(folder, "maps");
			string prefix = _ownFolder ? "" : null;
			bool draw = _drawMaps;
			// Writing the file and drawing the maps (an overview and strips of flagged moments) work only from that
			// copy, so they can run in the background without touching the fight.
			lock (_writingLock)
			{
				_writing = _writing.ContinueWith(_ =>
				{
					try
					{
						var background = System.Diagnostics.Stopwatch.StartNew();
						File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = indented }));
						if (draw)
							MapDrawer.DrawLog(path, maps, true, null, prefix);
						GD.Print($"FIGHT LOG: {Path.GetFileName(path)}: {gathered} ms on the main thread, " +
							$"{background.ElapsedMilliseconds} ms writing{(draw ? " and drawing maps" : "")} in the background");
					}
					catch (Exception error)
					{
						GD.PushError($"Could not write the fight log or its maps: {error.Message}");
					}
				}, System.Threading.Tasks.TaskScheduler.Default);
			}
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
				["tuningVariant"] = _variant,
				["tuningFingerprint"] = _variantText == null ? TuningFingerprint() : WithVariant(TuningFingerprint(), _variantText),
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
					["offers"] = l.Offers.Select(o => o.ToList()).ToList(),
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
			["timeline"] = new List<Dictionary<string, object>>(_timeline),
			["review"] = _review?.ToLog(_fight.Elapsed),
			["maps"] = _recorder?.ToLog(),
			["input"] = _recordInput
				? new Dictionary<string, object> { ["samplesPerSecond"] = Settings.InputSamplesPerSecond, ["samples"] = new List<float[]>(_input) }
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
		// The pilots' own files (pilots/ and each weapon's pilot_profile.tres) are left out: they describe the
		// pilots, not the game being balanced, so calibrating pilots mustn't make the designer's fights look as if
		// they were played on other tuning.
		string[] skip = { "placeholder/", "scripts/cheats/", "playtests/", ".godot/", ".claude/", "addons/", "pilots/" };
		var files = Directory.EnumerateFiles(root, "*.tres", SearchOption.AllDirectories)
			.Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
			.Where(f => !skip.Any(f.StartsWith) && !f.EndsWith("/pilot_profile.tres"))
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

	/// <summary>The tuning fingerprint with a variant's changes folded in.</summary>
	private static string WithVariant(string fingerprint, string variantText)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(fingerprint + "\n" + variantText.Replace("\r", ""));
		return Convert.ToHexString(SHA256.HashData(bytes))[..12].ToLowerInvariant();
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

	private static ReviewSettings LoadReviewSettings()
	{
		const string path = "res://scripts/cheats/playtesting/review_settings.tres";
		var settings = GD.Load<ReviewSettings>(path);
		if (settings == null)
		{
			GD.PushError($"Could not load the run review settings {path}.");
			settings = new ReviewSettings();
		}
		return settings;
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
