using Godot;

/// <summary>
/// The Devourer, a Grey Goo type: it slowly consumes asteroids and debris, growing as it does up to a large limit,
/// and drains and absorbs drones like the basic goo, but never splits or chases. The more it has consumed, the more
/// its debris is worth. Everything else, touch damage, draining drones, growth per drone and the wobbling look, is
/// shared by the Grey Goo types (GreyGooDefinition). Devourer.cs runs it.
/// </summary>
[GlobalClass]
public partial class DevourerDefinition : GreyGooDefinition
{
	/// <summary>How far it looks for material to consume, from its centre. Material is only kept findable within
	/// ActiveRadius of the VNP (asteroids.tres), so a range beyond that finds nothing more. It never chases the VNP
	/// or drones, so its aggro ranges are unused.</summary>
	[ExportGroup("Consuming")]
	[Export] public float SeekRange;

	/// <summary>Mass a second it draws out of material it touches. None of it becomes fight experience.</summary>
	[Export] public float ConsumePerSecond;

	/// <summary>How much it grows for each unit of mass consumed, as bulk (1 as it spawns; size follows its square
	/// root, and health rises by the same share).</summary>
	[Export] public float BulkPerMass;

	/// <summary>The most bulk it can reach, from consuming and from drones together.</summary>
	[Export] public float MaxBulk;

	/// <summary>How much of the mass it consumed is added to its debris when destroyed, from 0 to 1.</summary>
	[Export] public float DebrisShareOfConsumed;
}
