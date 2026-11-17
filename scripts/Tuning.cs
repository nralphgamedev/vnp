using Godot;

/// <summary>
/// Every number that affects how the fight feels lives in this file and nowhere else.
/// Change a value, press Play, and Godot rebuilds before the fight starts.
///
/// Distances are in world units on the gameplay plane. For scale, the VNP is about
/// 3 units long. Times are in seconds, angles in degrees.
/// </summary>
public static class Tuning
{
	// ───────────── VNP movement ─────────────

	/// <summary>Top speed, in units per second, with the stick or keys fully held.</summary>
	public static float VnpMoveSpeed = 20f;

	/// <summary>How quickly the VNP gains speed or changes direction, in units per second squared.
	/// Higher is snappier. At 80 it reaches top speed in a quarter of a second.</summary>
	public static float VnpAcceleration = 80f;

	/// <summary>How quickly the VNP slows once input is released, in units per second squared.
	/// Higher stops sooner. Lower makes it drift.</summary>
	public static float VnpDeceleration = 60f;

	/// <summary>How fast the ship model turns to face its direction of travel, in degrees per second.
	/// Cosmetic only, because weapons aim on their own.</summary>
	public static float VnpTurnSpeed = 540f;

	/// <summary>Joystick movement below this fraction of full tilt is ignored.</summary>
	public static float StickDeadzone = 0.2f;

	// ───────────── VNP health ─────────────

	/// <summary>Health the VNP starts the fight with.</summary>
	public static float VnpMaxHealth = 100f;

	/// <summary>How close an enemy projectile must pass to the VNP's center to hit it.</summary>
	public static float VnpHitRadius = 1.4f;

	// ───────────── Pulse Laser ─────────────
	// The laser copies these when the fight starts. After that the numbers belong to the weapon.

	/// <summary>Damage of one shot.</summary>
	public static float PulseLaserDamage = 10f;

	/// <summary>Seconds between shots.</summary>
	public static float PulseLaserCooldown = 0.6f;

	/// <summary>How far away an enemy can be and still be shot.</summary>
	public static float PulseLaserRange = 22f;

	/// <summary>Seconds the beam stays visible after a shot. Cosmetic.</summary>
	public static float PulseLaserBeamTime = 0.12f;

	/// <summary>Thickness of the drawn beam. Cosmetic, because the beam only hits the enemy it aimed at.</summary>
	public static float PulseLaserBeamWidth = 0.45f;

	// ───────────── Basic probes ─────────────

	/// <summary>Probes the VNP starts the fight with.</summary>
	public static int StartingProbes = 10;

	/// <summary>Health of one probe.</summary>
	public static float ProbeHealth = 16f;

	/// <summary>Top speed of a probe. Above the VNP's speed, so probes can catch up.
	/// The gap between the two is how fast a probe left behind closes the distance.</summary>
	public static float ProbeSpeed = 32f;

	/// <summary>How quickly a probe changes speed or direction, in units per second squared.
	/// Higher makes the flock tighter and twitchier. Lower makes it looser and floatier.</summary>
	public static float ProbeAcceleration = 90f;

	/// <summary>How fast a probe turns to face where it is going or shooting, in degrees per second.</summary>
	public static float ProbeTurnSpeed = 720f;

	/// <summary>How far from the VNP the flock hovers when it has nothing to attack.</summary>
	public static float ProbeFlockRadius = 5f;

	/// <summary>How fast probes mill about within the flock, on top of keeping pace with the VNP.
	/// Also how quickly a probe that has dropped a little behind closes the gap.</summary>
	public static float ProbeFlockSpeed = 6f;

	/// <summary>A probe farther than this from the VNP drops everything and sprints back at top speed.</summary>
	public static float ProbeSprintDistance = 15f;

	/// <summary>Probes closer than this to each other push apart.</summary>
	public static float ProbeSeparationRadius = 1.8f;

	/// <summary>Strength of the push apart.</summary>
	public static float ProbeSeparation = 1.5f;

	/// <summary>Probes attack enemies that come this close to the VNP. An enemy that ends up farther
	/// away than this, because it fled or the VNP moved off, is dropped and the probes return.</summary>
	public static float ProbeEngageRange = 20f;

	/// <summary>How far from its target a probe sits while surrounding it.</summary>
	public static float ProbeAttackDistance = 5f;

	/// <summary>How close an enemy projectile must pass to a probe's center to hit it.</summary>
	public static float ProbeHitRadius = 0.7f;

	// ───────────── Basic Probe Weapon: Projectile ─────────────
	// One weapon shared by every probe. It copies these when the fight starts.

	/// <summary>Damage of one probe projectile.</summary>
	public static float ProbeProjectileDamage = 2f;

