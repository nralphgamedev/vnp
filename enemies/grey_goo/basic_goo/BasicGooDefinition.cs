using Godot;

/// <summary>
/// A Goo: a slow blob that absorbs any drone that touches it, grows with each one, and splits in two once
/// it has absorbed enough. It damages the VNP only by touching it. Goo.cs runs it.
///
/// A Goo's growth is kept as one number, its bulk: 1 for a Goo as it spawns. Each absorbed drone adds
/// BulkPerDrone. Health scales with bulk, and size with its square root, so a Goo twice the bulk covers
/// twice the area. A split shares the bulk, health and absorbed drones evenly between the two halves.
/// </summary>
[GlobalClass]
public partial class GooDefinition : EnemyDefinition
{
	/// <summary>Damage to the VNP each time a Goo touches it, at the start of the fight.</summary>
	[ExportGroup("Attacking")]
	[Export] public float ContactDamage;

	/// <summary>Seconds after touching the VNP before the same Goo can damage it again.</summary>
	[Export] public float ContactCooldown;

	/// <summary>Health a Goo drains each second from every drone touching it, at the start of the fight. A
	/// drone drained to nothing is absorbed. Higher absorbs drones faster; lower lets a drone that strays in,
	/// or a ramming drone passing through, get away with only some damage.</summary>
	[Export] public float DroneDrainPerSecond;

	/// <summary>Bulk each absorbed drone adds, where a newly spawned Goo has a bulk of 1. 0.25 makes a Goo
	/// that has absorbed four drones twice the health of a new one, and about 1.4 times as wide.</summary>
	[ExportGroup("Absorbing")]
	[Export] public float BulkPerDrone;

	/// <summary>Drones a Goo absorbs before it splits in two. Each half starts counting again from 0.</summary>
	[Export] public int DronesToSplit;

	/// <summary>Seconds a Goo bulges before it splits, so the player can see it coming.</summary>
	[Export] public float SplitBulgeSeconds;

	/// <summary>Mass added to a Goo's debris for each drone it absorbed, on top of its own debris mass.
	/// The halves of a split each carry half.</summary>
	[Export] public float DebrisMassPerDrone;

	/// <summary>How far each wobble squashes and stretches the blob, as a fraction of its size.</summary>
	[ExportGroup("Look")]
	[Export] public float Wobble;

	/// <summary>Wobbles per second.</summary>
	[Export] public float WobbleSpeed;

	/// <summary>How much the blob swells for a moment when it absorbs a drone, as a fraction of its size.</summary>
	[Export] public float AbsorbPulse;

	/// <summary>Seconds the swell takes to settle.</summary>
	[Export] public float AbsorbPulseSeconds;
}
