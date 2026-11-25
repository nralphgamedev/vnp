using Godot;

/// <summary>
/// The Devourer, a Grey Goo type: it slowly consumes asteroids and debris, growing as it does up to a large limit,
/// and drains and absorbs drones like the basic goo, but never splits or chases. The more it has consumed, the more
/// its debris is worth. Everything else, touch damage, draining drones, growth per drone and the wobbling look, is
/// shared by the Grey Goo types (GreyGooDefinition). It stores what it consumes as its own growth, and spends it only
/// while the VNP is near, producing basic goos and pushing them out at the VNP. Devourer.cs runs it.
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

	/// <summary>The most bulk it can reach, from consuming and from drones together. Once there it is full and stops
	/// consuming until producing makes room.</summary>
	[Export] public float MaxBulk;

	/// <summary>How much of the mass it consumed is added to its debris when destroyed, from 0 to 1.</summary>
	[Export] public float DebrisShareOfConsumed;

	/// <summary>The enemy it produces, the basic goo. Null produces nothing.</summary>
	[ExportGroup("Producing")]
	[Export] public EnemyDefinition Produces;

	/// <summary>It produces only while the VNP is within this distance of its edge: it stores mass as it consumes, and
	/// spends it only when attacking.</summary>
	[Export] public float AttackRange;

	/// <summary>Stored mass each enemy it produces costs. Producing spends it: the Devourer gives back the growth that
	/// mass gave it (BulkPerMass of it, never below its starting size), and the mass no longer counts toward its
	/// debris. With less stored than this, it can't produce.</summary>
	[Export] public float ProduceMass;

	/// <summary>The least time between two it produces, in seconds. Its first can come as soon as the VNP is near.</summary>
	[Export] public float ProduceCooldown;

	/// <summary>How fast a produced enemy is pushed out toward the VNP. It then slows to its own speed at its own
	/// acceleration and moves as usual.</summary>
	[Export] public float ProducePushSpeed;
}
