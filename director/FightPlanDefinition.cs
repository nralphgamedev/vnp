using Godot;

/// <summary>
/// One plan a fight can follow: when and where enemies arrive, and how waves grow (docs/dev/FIGHT_DIRECTOR_SPEC.md).
/// The Fight Director picks one for each fight, by the weights in res://director/fight_director.tres. Files are in
/// res://director/plans/.
///
/// Only the classic plan exists so far: the fights as they were before the director. Its values moved here
/// unchanged from res://tuning/enemy_spawning.tres, which keeps what every plan shares, such as the enemy types'
/// weights, the spawn margin and the wave warning. Later stages add other pieces to choose from.
/// </summary>
[GlobalClass]
public partial class FightPlanDefinition : Resource
{
	[Export] public string Id;

	/// <summary>Shown in the debug line. The player isn't told the plan, at the designer's choice, until the
	/// galactic map can show it.</summary>
	[Export] public string Name;

	// Between waves, single groups arrive in random directions off screen and roam until they notice the VNP.

	/// <summary>Groups already roaming, off screen, when the fight starts.</summary>
	[ExportGroup("Groups")]
	[Export] public int StartingGroups;

	/// <summary>Seconds between spawned groups at the start of the fight. The spawn rate climbs through the fight
	/// by the ramp and escalation in res://tuning/difficulty.tres.</summary>
	[Export] public float SpawnInterval;

	/// <summary>Groups spawn up to this much farther out than the spawn margin, at random. Waves always spawn at
	/// the margin, so they arrive together.</summary>
	[Export] public float SpawnExtraDistance;

	// Every so often a wave arrives: several groups at once, spaced evenly on a ring just off screen around the
	// VNP, all hunting it from the start. Wave enemies chase from any distance and never give up. They have the
	// same strength as other enemies spawned at that moment, and ignore MaxAlive.

	/// <summary>Fight time, in seconds, when the first wave arrives.</summary>
	[ExportGroup("Waves")]
	[Export] public float WaveFirstSeconds;

	/// <summary>Seconds between waves.</summary>
	[Export] public float WaveIntervalSeconds;

	/// <summary>Groups in the first wave. Each group is its type's group size.</summary>
	[Export] public int WaveGroups;

	/// <summary>Groups added to each wave after the first.</summary>
	[Export] public int WaveGroupsAddedPerWave;
}