	/// <summary>Seconds between shots from one probe.</summary>
	public static float ProbeProjectileCooldown = 0.8f;

	/// <summary>A probe fires once its target is this close. Keep it above the attack distance.</summary>
	public static float ProbeProjectileRange = 8f;

	/// <summary>Speed of a probe projectile.</summary>
	public static float ProbeProjectileSpeed = 30f;

	// ───────────── Enemy ─────────────

	/// <summary>How close a probe projectile must pass to an enemy's center to hit it.</summary>
	public static float EnemyHitRadius = 1.1f;

	/// <summary>Health of an enemy spawned at the start of the fight.</summary>
	public static float EnemyHealth = 30f;

	/// <summary>Speed while chasing. Below the VNP's speed, so the player can get away.</summary>
	public static float EnemySpeed = 13f;

	/// <summary>Speed while roaming with nothing to chase.</summary>
	public static float EnemyWanderSpeed = 4f;

	/// <summary>How quickly an enemy changes speed or direction, in units per second squared.</summary>
	public static float EnemyAcceleration = 30f;

	/// <summary>How fast an enemy turns to face where it is going or shooting, in degrees per second.</summary>
	public static float EnemyTurnSpeed = 360f;

	// An enemy goes after whichever is nearest: the VNP or one of its probes.

	/// <summary>An idle enemy notices the VNP or a probe that comes this close, then chases and attacks.</summary>
	public static float EnemyAggroRange = 28f;

	/// <summary>A chasing enemy gives up once nothing of the player's is within this distance.
	/// Keep it above the aggro range.</summary>
	public static float EnemyLoseAggroRange = 45f;

	/// <summary>An enemy fires once its target is this close.</summary>
	public static float EnemyAttackRange = 16f;

	/// <summary>A chasing enemy stops closing in at this distance and slowly circles its target instead.
	/// 0 makes it keep charging.</summary>
	public static float EnemyHoldDistance = 11f;

	/// <summary>How far an idle enemy strays from its home point before turning back.</summary>
	public static float EnemyWanderRadius = 12f;

	/// <summary>Seconds between shots from one enemy.</summary>
	public static float EnemyFireCooldown = 1.5f;

	/// <summary>Damage of one enemy projectile at the start of the fight.</summary>
	public static float EnemyProjectileDamage = 4f;

	/// <summary>Speed of an enemy projectile. The further this is above the VNP's speed,
	/// the harder shots are to outrun.</summary>
	public static float EnemyProjectileSpeed = 32f;

	/// <summary>Seconds an enemy projectile flies before it fizzles out. Speed times this is its reach.</summary>
	public static float EnemyProjectileLifetime = 1.0f;

	/// <summary>How well enemies aim at a moving target. 1 aims exactly where the target will be
	/// when the shot arrives, so only a change of direction dodges it. 0 aims at where the target
	/// is now, so anything moving is missed. The effect is steep: against a VNP circling at full
	/// speed, roughly 70% of shots hit at 1, 50% at 0.75, and under 20% at 0.5.</summary>
	public static float EnemyAimLead = 0.75f;

	// ───────────── Enemy flocking ─────────────

	/// <summary>Enemies this close to each other count as flockmates.</summary>
	public static float EnemyNeighborRadius = 8f;

	/// <summary>Enemies closer than this push apart.</summary>
	public static float EnemySeparationRadius = 3.2f;

	/// <summary>Strength of the push apart. Higher gives looser groups.</summary>
	public static float EnemySeparation = 1.6f;

	/// <summary>How strongly an enemy matches its flockmates' heading.</summary>
	public static float EnemyAlignment = 1.0f;

	/// <summary>How strongly an enemy is pulled toward the middle of its flockmates.</summary>
	public static float EnemyCohesion = 0.6f;

	/// <summary>How strongly an idle enemy follows its own random heading instead of its flock.
	/// Lower gives tighter groups that move as one. Higher scatters them.</summary>
	public static float EnemyWanderWeight = 0.5f;

	// ───────────── Enemy spawning ─────────────

	/// <summary>Groups already roaming, off screen, when the fight starts.</summary>
	public static int EnemyStartingGroups = 5;

	/// <summary>Enemies in each spawned group.</summary>
	public static int EnemyGroupSize = 4;

	/// <summary>Seconds between spawned groups at the start of the fight.</summary>
	public static float EnemySpawnInterval = 4f;

	/// <summary>A group spawns at least this far from the VNP. 62 is just off screen
	/// at the current camera height.</summary>
	public static float EnemySpawnDistanceMin = 62f;

	/// <summary>A group spawns at most this far from the VNP.</summary>
	public static float EnemySpawnDistanceMax = 90f;

	/// <summary>No group spawns while this many enemies are alive. Raise it to test performance.</summary>
	public static int EnemyMaxAlive = 200;

