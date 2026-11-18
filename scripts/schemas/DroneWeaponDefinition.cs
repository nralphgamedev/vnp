using Godot;

/// <summary>How a Drone Weapon attacks. Each kind is a behaviour written in code.</summary>
public enum DroneWeaponKind
{
	/// <summary>Fires single projectiles, aimed ahead of a moving enemy.</summary>
	Projectile,

	/// <summary>A short-range beam that never misses and deals damage in ticks.</summary>
	Beam,
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

	/// <summary>Damage of one shot or beam tick.</summary>
	[Export] public float Damage;

	/// <summary>Seconds between shots or beam ticks from one drone.</summary>
	[Export] public float Cooldown;

	/// <summary>A drone attacks once its target is this close. Keep it above the attack distance.</summary>
	[Export] public float Range;

	/// <summary>How far from its target a drone sits while surrounding it.</summary>
	[Export] public float AttackDistance;

	/// <summary>Speed of a projectile. Only used by the Projectile kind.</summary>
	[Export] public float ProjectileSpeed;

	/// <summary>Colour of the projectiles or beam.</summary>
	[Export] public Color Color;
}
