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

	/// <summary>How many upgrades each level up offers.</summary>
	[ExportGroup("Offers")]
	[Export] public int OptionsPerOffer;

	/// <summary>Rerolls the player starts the fight with. They are not refreshed.</summary>
	[Export] public int FreeRerolls;

	/// <summary>Every upgrade that can be offered. Each weapon's upgrades are in its own folder under
	/// res://weapons/ or res://drone_weapons/; the rest are in res://tuning/upgrades/, by what
	/// they affect. Take one out of this
	/// list to stop it being offered without deleting it.</summary>
	[ExportGroup("Upgrade pool")]
	[Export] public Godot.Collections.Array<UpgradeDefinition> Upgrades = new();
}
