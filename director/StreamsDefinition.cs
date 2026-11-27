using Godot;

/// <summary>
/// Streams of single enemies coming in one after another, a piece of a plan (docs/dev/FIGHT_DIRECTOR_SPEC.md). Files
/// are in res://director/streams/, and plans share them.
///
/// One stream starts at FirstSeconds: single enemies spawned just off screen from one direction around the VNP, a
/// gap apart, each hunting it, so they come in as a line. The gap shortens through the fight. At each time in
/// AddStreamSeconds another stream is added, the streams spreading evenly round the VNP (two facing each other,
/// three a third of the way round, and so on), and each stream's gap lengthens by IntervalPerExtraStream, so the total
/// rate steps up rather than doubling. Every stream's spawn point turns round the VNP all the while, slowly at first
/// and faster from RotateFromSeconds, so the lines curve into the arms of a screw.
/// </summary>
[GlobalClass]
public partial class StreamsDefinition : Resource
{
	[Export] public string Id;

	/// <summary>Fight time, in seconds, when the first stream starts.</summary>
	[ExportGroup("Rate")]
	[Export] public float FirstSeconds;

	/// <summary>Seconds between one stream's enemies at the start of the fight.</summary>
	[Export] public float IntervalSeconds = 1.5f;

	/// <summary>How much faster each stream spawns each minute: 0.5 means half again as fast after one minute.</summary>
	[Export] public float RatePerMinute;

	/// <summary>The shortest gap between one stream's enemies, before the share for extra streams.</summary>
	[Export] public float MinIntervalSeconds = 0.5f;

	/// <summary>Fight times, in seconds, at which another stream is added. At most 8 streams.</summary>
	[ExportGroup("More streams")]
	[Export] public Godot.Collections.Array<float> AddStreamSeconds = new();

	/// <summary>How much longer each stream's gap is for each stream beyond the first: 0.5 makes each of two streams
	/// spawn at two thirds of the rate one would, so the total rate rises by a third, not double.</summary>
	[Export] public float IntervalPerExtraStream = 0.5f;

	/// <summary>How fast the spawn points turn round the VNP from the start, in degrees a second, all the same
	/// way. 0 holds them still until RotateFromSeconds.</summary>
	[ExportGroup("Screw")]
	[Export] public float StartRotationDegreesPerSecond = 3f;

	/// <summary>Fight time, in seconds, from which they turn at RotationDegreesPerSecond instead. 0 for never.</summary>
	[Export] public float RotateFromSeconds;

	/// <summary>How fast the spawn points turn from RotateFromSeconds, in degrees a second.</summary>
	[Export] public float RotationDegreesPerSecond = 10f;

	/// <summary>How far each enemy's spawn point strays from its stream's direction, either way, in degrees, so a
	/// line isn't ruler-straight.</summary>
	[ExportGroup("Enemies")]
	[Export] public float JitterDegrees = 3f;

	/// <summary>Health and debris of the streams' enemies, as a multiple of their type's. Their damage is
	/// unchanged.</summary>
	[Export] public float Strength = 1f;

	/// <summary>What the enemies are, by the faction's wave weights and tiers, as for waves.</summary>
	[Export] public WaveMakeup Makeup;

	/// <summary>Growing: the share of enemies drawn from harder tiers at the start of the fight.</summary>
	[Export] public float HardShareStart;

	/// <summary>Growing: how much that share grows each minute of fight time.</summary>
	[Export] public float HardSharePerMinute;

	/// <summary>Growing: the most that share grows to.</summary>
	[Export] public float HardShareMax = 1f;
}
