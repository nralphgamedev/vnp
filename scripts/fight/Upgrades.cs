using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Carries out fight upgrades. The upgrades themselves are definitions in res://tuning/: each weapon's in
/// its own folder, the rest in res://tuning/upgrades/. The pool that offers draw from is a list in
/// res://tuning/levels.tres. This class reads a definition's effect and does it: a new upgrade that uses
/// an existing effect needs no code, a new kind of effect does.
/// </summary>
public static class Upgrades
{
	/// <summary>Every upgrade that can be offered, in the order listed in levels.tres.</summary>
	public static IEnumerable<UpgradeDefinition> Pool => Tuning.Levels.Upgrades.Where(u => u != null);

	/// <summary>
	/// Picks up to <paramref name="count"/> different upgrades that can be offered right now, at random.
	/// Upgrades in <paramref name="avoid"/> are used only if there are not enough others.
	/// </summary>
	public static List<UpgradeDefinition> Draw(Fight fight, int count, ICollection<UpgradeDefinition> avoid = null)
	{
		var fresh = new List<UpgradeDefinition>();
		var avoided = new List<UpgradeDefinition>();
		foreach (UpgradeDefinition upgrade in Pool)
		{
			if (!CanOffer(fight, upgrade))
				continue;
			if (avoid != null && avoid.Contains(upgrade))
				avoided.Add(upgrade);
			else
				fresh.Add(upgrade);
		}

		Shuffle(fresh);
		Shuffle(avoided);
		return fresh.Concat(avoided).Take(count).ToList();
	}

	/// <summary>Whether an upgrade can be offered now: its effect makes sense and its limit is not reached.</summary>
	public static bool CanOffer(Fight fight, UpgradeDefinition upgrade)
	{
		if (upgrade.Limit > 0 && TimesTaken(fight, upgrade) >= upgrade.Limit)
			return false;

		return upgrade.Effect switch
		{
			UpgradeEffect.EquipWeapon => upgrade.Weapon != null
				&& fight.FindWeapon(upgrade.Weapon) == null && fight.WeaponCount < Tuning.Vnp.WeaponSlots,
			UpgradeEffect.ScaleWeaponStat => fight.FindWeapon(upgrade.Weapon) != null,
			UpgradeEffect.SwitchDroneWeapon => upgrade.DroneWeapon != null && fight.Drones.Weapon.Id != upgrade.DroneWeapon.Id,
			// Drones already being built count, so it is not offered when they would fill the cap anyway.
			UpgradeEffect.BuildDrones => fight.Drones.CountWithBuilds < fight.Drones.Cap,
			_ => true,
		};
	}

	public static void Apply(Fight fight, UpgradeDefinition upgrade)
	{
		switch (upgrade.Effect)
		{
			case UpgradeEffect.EquipWeapon:
				fight.Equip(upgrade.Weapon);
				break;
			case UpgradeEffect.ScaleWeaponStat:
				fight.FindWeapon(upgrade.Weapon)?.ScaleStat(upgrade.WeaponStat, 1f + upgrade.Amount);
				break;
			case UpgradeEffect.SwitchDroneWeapon:
				fight.Drones.SwitchWeapon(upgrade.DroneWeapon);
				break;
			case UpgradeEffect.ScaleStat:
				ScaleStat(fight, upgrade.Stat, 1f + upgrade.Amount);
				break;
			case UpgradeEffect.AddStat:
				AddStat(fight, upgrade.Stat, upgrade.Amount);
				break;
			case UpgradeEffect.BuildDrones:
				fight.BuildDronesNow(Mathf.RoundToInt(upgrade.Amount));
				break;
		}
	}

	/// <summary>True for upgrades that give a new weapon rather than improve something.</summary>
	public static bool IsNewWeapon(UpgradeDefinition upgrade) =>
		upgrade.Effect is UpgradeEffect.EquipWeapon or UpgradeEffect.SwitchDroneWeapon;

	/// <summary>The card text, with the numbers filled in.</summary>
	public static string Describe(UpgradeDefinition upgrade)
	{
		string text = (upgrade.Description ?? "")
			.Replace("{percent}", $"{Mathf.Abs(upgrade.Amount) * 100f:0}%")
			.Replace("{amount}", $"{upgrade.Amount:0.##}");
		if (upgrade.Weapon is OrbitalShardsDefinition shards)
			text = text.Replace("{count}", shards.Count.ToString());
		return text;
	}

	/// <summary>How many times this upgrade has been taken this fight.</summary>
	public static int TimesTaken(Fight fight, UpgradeDefinition upgrade) =>
		fight.TakenUpgrades.Count(taken => taken.Id == upgrade.Id);

	private static void ScaleStat(Fight fight, PlayerStat stat, float factor)
	{
		switch (stat)
		{
			case PlayerStat.MaxHealth:
				fight.Vnp.RaiseMaxHealth(fight.Vnp.MaxHealth * (factor - 1f));
				break;
			case PlayerStat.MoveSpeed:
				fight.Vnp.MoveSpeed *= factor;
				break;
			case PlayerStat.HarvestRate:
				fight.VnpHarvester.Rate *= factor;
				fight.Drones.HarvestRate *= factor;
				break;
			case PlayerStat.DroneCap:
				fight.Drones.Cap = Mathf.RoundToInt(fight.Drones.Cap * factor);
				break;
		}
	}

	private static void AddStat(Fight fight, PlayerStat stat, float amount)
	{
		switch (stat)
		{
			case PlayerStat.MaxHealth:
				fight.Vnp.RaiseMaxHealth(amount);
				break;
			case PlayerStat.MoveSpeed:
				fight.Vnp.MoveSpeed += amount;
				break;
			case PlayerStat.HarvestRate:
				// Added to every beam: the VNP's and each drone's.
				fight.VnpHarvester.Rate += amount;
				fight.Drones.HarvestRate += amount;
				break;
			case PlayerStat.DroneCap:
				fight.Drones.Cap += Mathf.RoundToInt(amount);
				break;
		}
	}

	private static void Shuffle(List<UpgradeDefinition> list)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = Rng.Int(0, i);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}
}
