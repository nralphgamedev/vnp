using Godot;

/// <summary>
/// One named plan a fight can follow (docs/dev/FIGHT_DIRECTOR_SPEC.md): how groups arrive between waves, its large
/// and small waves, how it grows harder, and its kill target. The waves and the difficulty curve are pieces, files of
/// their own in res://director/waves/ and res://director/curves/, which plans share. A fight picks a whole plan by
/// its faction's weights, so plans can be told apart while playing and shown on the galactic map later. Files are in
/// res://director/plans/.
///
/// The classic plan is the fights as they were before the director, kept so older batches still replay.
/// </summary>
[GlobalClass]
public partial class FightPlanDefinition : Resource
{
	[Export] public string Id;

	/// <summary>Shown in the debug line. The player isn't told the plan, at the designer's choice, until the
	/// galactic map can show it.</summary>
	[Export] public string Name;

	/// <summary>Kills that win the fight, or 0 for KillTarget in res://tuning/fight.tres. A plan that sends more
	/// enemies can ask for more, since otherwise more pressure would also mean an earlier win.</summary>
	[Export] public int KillTarget;

	// Between waves, single groups arrive in random directions off screen and roam until they notice the VNP.

	/// <summary>Groups already roaming, off screen, when the fight starts.</summary>
	[ExportGroup("Groups")]
	[Export] public int StartingGroups;

	/// <summary>Seconds between spawned groups at the start of the fight. The spawn rate climbs through the fight
	/// by the plan's difficulty curve, and past the limit by escalation.</summary>
	[Export] public float SpawnInterval;

	/// <summary>Groups spawn up to this much farther out than the spawn margin, at random. Waves always spawn at
	/// the margin, so they arrive together.</summary>
	[Export] public float SpawnExtraDistance;

	/// <summary>The share of groups placed ahead of the VNP while it moves, rather than in a random direction, so
	/// running in a straight line leads into fresh enemies. 0 is none, as before.</summary>
	[Export] public float AheadShare;

	/// <summary>Groups placed ahead land within this angle of the VNP's heading, in degrees, across.</summary>
	[Export] public float AheadSpreadDegrees = 60f;

	/// <summary>How far beyond the spawn margin the starting groups, and groups that keep the population up, can be
	/// placed, at random: 0 for SpawnExtraDistance. A large value fills the area around the gateway with enemies
	/// already there, rather than ringing the screen. Keep it within DespawnDistance in enemy_spawning.tres, past
	/// which enemies are removed.</summary>
	[Export] public float FieldExtraDistance;

	/// <summary>Starting groups are placed on screen as well as off it, in every plan: anywhere from this far beyond
	/// their aggro range out to FieldExtraDistance (or SpawnExtraDistance) beyond the screen, so a fight starts among
	/// enemies that haven't noticed the VNP yet, rather than with an empty screen.</summary>
	[Export] public float StartClearance = 12f;

	/// <summary>Enemies the director keeps alive at the least: while fewer are, it adds a group every RefillSeconds,
	/// spread like the starting groups, so the field stays dense however many are killed. 0 for none. Capped by
	/// the faction's MaxAlive.</summary>
	[Export] public int KeepAlive;

	/// <summary>How much KeepAlive grows each minute of fight time.</summary>
	[Export] public float KeepAlivePerMinute;

	/// <summary>Seconds between groups added to keep the population up.</summary>
	[Export] public float RefillSeconds = 0.5f;

	/// <summary>Multiplies the faction's group weights for this plan's groups between waves, by type: 0.5 makes a
	/// type half as common. A type not listed keeps its weight. Waves use the faction's wave weights as they are.</summary>
	[Export] public Godot.Collections.Dictionary<EnemyDefinition, float> GroupWeightScales;

	/// <summary>How long idle enemies head one way before picking a new direction, as a multiple of the usual 1.5 to
	/// 4 seconds: 2 makes each leg twice as long, so idle groups roam twice as far each way.</summary>
	[Export] public float WanderLegScale = 1f;

	/// <summary>Health and debris of the groups between waves, as a multiple of their type's: 1 as usual, less for
	/// fragile enemies that come in large numbers, such as the horde plan's. Their damage is unchanged.</summary>
	[Export] public float GroupStrength = 1f;

	/// <summary>The announced waves: warned of on screen and by sound, numbered on the banner, shaking the
	/// screen. Empty for none, as in a plan that is one continuous stream.</summary>
	[ExportGroup("Pieces")]
	[Export] public WavesDefinition LargeWaves;

	/// <summary>Unannounced waves, with no warning, banner or shake, usually smaller and more frequent: steady
	/// pressure between the large waves. Empty for none.</summary>
	[Export] public WavesDefinition SmallWaves;

	/// <summary>Streams of single enemies, one after another, which can multiply and turn into a screw. Empty for
	/// none.</summary>
	[Export] public StreamsDefinition Streams;

	/// <summary>How the fight grows harder before the time limit.</summary>
	[Export] public DifficultyCurveDefinition Curve;
}
