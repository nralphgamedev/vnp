using Godot;

/// <summary>
/// Every number that affects how the fight feels. The values are not in code: they are in .tres files,
/// one per system, which can be edited in Godot's inspector. Most are in res://tuning/; a system with its
/// own folder keeps its file there (audio in res://audio/). Weapons, Drone Weapons and enemy types are
/// definitions in their own folders (res://weapons/, res://drone_weapons/, res://enemies/), beside their
/// code; upgrades that belong to no feature are in res://tuning/upgrades/. The Fight Director's plans are in
/// res://director/.
///
/// Each file is read the first time the fight needs it, so changes take effect from the next run.
///
/// Distances are in world units on the gameplay plane. For scale, the VNP is about 3 units long.
/// Times are in seconds, angles in degrees.
/// </summary>
public static class Tuning
{
	public static VnpTuning Vnp => _vnp ??= Load<VnpTuning>("res://tuning/vnp.tres");
	public static DronesTuning Drones => _drones ??= Load<DronesTuning>("res://tuning/drones.tres");
	public static HarvestingTuning Harvesting => _harvesting ??= Load<HarvestingTuning>("res://tuning/harvesting.tres");
	public static AsteroidsTuning Asteroids => _asteroids ??= Load<AsteroidsTuning>("res://tuning/asteroids.tres");
	public static EnemySpawningTuning EnemySpawning => _enemySpawning ??= Load<EnemySpawningTuning>("res://tuning/enemy_spawning.tres");
	public static DifficultyTuning Difficulty => _difficulty ??= Load<DifficultyTuning>("res://tuning/difficulty.tres");
	public static FightTuning Fight => _fight ??= Load<FightTuning>("res://tuning/fight.tres");
	public static LevelsTuning Levels => _levels ??= Load<LevelsTuning>("res://tuning/levels.tres");
	public static CameraTuning Camera => _camera ??= Load<CameraTuning>("res://tuning/camera.tres");
	public static LookTuning Look => _look ??= Load<LookTuning>("res://tuning/look.tres");
	public static FeedbackTuning Feedback => _feedback ??= Load<FeedbackTuning>("res://tuning/feedback.tres");
	public static AudioTuning Audio => _audio ??= Load<AudioTuning>("res://audio/audio.tres");
	public static FightDirectorTuning FightDirector => _fightDirector ??= Load<FightDirectorTuning>("res://director/fight_director.tres");

	private static VnpTuning _vnp;
	private static DronesTuning _drones;
	private static HarvestingTuning _harvesting;
	private static AsteroidsTuning _asteroids;
	private static EnemySpawningTuning _enemySpawning;
	private static DifficultyTuning _difficulty;
	private static FightTuning _fight;
	private static LevelsTuning _levels;
	private static CameraTuning _camera;
	private static LookTuning _look;
	private static FeedbackTuning _feedback;
	private static AudioTuning _audio;
	private static FightDirectorTuning _fightDirector;

	/// <summary>Loads one tuning file by its full path, so a system can move to its own folder by changing
	/// its line above.</summary>
	private static T Load<T>(string path) where T : Resource
	{
		T tuning = GD.Load<T>(path);
		if (tuning == null)
			GD.PushError($"Could not load the tuning file {path}.");
		return tuning;
	}
}
