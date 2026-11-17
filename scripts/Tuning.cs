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
	public static float ShardDamage = 12f;

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

	// ───────────── Drones ─────────────

	/// <summary>Drones the VNP starts the fight with.</summary>
	public static int StartingDrones = 10;

	/// <summary>Health of one drone.</summary>
	public static float DroneHealth = 16f;

	/// <summary>Top speed of a drone. Above the VNP's speed, so drones can catch up.
	/// The gap between the two is how fast a drone left behind closes the distance.</summary>
	public static float DroneSpeed = 32f;

	/// <summary>How quickly a drone changes speed or direction, in units per second squared.
	/// Higher makes the flock tighter and twitchier. Lower makes it looser and floatier.</summary>
	public static float DroneAcceleration = 90f;

	/// <summary>How fast a drone turns to face where it is going or shooting, in degrees per second.</summary>
	public static float DroneTurnSpeed = 720f;

	/// <summary>How far from the VNP the flock hovers when it has nothing to attack.</summary>
	public static float DroneFlockRadius = 5f;

	/// <summary>How fast drones mill about within the flock, on top of keeping pace with the VNP.
	/// Also how quickly a drone that has dropped a little behind closes the gap.</summary>
	public static float DroneFlockSpeed = 6f;

	/// <summary>A drone farther than this from the VNP drops everything and sprints back at top speed.</summary>
	public static float DroneSprintDistance = 15f;

	/// <summary>Drones closer than this to each other push apart.</summary>
	public static float DroneSeparationRadius = 1.8f;

	/// <summary>Strength of the push apart.</summary>
	public static float DroneSeparation = 1.5f;

	/// <summary>Drones attack enemies that come this close to the VNP. An enemy that ends up farther
	/// away than this, because it fled or the VNP moved off, is dropped and the drones return.
	/// While any enemy is in this range, attacking comes before harvesting.</summary>
	public static float DroneEngageRange = 20f;

	/// <summary>How far from its target a drone sits while surrounding it.</summary>
	public static float DroneAttackDistance = 5f;

	/// <summary>How fast drones circle whatever they surround, in degrees per second.</summary>
	public static float DroneCircleSpeed = 46f;

	/// <summary>How hard drones brake as they reach their spot around a target, as a fraction of
	/// their acceleration. Lower starts braking earlier and arrives more gently; higher arrives
	/// faster but closer to overshooting. Keep it at or below 0.5 to avoid bouncing.</summary>
	public static float DroneArriveBraking = 0.5f;

	/// <summary>How close an enemy projectile must pass to a drone's center to hit it.</summary>
	public static float DroneHitRadius = 0.7f;

	// ───────────── Drone Weapon: Projectile ─────────────
	// One weapon shared by every drone. It copies these when the fight starts.

	/// <summary>Damage of one drone projectile.</summary>
	public static float DroneProjectileDamage = 2f;

	/// <summary>Seconds between shots from one drone.</summary>
	public static float DroneProjectileCooldown = 0.8f;

	/// <summary>A drone fires once its target is this close. Keep it above the attack distance.</summary>
	public static float DroneProjectileRange = 8f;

	/// <summary>Speed of a drone projectile.</summary>
	public static float DroneProjectileSpeed = 30f;

	// ───────────── Drone Weapon: Beam ─────────────
	// What every drone switches to when the Beam upgrade is taken.

	/// <summary>Damage of one beam tick.</summary>
	public static float DroneBeamDamage = 1f;

	/// <summary>Seconds between beam ticks.</summary>
	public static float DroneBeamCooldown = 0.25f;

	/// <summary>How far the beam reaches.</summary>
	public static float DroneBeamRange = 5f;

	/// <summary>How far from its target a drone sits while surrounding it with the beam.
	/// Keep it below the beam's range.</summary>
	public static float DroneBeamAttackDistance = 3.5f;

	// ───────────── Enemy ─────────────

	/// <summary>How close a drone projectile must pass to an enemy's center to hit it.
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

	/// <summary>How fast an idle enemy turns to face where it is going, in degrees per second.
	/// Lower looks calmer; higher follows every small change of course.</summary>
	public static float EnemyIdleTurnSpeed = 90f;

	/// <summary>An idle enemy ignores changes of course smaller than this, in degrees, so small nudges
	/// from flockmates do not show as twitching. 0 follows every change.</summary>
	public static float EnemyIdleTurnDeadband = 10f;

	// An enemy goes after whichever is nearest: the VNP or one of its drones.

	/// <summary>An idle enemy notices the VNP or a drone that comes this close, then chases and attacks.</summary>
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

	// ───────────── Enemy waves ─────────────
	// Every so often a wave arrives: several groups at once, spaced evenly on a ring just off screen
	// around the VNP, all hunting it from the start. Wave enemies chase from any distance and never
	// give up. They have the same strength as other enemies spawned at that moment, and ignore
	// EnemyMaxAlive.

	/// <summary>Fight time, in seconds, when the first wave arrives.</summary>
	public static float WaveFirstSeconds = 45f;

	/// <summary>Seconds between waves.</summary>
	public static float WaveIntervalSeconds = 45f;

	/// <summary>Groups in the first wave. Each group is EnemyGroupSize enemies.</summary>
	public static int WaveGroups = 5;

	/// <summary>Groups added to each wave after the first.</summary>
	public static int WaveGroupsAddedPerWave = 1;

	/// <summary>Seconds of "WAVE INCOMING" warning before a wave arrives.</summary>
	public static float WaveWarningSeconds = 3f;

	/// <summary>Seconds the "WAVE n" banner stays up after a wave arrives.</summary>
	public static float WaveBannerSeconds = 2f;

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
	// drone-building progress arrive as it comes. Harvest time is the mass divided by the total
	// rate of the beams on it: the VNP alone takes half a second per unit of mass.

	/// <summary>Mass the VNP's harvest beam draws out per second. Kept well above a drone's rate.</summary>
	public static float VnpHarvestRate = 2f;

	/// <summary>Mass one drone's harvest beam draws out per second.
	/// More drones on the same asteroid harvest it faster.</summary>
	public static float DroneHarvestRate = 0.4f;

	/// <summary>How far a harvest beam reaches past the edge of an asteroid or piece of debris.
	/// Used by the VNP and the drones.</summary>
	public static float HarvestBeamRange = 8f;

	/// <summary>Fight experience gained per unit of mass harvested.</summary>
	public static float ExperiencePerMass = 10f;

	/// <summary>Mass that must be harvested to build one temporary drone.</summary>
	public static float HarvestPerDrone = 8f;

	/// <summary>Most drones the VNP can have. Harvesting builds drones up to this,
	/// and gives only fight experience once it is reached.</summary>
	public static int DroneCap = 20;

	/// <summary>Drones go to harvest asteroids and debris whose edge comes within this distance
	/// of the VNP, once there is no enemy to attack.</summary>
	public static float DroneHarvestRange = 20f;

	/// <summary>How far beyond the edge of an asteroid a drone sits while harvesting it.</summary>
	public static float DroneHarvestStandoff = 2f;

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

	/// <summary>Movement speed upgrade. Drones speed up by the same fraction, so they keep up.</summary>
	public static float UpgradeMoveSpeed = 0.1f;

	/// <summary>Harvest rate upgrade: every harvest beam, the VNP's and the drones', works this much faster.</summary>
	public static float UpgradeHarvestRate = 0.25f;

	/// <summary>Drone cap upgrade: added to the cap.</summary>
	public static int UpgradeDroneCap = 5;

	/// <summary>New drones upgrade: drones built at once, up to the cap.</summary>
	public static int UpgradeNewDrones = 5;

	// Upgrade limits: the most times each upgrade can be taken in one fight. Once reached, it is no
	// longer offered. The card shows which one it is, such as 2/4. New weapons can be taken once.
	// New drones has no limit.
	public static int UpgradeLimitPulseLaserDamage = 4;
	public static int UpgradeLimitPulseLaserCooldown = 4;
	public static int UpgradeLimitShardDamage = 4;
	public static int UpgradeLimitShardArea = 4;
	public static int UpgradeLimitShardSpeed = 4;
	public static int UpgradeLimitHealth = 4;
	public static int UpgradeLimitMoveSpeed = 4;
	public static int UpgradeLimitHarvestRate = 4;
	public static int UpgradeLimitDroneCap = 4;

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

	/// <summary>Chance that an asteroid tumbles: it turns slowly around a random axis in 3D, on top
	/// of turning with its field. The rest only turn with their field. Applies to lone asteroids too.</summary>
	public static float AsteroidTumbleChance = 0.25f;

	/// <summary>Slowest tumble, in degrees per second.</summary>
	public static float AsteroidTumbleSpeedMin = 15f;

	/// <summary>Fastest tumble, in degrees per second.</summary>
	public static float AsteroidTumbleSpeedMax = 50f;

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

	// ───────────── Winning, losing and retreating ─────────────

	/// <summary>Enemies to destroy to win the fight. Reaching it ends the fight at once.</summary>
	public static int KillTarget = 120;

	/// <summary>The time limit, in seconds. The fight does not end there; difficulty starts to escalate.</summary>
	public static float TimeLimitSeconds = 300f;

	/// <summary>Seconds the VNP must stay on the gateway before the option to retreat appears.
	/// Leaving the gateway starts the count again.</summary>
	public static float RetreatHoldSeconds = 10f;

	// ───────────── Escalation past the time limit ─────────────
	// At the limit, and again every step after it, difficulty rises by these amounts, on top of the
	// ramp during the fight. Each is the fraction added per step: 0.6 means +60% at the limit,
	// +120% a step later, and so on. Every step flashes the screen edges red with a "!".

	/// <summary>Seconds between escalation steps after the limit.</summary>
	public static float EscalationStepSeconds = 60f;

	/// <summary>Extra health of newly spawned enemies per step.</summary>
	public static float EscalationEnemyHealthPerStep = 0.6f;

	/// <summary>Extra projectile damage of newly spawned enemies per step.</summary>
	public static float EscalationEnemyDamagePerStep = 0.5f;

	/// <summary>Extra spawn rate per step. 1 doubles it at the limit, triples it a step later.</summary>
	public static float EscalationSpawnRatePerStep = 1.0f;

	/// <summary>Extra aggro range per step, for every enemy. Wider aggro makes enemies come for the
	/// VNP from farther away, which is what pushes the player to retreat.</summary>
	public static float EscalationAggroRangePerStep = 0.4f;

	/// <summary>Seconds the red edge flash and "!" last at each step.</summary>
	public static float EscalationAlertSeconds = 1.6f;

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

	/// <summary>Scale of the drone model. 1 is about 1.3 units long.</summary>
	public static float DroneSize = 1f;

	/// <summary>Seconds a destroyed drone's wreckage takes to disintegrate.</summary>
	public static float DroneWreckTime = 0.4f;

	/// <summary>Scale of the enemy model. 1 is about 2.5 units across.</summary>
	public static float EnemySize = 1f;

	/// <summary>Scale of a piece of debris with mass 1. Heavier pieces are drawn larger,
	/// and every piece shrinks as it is harvested.</summary>
	public static float DebrisSize = 0.8f;

	/// <summary>Scale of an asteroid with mass 1, about 1.8 units across. Heavier asteroids are
	/// drawn larger, and every asteroid shrinks as it is harvested.</summary>
	public static float AsteroidSize = 1f;

	/// <summary>How many different rock shapes asteroids are drawn with.</summary>
	public static int AsteroidShapeVariants = 4;

	/// <summary>How lumpy the rock shapes are. 0 is a plain ball; 0.5 is very lumpy.</summary>
	public static float AsteroidBumpiness = 0.35f;

	/// <summary>Each asteroid is stretched or squashed along each of its own axes by a random amount
	/// between these, so asteroids with the same rock shape still differ.</summary>
	public static float AsteroidStretchMin = 0.75f;
	public static float AsteroidStretchMax = 1.15f;

	/// <summary>Seconds an enemy or the VNP flashes after being hit.</summary>
	public static float HitFlashTime = 0.12f;

	/// <summary>Spacing of the background grid lines. Every fifth line is brighter.</summary>
	public static float GridCellSize = 5f;

	public static Color DroneColor = new(0.45f, 0.8f, 1.0f);
	public static Color DroneProjectileColor = new(0.7f, 1.0f, 0.45f);
	public static Color DroneBeamColor = new(0.8f, 0.97f, 1.0f);
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
