using Godot;

/// <summary>Where, when and how many enemies spawn, including waves. Values live in res://tuning/enemy_spawning.tres.</summary>
[GlobalClass]
public partial class EnemySpawningTuning : Resource
{
	/// <summary>Groups already roaming, off screen, when the fight starts.</summary>
	[ExportGroup("Spawning")]
	[Export] public int StartingGroups;

	/// <summary>The enemy type that spawns. Files in res://enemies/.</summary>
	[Export] public EnemyDefinition Enemy;

	/// <summary>Enemies in each spawned group.</summary>
	[Export] public int GroupSize;

	/// <summary>Seconds between spawned groups at the start of the fight.</summary>
	[Export] public float SpawnInterval;

	/// <summary>How far past the edge of the screen enemy groups and waves spawn. Measured from what
	/// the camera actually shows, so it keeps them out of sight at any zoom. It must cover a group's
	/// spread (4 units) and an enemy's size, with room to spare.</summary>
	[Export] public float SpawnMargin;

	/// <summary>Ordinary groups spawn up to this much farther out than the margin, at random.
	/// Waves always spawn at the margin, so they arrive together.</summary>
	[Export] public float SpawnExtraDistance;

	/// <summary>No group spawns while this many enemies are alive. Raise it to test performance.</summary>
	[Export] public int MaxAlive;

	/// <summary>An enemy this far from the VNP is removed without counting as a kill,
	/// so enemies left behind do not use up the cap.</summary>
	[Export] public float DespawnDistance;

	// Every so often a wave arrives: several groups at once, spaced evenly on a ring just off screen
	// around the VNP, all hunting it from the start. Wave enemies chase from any distance and never
	// give up. They have the same strength as other enemies spawned at that moment, and ignore
	// MaxAlive.

	/// <summary>Fight time, in seconds, when the first wave arrives.</summary>
	[ExportGroup("Waves")]
	[Export] public float WaveFirstSeconds;

	/// <summary>Seconds between waves.</summary>
	[Export] public float WaveIntervalSeconds;

	/// <summary>Groups in the first wave. Each group is GroupSize enemies.</summary>
	[Export] public int WaveGroups;

	/// <summary>Groups added to each wave after the first.</summary>
	[Export] public int WaveGroupsAddedPerWave;

	/// <summary>Seconds of "WAVE INCOMING" warning before a wave arrives.</summary>
	[Export] public float WaveWarningSeconds;

	/// <summary>Seconds the "WAVE n" banner stays up after a wave arrives.</summary>
	[Export] public float WaveBannerSeconds;
}
