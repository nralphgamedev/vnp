using Godot;

/// <summary>When a fight's waves come.</summary>
public enum WaveTiming
{
	/// <summary>A fixed gap between waves.</summary>
	Steady,
	/// <summary>The gaps shorten wave by wave, down to MinIntervalSeconds.</summary>
	SpeedingUp,
	/// <summary>Random gaps around IntervalSeconds, so the next wave can't be timed.</summary>
	Irregular,
	/// <summary>WavesPerSurge waves close together, SecondsWithinSurge apart, then a quiet stretch until the next
	/// surge, IntervalSeconds after the last one began.</summary>
	Surge,
}

/// <summary>Where a wave's groups arrive, around the VNP, just off screen.</summary>
public enum WaveArrival
{
	/// <summary>Evenly all round the VNP.</summary>
	Ring,
	/// <summary>From one direction, spread across SpreadDegrees, so there is a safe side.</summary>
	Front,
	/// <summary>From two opposite directions, each spread across SpreadDegrees.</summary>
	Pincer,
}

/// <summary>What a wave's groups are, by the faction's wave weights and its types' tiers.</summary>
public enum WaveMakeup
{
	/// <summary>Each group's type drawn from every type by the wave weights.</summary>
	Mixed,
	/// <summary>Mostly the easiest tier, with a share of harder types that grows through the fight.</summary>
	Growing,
	/// <summary>Only the easiest tier.</summary>
	Swarm,
}

/// <summary>
/// One kind of wave a plan sends: when the waves come, where from, what they are made of, and how many groups
/// (docs/dev/FIGHT_DIRECTOR_SPEC.md). A plan has large waves, announced, and may have small ones, unannounced. A
/// piece of a plan: each is a file in res://director/waves/, and plans share them.
///
/// Wave enemies hunt the VNP from the moment they arrive, chase from any distance and never give up. They have the
/// same strength as other enemies spawned at that moment, and ignore the faction's MaxAlive.
/// </summary>
[GlobalClass]
public partial class WavesDefinition : Resource
{
	[Export] public string Id;

	/// <summary>Groups in the first wave. Each group is its type's group size.</summary>
	[ExportGroup("Size")]
	[Export] public int Groups;

	/// <summary>Groups added to each wave after the first.</summary>
	[Export] public int GroupsAddedPerWave;

	[ExportGroup("Timing")]
	[Export] public WaveTiming Timing;

	/// <summary>Fight time, in seconds, when the first wave arrives.</summary>
	[Export] public float FirstSeconds;

	/// <summary>Seconds between waves: for Steady, the gap; for SpeedingUp, the first gap; for Irregular, the
	/// average gap; for Surge, from the start of one surge to the start of the next.</summary>
	[Export] public float IntervalSeconds;

	/// <summary>SpeedingUp: each gap is this times the one before.</summary>
	[Export] public float IntervalScalePerWave = 1f;

	/// <summary>SpeedingUp: the gaps never shorten below this.</summary>
	[Export] public float MinIntervalSeconds;

	/// <summary>Irregular: how far a gap can stray from IntervalSeconds, as a share of it, either way.</summary>
	[Export] public float IntervalSpread;

	/// <summary>Surge: waves in each surge.</summary>
	[Export] public int WavesPerSurge = 1;

	/// <summary>Surge: seconds between the waves within a surge.</summary>
	[Export] public float SecondsWithinSurge;

	[ExportGroup("Arrival")]
	[Export] public WaveArrival Arrival;

	/// <summary>Front and Pincer: the angle each side's groups are spread across, in degrees.</summary>
	[Export] public float SpreadDegrees;

	[ExportGroup("Makeup")]
	[Export] public WaveMakeup Makeup;

	/// <summary>Growing: the share of groups drawn from harder tiers at the start of the fight.</summary>
	[Export] public float HardShareStart;

	/// <summary>Growing: how much that share grows each minute of fight time.</summary>
	[Export] public float HardSharePerMinute;

	/// <summary>Growing: the most that share grows to.</summary>
	[Export] public float HardShareMax = 1f;
}
