using Godot;

/// <summary>
/// A VNP Weapon: its ID, name and the numbers it starts the fight with. Which subclass it is decides
/// how it behaves, so a new weapon that works like an existing one is just a new folder in
/// res://weapons/, holding its .tres file and, in upgrades/, its upgrades. When equipped, the
/// weapon copies these numbers; upgrades then change its own copy.
/// </summary>
[GlobalClass]
public partial class WeaponDefinition : Resource
{
	/// <summary>Stays the same if the name changes. Used to tell weapons apart.</summary>
	[Export] public string Id;

	/// <summary>Shown to the player.</summary>
	[Export] public string Name;

	/// <summary>Damage of one hit.</summary>
	[Export] public float Damage;

	/// <summary>Seconds between shots, or between hits on the same enemy for weapons that touch.</summary>
	[Export] public float Cooldown;

	/// <summary>Colour of the weapon's shots or shapes.</summary>
	[Export] public Color Color;
}
