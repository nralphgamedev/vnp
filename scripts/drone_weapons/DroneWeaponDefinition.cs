using Godot;

/// <summary>How a Drone Weapon attacks. Each kind is a behaviour written in code.</summary>
public enum DroneWeaponKind
{
	/// <summary>Fires single projectiles, aimed ahead of a moving enemy.</summary>
	Projectile,

	/// <summary>A short-range beam that never misses and deals damage in ticks.</summary>
	Beam,

	/// <summary>The drones charge through enemies, damaging them on impact, then arc back round and charge again.</summary>
	Ram,
}

/// <summary>
/// A Drone Weapon: the attack every drone uses. All of a VNP's drones share one. A new drone weapon
/// that reuses a kind is a new folder in res://tuning/drone_weapons/, holding its .tres file and,
/// in upgrades/, the upgrade that switches the drones to it.
/// </summary>
[GlobalClass]
public partial class DroneWeaponDefinition : Resource
{
	/// <summary>Stays the same if the name changes. Used to tell weapons apart.</summary>
	[Export] public string Id;

	/// <summary>Shown to the player.</summary>
	[Export] public string Name;

	[Export] public DroneWeaponKind Kind;

	/// <summary>Damage of one shot, beam tick or ram.</summary>
	[Export] public float Damage;

	/// <summary>Seconds between shots, beam ticks or rams from one drone.</summary>
	[Export] public float Cooldown;

	/// <summary>A drone attacks once its target is this close: fires, beams, or starts a charge. Keep it
	/// above the attack distance.</summary>
	[Export] public float Range;

	/// <summary>How far from its target a drone sits while surrounding it.</summary>
	[Export] public float AttackDistance;

	/// <summary>Speed of a projectile. Only used by the Projectile kind.</summary>
	[Export] public float ProjectileSpeed;

	/// <summary>How fast a drone charges, in units per second. Only used by the Ram kind.</summary>
	[Export] public float ChargeSpeed;

	/// <summary>How fast a charging drone turns to follow its target, in degrees a second. Low, so a charge
	/// runs nearly straight. Only used by the Ram kind.</summary>
	[Export] public float ChargeTurnRate;

	/// <summary>How fast a drone turns while it arcs back round after a charge, in degrees a second. Higher
	/// makes a tighter loop. Only used by the Ram kind.</summary>
	[Export] public float ArcTurnRate;

	/// <summary>How quickly a drone slows from charge speed to its normal speed as it arcs round, in units
	/// a second per second. Lower makes it glide further past the target. Only used by the Ram kind.</summary>
	[Export] public float ArcSlowdown;

	/// <summary>Colour of the projectiles or beam.</summary>
	[Export] public Color Color;
}
