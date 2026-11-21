using Godot;

/// <summary>
/// What a pilot tries to pick from level-up offers. One file per build in res://pilots/builds/. Pilot.cs scores
/// each offered upgrade by these weights, adds some randomness, and takes the highest, or rerolls when nothing
/// scores well enough.
///
/// An upgrade's score starts from its own weight in UpgradeWeights if it has one, and otherwise from its
/// category's weight. Then FavourOwned is added if it improves something the VNP already has, and
/// NewWeaponWeight if it gives a new weapon.
/// </summary>
[GlobalClass]
public partial class BuildDefinition : Resource
{
	/// <summary>Stays the same if the name changes. Written in fight logs.</summary>
	[Export] public string Id;

	[Export] public string Name;

	/// <summary>Weights for particular upgrades, by upgrade ID, in place of their category's weight. Empty for a
	/// build that only cares about categories.</summary>
	[Export] public Godot.Collections.Dictionary<string, float> UpgradeWeights = new();

	[ExportGroup("Category weights")]
	[Export] public float VnpWeaponWeight = 1f;
	[Export] public float DroneWeaponWeight = 1f;
	[Export] public float StatsWeight = 1f;
	[Export] public float DronesWeight = 1f;

	/// <summary>Added for an upgrade to something the VNP already has: an equipped weapon, or the drones' weapon.
	/// Higher keeps investing in the same things; 0 doesn't care.</summary>
	[ExportGroup("Preferences")]
	[Export] public float FavourOwned;

	/// <summary>Added for an upgrade that gives a new VNP Weapon or Drone Weapon.</summary>
	[Export] public float NewWeaponWeight;

	/// <summary>Randomness added to every score. Large enough, and the build picks at random.</summary>
	[Export] public float Randomness;

	/// <summary>If the best offered upgrade scores below this and a reroll is left, it rerolls, once per offer.
	/// 0 never rerolls.</summary>
	[Export] public float RerollBelow;
}
