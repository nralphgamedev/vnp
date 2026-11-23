using Godot;

/// <summary>
/// What every Grey Goo type has, on top of what every enemy has: touching the VNP to damage it, draining and
/// absorbing drones for the types that do, growing with what it absorbs, and the look of a wobbling blob. Each
/// type adds its own values in a subclass in its own folder below res://enemies/grey_goo/, such as
/// BasicGooDefinition, with the code that runs it (a GreyGoo subclass) beside it.
///
/// A Grey Goo's growth is kept as one number, its bulk: 1 as it spawns. Each absorbed drone adds BulkPerDrone.
/// Health scales with bulk, and size with its square root, so twice the bulk covers twice the area.
/// </summary>
[GlobalClass]
public partial class GreyGooDefinition : EnemyDefinition
{
	/// <summary>Damage to the VNP each time it touches it, at the start of the fight. Every Grey Goo type
	/// damages the VNP by touch.</summary>
	[ExportGroup("Attacking")]
	[Export] public float ContactDamage;

	/// <summary>Seconds after touching the VNP before the same one can damage it again.</summary>
	[Export] public float ContactCooldown;

	/// <summary>Health it drains each second from every drone touching it, at the start of the fight. A drone
	/// drained to nothing is absorbed. Higher absorbs drones faster; lower lets a drone that strays in, or a
	/// ramming drone passing through, get away with only some damage. 0 for a type that doesn't drain or absorb
	/// drones by touch.</summary>
	[Export] public float DroneDrainPerSecond;

	/// <summary>Bulk each absorbed drone adds, where a newly spawned one has a bulk of 1. 0.25 makes one that
	/// has absorbed four drones twice the health of a new one, and about 1.4 times as wide.</summary>
	[ExportGroup("Absorbing")]
	[Export] public float BulkPerDrone;

	/// <summary>Mass added to its debris for each drone it absorbed, on top of its own debris mass, so
	/// destroying one that grew wins back some of what it took.</summary>
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
