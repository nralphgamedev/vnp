using Godot;

/// <summary>
/// One faction a fight can be against, as data for the Fight Director (docs/dev/FIGHT_DIRECTOR_SPEC.md): its enemy
/// types and how often each spawns, how hard each is, the plans its fights can follow, and its limits. There is one
/// director for every faction; a faction that needs something new adds a new kind of plan piece, which any faction
/// can then use. What each enemy does stays in its type's own code and file.
///
/// Each faction's file sits with its enemy types, such as res://enemies/grey_goo/grey_goo.tres. Grey Goo is the
/// only one so far, and every fight is against it (DefaultFaction in res://director/fight_director.tres) until the
/// galactic map chooses.
/// </summary>
[GlobalClass]
public partial class FactionDefinition : Resource
{
	[Export] public string Id;

	[Export] public string Name;

	/// <summary>The enemy types that spawn in groups, each with a weight: each group is one type, picked at
	/// random, and a type with twice the weight of another is picked twice as often. A weight of 0 never spawns it,
	/// but the fight still knows the type, so it can be spawned another way (an asteroid mimic hidden in a field, a
	/// split, or the scenario harness's spawn=). Each type's group size is in its own file in res://enemies/.</summary>
	[ExportGroup("Enemy types")]
	[Export] public Godot.Collections.Dictionary<EnemyDefinition, float> GroupWeights;

	/// <summary>The enemy types in waves, each with a weight, picked for each group of a wave as for
	/// GroupWeights.</summary>
	[Export] public Godot.Collections.Dictionary<EnemyDefinition, float> WaveGroupWeights;

	/// <summary>How hard each type is, from 1, the easiest, up. Growing waves draw mostly from the easiest tier
	/// with harder ones peppered in, and swarm waves only from the easiest, so a new type only needs its tier. A
	/// type not listed is tier 1.</summary>
	[Export] public Godot.Collections.Dictionary<EnemyDefinition, int> Tiers;

	/// <summary>The plans this faction's fights can follow, each with a weight, picked as for GroupWeights.</summary>
	[ExportGroup("Plans")]
	[Export] public Godot.Collections.Dictionary<FightPlanDefinition, float> Plans;

	/// <summary>No group spawns while this many enemies are alive. Waves ignore it. Raise it to test
	/// performance.</summary>
	[ExportGroup("Limits")]
	[Export] public int MaxAlive;
}
