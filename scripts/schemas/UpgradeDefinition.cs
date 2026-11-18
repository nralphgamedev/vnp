using Godot;

/// <summary>What an upgrade does. Each effect is a behaviour written in code; the definition says what it applies to.</summary>
public enum UpgradeEffect
{
	/// <summary>Puts the definition's Weapon in a free weapon slot.
	/// Offered only while that weapon is not equipped and a slot is free.</summary>
	EquipWeapon,

	/// <summary>Multiplies one stat of an equipped weapon by 1 + Amount. A negative amount lowers it,
	/// so -0.15 on cooldown fires more often. Offered only while the weapon is equipped.</summary>
	ScaleWeaponStat,

	/// <summary>Switches every drone to the definition's DroneWeapon.
	/// Offered only while the drones use a different one.</summary>
	SwitchDroneWeapon,

	/// <summary>Multiplies a player stat by 1 + Amount.</summary>
	ScaleStat,

	/// <summary>Adds Amount to a player stat.</summary>
	AddStat,

	/// <summary>Builds Amount drones at once, up to the cap. Offered only while below the cap.</summary>
	BuildDrones,
}

/// <summary>
/// What an upgrade is about. Its offer card takes the category's colour from look.tres, so an offer reads
/// at a glance. Upgrades to a weapon go with that weapon's kind. The full rule and the current upgrades
/// in each category are under "Offer card colours" in docs/PROTOTYPE_2_SPEC.md.
/// </summary>
public enum UpgradeCategory
{
	/// <summary>New VNP Weapons and upgrades to a VNP Weapon. Orange cards.</summary>
	VnpWeapon,

	/// <summary>New Drone Weapons and upgrades to how drones attack. Blue cards.</summary>
	DroneWeapon,

	/// <summary>The VNP's own stats, and harvesting. Green cards.</summary>
	Stats,

	/// <summary>How many drones there are: the drone cap and new drones. Pink cards.</summary>
	Drones,
}

/// <summary>A stat that belongs to a VNP Weapon.</summary>
public enum WeaponStat
{
	Damage,
	Cooldown,

	/// <summary>Area of effect. For Orbital Shards, both the orbit and the size of each shard.</summary>
	Area,

	/// <summary>How fast the weapon's shapes move. For Orbital Shards, how fast they circle.</summary>
	Speed,
}

/// <summary>A stat of the VNP and its drones that upgrades can change.</summary>
public enum PlayerStat
{
	/// <summary>The VNP's maximum health. Adding to it also heals by the same amount.</summary>
	MaxHealth,

	/// <summary>The VNP's top speed. Drones speed up by the same fraction, so they keep up.</summary>
	MoveSpeed,

	/// <summary>Every harvest beam, the VNP's and the drones'.</summary>
	HarvestRate,

	/// <summary>Most drones the VNP can have.</summary>
	DroneCap,
}

/// <summary>
/// One fight upgrade. To add one that reuses an existing effect, make a new .tres file in
/// the upgrades/ folder of the weapon it belongs to, or in res://tuning/upgrades/ by what it affects,
/// and add it to the upgrade pool in res://tuning/levels.tres.
/// </summary>
[GlobalClass]
public partial class UpgradeDefinition : Resource
{
	/// <summary>Stays the same if the name changes. Used to count how many times it was taken.</summary>
	[Export] public string Id;

	/// <summary>Shown on the card and in the pause menu.</summary>
	[Export] public string Name;

	/// <summary>The card text. {percent} is replaced by the amount as a percentage, such as 25%,
	/// and {amount} by the amount as a number. For an Orbital Shards weapon, {count} is its shard count.</summary>
	[Export(PropertyHint.MultilineText)] public string Description;

	/// <summary>Decides the colour of its offer card and the category named on it.</summary>
	[Export] public UpgradeCategory Category;

	[Export] public UpgradeEffect Effect;

	/// <summary>The weapon it equips or changes. For EquipWeapon and ScaleWeaponStat.</summary>
	[Export] public WeaponDefinition Weapon;

	/// <summary>The weapon stat it changes. For ScaleWeaponStat.</summary>
	[Export] public WeaponStat WeaponStat;

	/// <summary>The Drone Weapon it switches to. For SwitchDroneWeapon.</summary>
	[Export] public DroneWeaponDefinition DroneWeapon;

	/// <summary>The player stat it changes. For ScaleStat and AddStat.</summary>
	[Export] public PlayerStat Stat;

	/// <summary>How much. A fraction for the Scale effects (0.25 is +25%), a number for the others.</summary>
	[Export] public float Amount;

	/// <summary>Most times it can be taken in one fight. 0 means no limit. Upgrades that equip or
	/// switch a weapon can only be taken once anyway.</summary>
	[Export] public int Limit;
}
