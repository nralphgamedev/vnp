using Godot;

/// <summary>
/// A VNP Weapon in one of the VNP's weapon slots. It starts with the numbers in its definition and keeps
/// its own copy, so upgrades change this weapon for the rest of the fight without touching the file.
/// </summary>
public abstract partial class VnpWeapon : Node3D
{
	public Fight Fight { get; private set; }

	/// <summary>The definition this weapon was made from, in its folder in res://tuning/weapons/.</summary>
	public WeaponDefinition Definition { get; private set; }

	public float Damage;
	public float Cooldown;

	/// <summary>Area of effect, as a multiple of the definition's sizes. Starts at 1.</summary>
	public float Area = 1f;

	/// <summary>How fast the weapon's shapes move, as a multiple of the definition's speed. Starts at 1.</summary>
	public float Speed = 1f;

	/// <summary>
	/// Makes the weapon a definition describes. The kind of definition decides which behaviour it gets.
	/// Returns null, with an error, for a kind of definition no weapon is written for.
	/// </summary>
	public static VnpWeapon Create(Fight fight, WeaponDefinition definition)
	{
		VnpWeapon weapon = definition switch
		{
			PulseLaserDefinition => new PulseLaser(),
			OrbitalShardsDefinition => new OrbitalShards(),
			_ => null,
		};
		if (weapon == null)
		{
			GD.PushError($"No weapon behaviour for {definition?.GetType().Name ?? "a missing definition"} ({definition?.ResourcePath}).");
			return null;
		}

		weapon.Fight = fight;
		weapon.Definition = definition;
		weapon.Name = definition.Id;
		weapon.Damage = definition.Damage;
		weapon.Cooldown = definition.Cooldown;
		return weapon;
	}

	/// <summary>Multiplies one of this weapon's stats, for an upgrade.</summary>
	public void ScaleStat(WeaponStat stat, float factor)
	{
		switch (stat)
		{
			case WeaponStat.Damage: Damage *= factor; break;
			case WeaponStat.Cooldown: Cooldown *= factor; break;
			case WeaponStat.Area: Area *= factor; break;
			case WeaponStat.Speed: Speed *= factor; break;
		}
	}
}
