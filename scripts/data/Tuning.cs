using Godot;

/// <summary>
/// Every number that affects how the fight feels. The values are not in code: they are in the .tres
/// files in res://tuning/, one per system, which can be edited in Godot's inspector. Weapons, Drone
/// Weapons, enemy types and upgrades are definitions in the folders beside them.
///
/// Each file is read the first time the fight needs it, so changes take effect from the next run.
///
/// Distances are in world units on the gameplay plane. For scale, the VNP is about 3 units long.
/// Times are in seconds, angles in degrees.
/// </summary>
public static class Tuning
{
	public static VnpTuning Vnp => _vnp ??= Load<VnpTuning>("vnp");
	public static DronesTuning Drones => _drones ??= Load<DronesTuning>("drones");
	public static HarvestingTuning Harvesting => _harvesting ??= Load<HarvestingTuning>("harvesting");
	public static AsteroidsTuning Asteroids => _asteroids ??= Load<AsteroidsTuning>("asteroids");
	public static EnemySpawningTuning EnemySpawning => _enemySpawning ??= Load<EnemySpawningTuning>("enemy_spawning");
	public static DifficultyTuning Difficulty => _difficulty ??= Load<DifficultyTuning>("difficulty");
	public static FightTuning Fight => _fight ??= Load<FightTuning>("fight");
	public static LevelsTuning Levels => _levels ??= Load<LevelsTuning>("levels");
	public static CameraTuning Camera => _camera ??= Load<CameraTuning>("camera");
	public static LookTuning Look => _look ??= Load<LookTuning>("look");
	public static FeedbackTuning Feedback => _feedback ??= Load<FeedbackTuning>("feedback");

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

	private static T Load<T>(string file) where T : Resource
	{
		string path = $"res://tuning/{file}.tres";
		T tuning = GD.Load<T>(path);
		if (tuning == null)
			GD.PushError($"Could not load the tuning file {path}.");
		return tuning;
	}
}
