using Godot;

/// <summary>
/// A weapon of shards that circle the VNP and damage enemies they touch. Cooldown is how long before a
/// shard can hit the same enemy again. It only has to stop one pass counting twice, so keep it below
/// the time a shard takes to go round once (360 / orbit speed, 1.8 seconds at 200). Above that, speed
/// upgrades stop adding damage.
/// </summary>
[GlobalClass]
public partial class OrbitalShardsDefinition : WeaponDefinition
{
	/// <summary>How many shards circle the VNP.</summary>
	[Export] public int Count;

	/// <summary>How far from the VNP the shards circle. Enemies hold about 11 units from their target,
	/// so shards mostly hit when the VNP closes in on enemies.</summary>
	[Export] public float OrbitRadius;

	/// <summary>How fast the shards circle, in degrees per second. Faster shards pass each enemy more often,
	/// so they deal more damage.</summary>
	[Export] public float OrbitSpeed;

	/// <summary>How close to a shard an enemy's edge must come to be hit.</summary>
	[Export] public float HitRadius;

	/// <summary>Scale of a shard. 1 is 2 units long.</summary>
	[Export] public float Size;
}
