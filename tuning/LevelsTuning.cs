using Godot;

/// <summary>Fight experience per level, level-up offers, and the upgrade pool. Values live in res://tuning/levels.tres.</summary>
[GlobalClass]
public partial class LevelsTuning : Resource
{
	/// <summary>Fight experience needed to go from level 1 to level 2.</summary>
	[ExportGroup("Levels")]
	[Export] public float ExperienceForFirstLevel;

	/// <summary>How much more each level after that needs than the one before.
	/// At 150 and 75, level 5 takes 1050 in total and level 10 takes 4050.</summary>
	[Export] public float ExperienceIncreasePerLevel;

	/// <summary>Each level's increase is this times the one before, so late levels grow dearer ever more gently: at
	/// 0.95, with 150 and 75 above, level 5 needs 428, level 10 705, level 20 about 1,080 and level 30 about 1,330,
	/// levelling off near 1,650. 1 keeps the increase the same every level.</summary>
	[Export] public float ExperienceIncreaseDecay = 1f;

	/// <summary>How many upgrades each level up offers.</summary>
	[ExportGroup("Offers")]
	[Export] public int OptionsPerOffer;

	/// <summary>Rerolls the player starts the fight with. They are not refreshed.</summary>
	[Export] public int FreeRerolls;

	/// <summary>Once a Drone Weapon has been chosen at a level up, no Drone Weapon is offered again for the rest of
	/// the fight. The Drone Weapon the fight starts with doesn't count, even one picked with the starting weapon
	/// cheat. Off, the drones can switch as often as Drone Weapons are offered.</summary>
	[Export] public bool OneDroneWeaponChoicePerFight;

	/// <summary>A VNP Weapon can have only one of its features, such as Orbital Shards blocking or harvesting:
	/// once one is taken, the weapon's other features are no longer offered for the rest of the fight. Off, a
	/// weapon can take every feature it has.</summary>
	[Export] public bool OneFeaturePerWeapon;

	/// <summary>Every upgrade that can be offered. Each weapon's upgrades are in its own folder under
	/// res://weapons/ or res://drone_weapons/; the rest are in res://tuning/upgrades/, by what
	/// they affect. Take one out of this
	/// list to stop it being offered without deleting it.</summary>
	[ExportGroup("Upgrade pool")]
	[Export] public Godot.Collections.Array<UpgradeDefinition> Upgrades = new();
}
