using Godot;

/// <summary>
/// What enemy spawning shares whatever the fight's faction and plan: how far off screen enemies spawn and are
/// removed, and the wave warning. Which types spawn, how often and how many at most is the faction's (such as
/// res://enemies/grey_goo/grey_goo.tres); when groups and waves come, and how large waves grow, is the plan's, in
/// res://director/plans/ (docs/dev/FIGHT_DIRECTOR_SPEC.md). Values live in res://tuning/enemy_spawning.tres.
/// </summary>
[GlobalClass]
public partial class EnemySpawningTuning : Resource
{
	/// <summary>How far past the edge of the screen enemy groups and waves spawn. Measured from what
	/// the camera actually shows, so it keeps them out of sight at any zoom. It must cover a group's
	/// spread (4 units) and an enemy's size, with room to spare.</summary>
	[ExportGroup("Spawning")]
	[Export] public float SpawnMargin;

	/// <summary>An enemy this far from the VNP is removed without counting as a kill,
	/// so enemies left behind do not use up the cap.</summary>
	[Export] public float DespawnDistance;

	/// <summary>How strongly idle enemies are pulled back to where they spawned (or last gave up a chase), as a
	/// multiple: 1 keeps groups near home, within each type's WanderRadius; 0, the designer's choice, lets idle groups
	/// roam freely, still keeping together.</summary>
	[Export] public float WanderHomePull;

	/// <summary>Group aggro: when an idle enemy notices the VNP by sight, every idle enemy within this many units of
	/// it notices too, and chases while the VNP is within its LoseAggroRange. One step only: those alerted don't alert
	/// others. 0 for none. At 18, a group's spotter alerts its own group and any group touching it.</summary>
	[Export] public float AlertRadius;

	/// <summary>Seconds of "WAVE INCOMING" warning before a wave arrives.</summary>
	[ExportGroup("Waves")]
	[Export] public float WaveWarningSeconds;

	/// <summary>Seconds the "WAVE n" banner stays up after a wave arrives.</summary>
	[Export] public float WaveBannerSeconds;
}
