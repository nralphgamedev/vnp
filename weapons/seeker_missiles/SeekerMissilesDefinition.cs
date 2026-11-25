using Godot;

/// <summary>
/// A weapon that fires volleys of slow missiles that home in on enemies. Each missile heads for the
/// nearest enemy when fired, and picks the nearest enemy again if its target is destroyed first. The
/// volley fans out as it launches, then each missile curves onto its target.
/// </summary>
[GlobalClass]
public partial class SeekerMissilesDefinition : WeaponDefinition
{
	/// <summary>Fires when an enemy is this close, and a missile looks this far for a new target.</summary>
	[Export] public float Range;

	/// <summary>Missiles in each volley.</summary>
	[Export] public int MissilesPerVolley;

	/// <summary>Speed of a missile, in units per second. Slow, so the homing can be seen.</summary>
	[Export] public float MissileSpeed;

	/// <summary>How fast a missile turns toward its target, in degrees per second.</summary>
	[Export] public float TurnRate;

	/// <summary>Seconds a missile flies before it fizzles out if it hits nothing.</summary>
	[Export] public float MissileLifetime;

	/// <summary>How far apart the volley fans out at launch, in degrees, from the first missile to the last.</summary>
	[Export] public float LaunchSpread;

	/// <summary>Scale of a missile.</summary>
	[Export] public float MissileSize;

	/// <summary>How far from where a missile hits its explosion reaches, damaging every enemy within it (and, since
	/// it is explosive, breaking fragile enemy shots there). Area of effect upgrades widen it.</summary>
	[Export] public float BlastRadius;
}
