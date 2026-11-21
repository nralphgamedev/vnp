using Godot;

/// <summary>The VNP: movement, health and weapon slots. Values live in res://tuning/vnp.tres.</summary>
[GlobalClass]
public partial class VnpTuning : Resource
{
	/// <summary>Top speed, in units per second, with the stick or keys fully held.</summary>
	[ExportGroup("Movement")]
	[Export] public float MoveSpeed;

	/// <summary>How quickly the VNP gains speed or changes direction, in units per second squared.
	/// Higher is snappier. At 80 it reaches top speed in a quarter of a second.</summary>
	[Export] public float Acceleration;

	/// <summary>How quickly the VNP slows once input is released, in units per second squared.
	/// Higher stops sooner. Lower makes it drift.</summary>
	[Export] public float Deceleration;

	/// <summary>How fast the ship model turns to face its direction of travel, in degrees per second.
	/// Cosmetic only, because weapons aim on their own.</summary>
	[Export] public float TurnSpeed;

	/// <summary>Joystick movement below this fraction of full tilt is ignored.</summary>
	[Export] public float StickDeadzone;

	/// <summary>Health the VNP starts the fight with.</summary>
	[ExportGroup("Health")]
	[Export] public float MaxHealth;

	/// <summary>How close an enemy projectile must pass to the VNP's center to hit it.</summary>
	[Export] public float HitRadius;

	/// <summary>How many VNP Weapons the VNP can carry. The Pulse Laser takes the first slot.</summary>
	[ExportGroup("Weapons")]
	[Export] public int WeaponSlots;

	/// <summary>VNP Weapons the VNP starts the fight with, in slot order. One folder each in res://weapons/.
	/// In the editor, the starting weapon playtest cheat (scripts/cheats/cheats.tres) can choose them instead.</summary>
	[Export] public Godot.Collections.Array<WeaponDefinition> StartingWeapons = new();

	/// <summary>Scale of the VNP model. 1 is about 3 units long.</summary>
	[ExportGroup("Look")]
	[Export] public float Size;
}
