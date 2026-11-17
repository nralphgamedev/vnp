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

	// ───────────── VNP weapons ─────────────

	/// <summary>How many VNP Weapons the VNP can carry. The Pulse Laser takes the first slot.</summary>
	public static int WeaponSlots = 2;

	// ───────────── Orbital Shards ─────────────
	// A VNP Weapon offered as a fight upgrade. Copied when equipped; after that the numbers belong to the weapon.

	/// <summary>How many shards circle the VNP.</summary>
	public static int ShardCount = 3;

	/// <summary>Damage a shard deals when it touches an enemy.</summary>
	public static float ShardDamage = 8f;

	/// <summary>Seconds before a shard can hit the same enemy again. It only has to stop one pass
	/// counting twice, so keep it below the time a shard takes to go round once (360 / orbit speed,
	/// 1.8 seconds at 200). Above that, speed upgrades stop adding damage.</summary>
	public static float ShardCooldown = 0.5f;

	/// <summary>How far from the VNP the shards circle. Enemies hold about 11 units from their target,
	/// so shards mostly hit when the VNP closes in on enemies.</summary>
	public static float ShardOrbitRadius = 7f;

	/// <summary>How fast the shards circle, in degrees per second. Faster shards pass each enemy more often,
	/// so they deal more damage.</summary>
	public static float ShardOrbitSpeed = 200f;

	/// <summary>How close to a shard an enemy's edge must come to be hit.</summary>
	public static float ShardHitRadius = 0.8f;

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
	/// away than this, because it fled or the VNP moved off, is dropped and the probes return.
	/// While any enemy is in this range, attacking comes before harvesting.</summary>
	public static float ProbeEngageRange = 20f;

	/// <summary>How far from its target a probe sits while surrounding it.</summary>
	public static float ProbeAttackDistance = 5f;

	/// <summary>How fast probes circle whatever they surround, in degrees per second.</summary>
	public static float ProbeCircleSpeed = 46f;

	/// <summary>How hard probes brake as they reach their spot around a target, as a fraction of
	/// their acceleration. Lower starts braking earlier and arrives more gently; higher arrives
	/// faster but closer to overshooting. Keep it at or below 0.5 to avoid bouncing.</summary>
	public static float ProbeArriveBraking = 0.5f;

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

	// ───────────── Basic Probe Weapon: Beam ─────────────
	// What every probe switches to when the Beam upgrade is taken.

	/// <summary>Damage of one beam tick.</summary>
	public static float ProbeBeamDamage = 1f;

	/// <summary>Seconds between beam ticks.</summary>
	public static float ProbeBeamCooldown = 0.25f;

	/// <summary>How far the beam reaches.</summary>
	public static float ProbeBeamRange = 5f;

	/// <summary>How far from its target a probe sits while surrounding it with the beam.
	/// Keep it below the beam's range.</summary>
	public static float ProbeBeamAttackDistance = 3.5f;

	// ───────────── Enemy ─────────────

	/// <summary>How close a probe projectile must pass to an enemy's center to hit it.
	/// Also how big an enemy is for Orbital Shards.</summary>
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

	// ───────────── Harvesting ─────────────
	// Harvest beams draw mass out of asteroids and debris gradually, and experience and
	// probe-building progress arrive as it comes. Harvest time is the mass divided by the total
	// rate of the beams on it: the VNP alone takes 1 second per unit of mass.

	/// <summary>Mass the VNP's harvest beam draws out per second.</summary>
	public static float VnpHarvestRate = 1f;

	/// <summary>Mass one probe's harvest beam draws out per second.
	/// More probes on the same asteroid harvest it faster.</summary>
	public static float ProbeHarvestRate = 0.4f;

	/// <summary>How far a harvest beam reaches past the edge of an asteroid or piece of debris.
	/// Used by the VNP and the probes.</summary>
	public static float HarvestBeamRange = 8f;

	/// <summary>Fight experience gained per unit of mass harvested.</summary>
	public static float ExperiencePerMass = 10f;

	/// <summary>Mass that must be harvested to build one temporary probe.</summary>
	public static float HarvestPerProbe = 8f;

	/// <summary>Most probes the VNP can have. Harvesting builds probes up to this,
	/// and gives only fight experience once it is reached.</summary>
	public static int ProbeCap = 20;

	/// <summary>Probes go to harvest asteroids and debris whose edge comes within this distance
	/// of the VNP, once there is no enemy to attack.</summary>
	public static float ProbeHarvestRange = 20f;

	/// <summary>How far beyond the edge of an asteroid a probe sits while harvesting it.</summary>
	public static float ProbeHarvestStandoff = 2f;

	// ───────────── Levels and fight upgrades ─────────────

	/// <summary>Fight experience needed to go from level 1 to level 2.</summary>
	public static float ExperienceForFirstLevel = 150f;

	/// <summary>How much more each level after that needs than the one before.
	/// At 150 and 75, level 5 takes 1050 in total and level 10 takes 4050.</summary>
	public static float ExperienceIncreasePerLevel = 75f;

	/// <summary>How many upgrades each level up offers.</summary>
	public static int UpgradeOptionsPerOffer = 3;

	/// <summary>Rerolls the player starts the fight with. They are not refreshed.</summary>
	public static int FreeRerolls = 1;

	// Upgrade amounts. Fractions are of the current value: 0.25 means +25% each time it is taken.

	/// <summary>Pulse Laser damage upgrade.</summary>
	public static float UpgradePulseLaserDamage = 0.25f;

	/// <summary>Pulse Laser cooldown upgrade: the cooldown shrinks by this fraction.</summary>
	public static float UpgradePulseLaserCooldown = 0.15f;

	/// <summary>Orbital Shards damage upgrade.</summary>
	public static float UpgradeShardDamage = 0.3f;

	/// <summary>Orbital Shards area of effect upgrade: orbit size and shard size both grow by this fraction.</summary>
	public static float UpgradeShardArea = 0.2f;

	/// <summary>Orbital Shards speed upgrade: the shards circle this much faster.</summary>
	public static float UpgradeShardSpeed = 0.25f;

	/// <summary>Health upgrade: added to the VNP's maximum health, and healed.</summary>
	public static float UpgradeHealth = 25f;

	/// <summary>Movement speed upgrade. Probes speed up by the same fraction, so they keep up.</summary>
	public static float UpgradeMoveSpeed = 0.1f;

	/// <summary>Harvest rate upgrade: every harvest beam, the VNP's and the probes', works this much faster.</summary>
	public static float UpgradeHarvestRate = 0.25f;

	/// <summary>Harvest range upgrade: every harvest beam reaches this much farther.</summary>
	public static float UpgradeHarvestRange = 0.25f;

	/// <summary>Basic probe cap upgrade: added to the cap.</summary>
	public static int UpgradeProbeCap = 5;

	/// <summary>New basic probes upgrade: probes built at once, up to the cap.</summary>
	public static int UpgradeNewProbes = 5;

	// ───────────── Asteroid fields ─────────────
	// These place the fields around the gateway at the start of the fight. Farther out, the arena is
	// filled as the VNP explores: see "Filling the arena" below.

	/// <summary>Fewest asteroid fields in a fight.</summary>
	public static int AsteroidFieldsMin = 5;

	/// <summary>Most asteroid fields in a fight.</summary>
	public static int AsteroidFieldsMax = 9;

	/// <summary>Closest a field's center can be to the gateway.</summary>
	public static float AsteroidFieldDistanceMin = 70f;

	/// <summary>Farthest a field's center can be from the gateway.</summary>
	public static float AsteroidFieldDistanceMax = 260f;

	/// <summary>Closest two fields' centers can be to each other.</summary>
	public static float AsteroidFieldSpacing = 70f;

	/// <summary>Chance that a field is a ring. The rest are clusters.</summary>
	public static float AsteroidRingChance = 0.4f;

	/// <summary>Chance that a field drifts in a straight line.</summary>
	public static float AsteroidFieldMovingChance = 0.3f;

	/// <summary>Chance that a field turns around its center. Fields that neither drift nor turn stay put.</summary>
	public static float AsteroidFieldTurningChance = 0.3f;

	/// <summary>Speed of a drifting field, in units per second.</summary>
	public static float AsteroidFieldDriftSpeed = 0.6f;

	/// <summary>Speed of a turning field, in degrees per second.</summary>
	public static float AsteroidFieldTurnSpeed = 5f;

	/// <summary>Fewest asteroids in a cluster.</summary>
	public static int AsteroidsPerClusterMin = 12;

	/// <summary>Most asteroids in a cluster.</summary>
	public static int AsteroidsPerClusterMax = 24;

	/// <summary>Smallest radius of a cluster.</summary>
	public static float AsteroidClusterRadiusMin = 10f;

	/// <summary>Largest radius of a cluster.</summary>
	public static float AsteroidClusterRadiusMax = 18f;

	/// <summary>Fewest asteroids in a ring.</summary>
	public static int AsteroidsPerRingMin = 22;

	/// <summary>Most asteroids in a ring.</summary>
	public static int AsteroidsPerRingMax = 36;

	/// <summary>Smallest radius of a ring.</summary>
	public static float AsteroidRingRadiusMin = 16f;

	/// <summary>Largest radius of a ring.</summary>
	public static float AsteroidRingRadiusMax = 26f;

	/// <summary>How wide the band of a ring is.</summary>
	public static float AsteroidRingWidth = 5f;

	/// <summary>Mass of the smallest asteroids. Harvest time scales with mass.</summary>
	public static float AsteroidMassMin = 1f;

	/// <summary>Mass of the largest asteroids.</summary>
	public static float AsteroidMassMax = 10f;

	/// <summary>How strongly asteroid masses lean toward small. 1 spreads them evenly;
	/// 2 makes small asteroids common and large ones rare; higher leans further.
	/// Applies to lone asteroids too.</summary>
	public static float AsteroidSmallBias = 2f;

	// ───────────── Lone asteroids ─────────────
	// Single asteroids scattered between the fields. Like the fields, they are placed at the start
	// of the fight and stay put.

	/// <summary>How many lone asteroids there are around the gateway. At 30 there is roughly one every couple of screens.</summary>
	public static int LoneAsteroidCount = 30;

	/// <summary>Closest a lone asteroid can be to the gateway.</summary>
	public static float LoneAsteroidDistanceMin = 20f;

	/// <summary>Farthest a lone asteroid can be from the gateway.</summary>
	public static float LoneAsteroidDistanceMax = 300f;

	/// <summary>Closest a lone asteroid can be to a field's center, so it does not look like part of the field.</summary>
	public static float LoneAsteroidFieldClearance = 45f;

	/// <summary>Closest two lone asteroids can be to each other.</summary>
	public static float LoneAsteroidSpacing = 25f;

	/// <summary>Mass of the smallest lone asteroids. Above the field minimum, so a lone asteroid is worth a detour.</summary>
	public static float LoneAsteroidMassMin = 3f;

	/// <summary>Mass of the largest lone asteroids.</summary>
	public static float LoneAsteroidMassMax = 10f;

	// ───────────── Filling the arena ─────────────
	// Beyond the area around the gateway (300 units with the values above), the arena is filled in
	// square cells as the VNP comes within reach of each one, long before it is on screen. Once
	// placed, asteroids stay where they are: nothing follows the VNP and nothing respawns.
	// Fields and lone asteroids keep the same spacing rules as around the gateway.

	/// <summary>Width of one cell.</summary>
	public static float AsteroidCellSize = 400f;

	/// <summary>Cells this close to the VNP are filled. Keep it well beyond half the screen width (about 50),
	/// so filling always happens off screen.</summary>
	public static float AsteroidFillDistance = 500f;

	/// <summary>Fewest fields in a cell. 4 to 7 matches the density around the gateway.</summary>
	public static int AsteroidFieldsPerCellMin = 4;

	/// <summary>Most fields in a cell.</summary>
	public static int AsteroidFieldsPerCellMax = 7;

	/// <summary>Lone asteroids in a cell. 17 matches the density around the gateway.</summary>
	public static int LoneAsteroidsPerCell = 17;

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

	/// <summary>Gap in pixels at 1920 by 1080 between the edge of the screen and the tip of the arrow.</summary>
	public static float IndicatorEdgeMargin = 46f;

	/// <summary>Length of the arrow in pixels at 1920 by 1080.</summary>
	public static float IndicatorSize = 36f;

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

	/// <summary>Scale of a piece of debris with mass 1. Heavier pieces are drawn larger,
	/// and every piece shrinks as it is harvested.</summary>
	public static float DebrisSize = 0.8f;

	/// <summary>Scale of an asteroid with mass 1, about 1.8 units across. Heavier asteroids are
	/// drawn larger, and every asteroid shrinks as it is harvested.</summary>
	public static float AsteroidSize = 1f;

	/// <summary>Seconds an enemy or the VNP flashes after being hit.</summary>
	public static float HitFlashTime = 0.12f;

	/// <summary>Spacing of the background grid lines. Every fifth line is brighter.</summary>
	public static float GridCellSize = 5f;

	public static Color ProbeColor = new(0.45f, 0.8f, 1.0f);
	public static Color ProbeProjectileColor = new(0.7f, 1.0f, 0.45f);
	public static Color ProbeBeamColor = new(0.8f, 0.97f, 1.0f);
	public static Color ShardColor = new(0.95f, 0.95f, 1.0f);

	/// <summary>Scale of an Orbital Shard. 1 is 2 units long.</summary>
	public static float ShardSize = 0.8f;
	public static Color EnemyColor = new(0.95f, 0.22f, 0.25f);
	public static Color EnemyProjectileColor = new(1.0f, 0.55f, 0.15f);
	public static Color PulseLaserColor = new(0.55f, 0.95f, 1.0f);
	public static Color DebrisColor = new(0.55f, 0.3f, 0.24f);
	public static Color AsteroidColor = new(0.46f, 0.47f, 0.52f);

	/// <summary>Colour of fight experience: harvest beams, and the tint on whatever they are harvesting.</summary>
	public static Color ExperienceColor = new(0.72f, 0.45f, 1.0f);

	public static Color VnpHullColor = new(0.62f, 0.7f, 0.9f);
	public static Color VnpCoreColor = new(1.0f, 0.72f, 0.2f);
	public static Color GatewayColor = new(0.2f, 0.95f, 0.85f);
	public static Color GridBaseColor = new(0.015f, 0.02f, 0.045f);
	public static Color GridLineColor = new(0.07f, 0.11f, 0.2f);
	public static Color GridMajorLineColor = new(0.13f, 0.22f, 0.4f);
}
