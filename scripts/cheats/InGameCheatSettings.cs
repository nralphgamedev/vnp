#if TOOLS
using Godot;

/// <summary>
/// Settings for the playtest cheats. Values live in res://scripts/cheats/in_game_cheats.tres. Editor only, like
/// everything in scripts/cheats/: an exported game never has these settings, and plays as if every cheat
/// were off.
/// </summary>
[GlobalClass]
public partial class InGameCheatSettings : Resource
{
	/// <summary>Each fight starts with a menu to choose the starting VNP Weapon from StartingWeaponChoices,
	/// in place of StartingWeapons in vnp.tres, then the starting Drone Weapon from
	/// StartingDroneWeaponChoices, in place of the one in drones.tres. Turn off to play as an exported
	/// game would.</summary>
	[ExportGroup("Starting weapons")]
	[Export] public bool ChooseStartingWeapon;

	/// <summary>The weapons offered when ChooseStartingWeapon is on, in the order shown. Leave it empty to
	/// skip that page and start with StartingWeapons in vnp.tres.</summary>
	[Export] public Godot.Collections.Array<WeaponDefinition> StartingWeaponChoices = new();

	/// <summary>The Drone Weapons offered when ChooseStartingWeapon is on, in the order shown. Leave it
	/// empty to skip that page and start with the Drone Weapon in drones.tres.</summary>
	[Export] public Godot.Collections.Array<DroneWeaponDefinition> StartingDroneWeaponChoices = new();
}
#endif