	/// <summary>An enemy this far from the VNP is removed without counting as a kill,
	/// so enemies left behind do not use up the cap.</summary>
	public static float EnemyDespawnDistance = 180f;

	// ───────────── Difficulty ramp during the fight ─────────────
	// Each value is the extra fraction added per minute of fight time. 0.2 means +20% a minute.
	// An enemy keeps the strength it had when it spawned.

	/// <summary>Extra enemy health per minute.</summary>
	public static float RampEnemyHealthPerMinute = 0.2f;

	/// <summary>Extra enemy projectile damage per minute.</summary>
	public static float RampEnemyDamagePerMinute = 0.1f;

	/// <summary>Extra spawn rate per minute.</summary>
	public static float RampSpawnRatePerMinute = 0.25f;

	// ───────────── Debris ─────────────

	/// <summary>Smallest mass of the debris an enemy leaves.</summary>
	public static float DebrisMassMin = 1f;

	/// <summary>Largest mass of the debris an enemy leaves.</summary>
	public static float DebrisMassMax = 2f;

	// ───────────── Camera ─────────────

	/// <summary>False is perspective, true is orthographic. F1 switches while playing.</summary>
	public static bool CameraOrthographic = false;

	/// <summary>Perspective field of view, top to bottom. Narrow values flatten the picture.</summary>
	public static float CameraFov = 20f;

	/// <summary>How far the camera sits above the plane. Raising it shows more of the arena.
	/// At 150 the screen shows about 97 by 55 units. If you change this, also change the two
	/// EnemySpawnDistance values so groups still spawn off screen.</summary>
	public static float CameraHeight = 150f;

	/// <summary>How far the camera leans away from looking straight down. 0 is a pure top-down view.</summary>
	public static float CameraTilt = 15f;

	/// <summary>Seconds the camera takes to catch up with the VNP. 0 locks it to the VNP.</summary>
	public static float CameraFollowLag = 0f;

	// ───────────── Gateway ─────────────

	/// <summary>Where the gateway sits on the plane. The fight starts here.</summary>
	public static Vector2 GatewayPosition = Vector2.Zero;

	/// <summary>Size of the gateway ring. Later this is also the area that counts as "on the gateway".</summary>
	public static float GatewayRadius = 6f;

	// ───────────── Gateway indicator ─────────────

	/// <summary>Gap in pixels between the edge of the screen and the tip of the arrow.</summary>
	public static float IndicatorEdgeMargin = 28f;

	/// <summary>Length of the arrow in pixels.</summary>
	public static float IndicatorSize = 22f;

	/// <summary>False hides the arrow while the gateway itself is on screen.
	/// True keeps it at the screen edge at all times.</summary>
	public static bool IndicatorAlwaysVisible = false;

	// ───────────── Placeholder look ─────────────

	/// <summary>Scale of the VNP model. 1 is about 3 units long.</summary>
	public static float VnpSize = 1f;

	/// <summary>Scale of the probe model. 1 is about 1.3 units long.</summary>
	public static float ProbeSize = 1f;

	/// <summary>Seconds a destroyed probe's wreckage takes to disintegrate.</summary>
	public static float ProbeWreckTime = 0.4f;

	/// <summary>Scale of the enemy model. 1 is about 2.5 units across.</summary>
	public static float EnemySize = 1f;

	/// <summary>Scale of a piece of debris with mass 1. Heavier pieces are drawn larger.</summary>
	public static float DebrisSize = 0.8f;

	/// <summary>Seconds an enemy or the VNP flashes after being hit.</summary>
	public static float HitFlashTime = 0.12f;

	/// <summary>Spacing of the background grid lines. Every fifth line is brighter.</summary>
	public static float GridCellSize = 5f;

	public static Color ProbeColor = new(0.45f, 0.8f, 1.0f);
	public static Color ProbeProjectileColor = new(0.7f, 1.0f, 0.45f);
	public static Color EnemyColor = new(0.95f, 0.22f, 0.25f);
	public static Color EnemyProjectileColor = new(1.0f, 0.55f, 0.15f);
	public static Color PulseLaserColor = new(0.55f, 0.95f, 1.0f);
	public static Color DebrisColor = new(0.5f, 0.42f, 0.36f);

	public static Color VnpHullColor = new(0.62f, 0.7f, 0.9f);
	public static Color VnpCoreColor = new(1.0f, 0.72f, 0.2f);
	public static Color GatewayColor = new(0.2f, 0.95f, 0.85f);
	public static Color GridBaseColor = new(0.015f, 0.02f, 0.045f);
	public static Color GridLineColor = new(0.07f, 0.11f, 0.2f);
	public static Color GridMajorLineColor = new(0.13f, 0.22f, 0.4f);
}
