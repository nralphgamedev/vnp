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
			UpgradeEffect.ScaleWeaponStat or UpgradeEffect.AddWeaponStat => fight.FindWeapon(upgrade.Weapon) != null,
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
			case UpgradeEffect.AddWeaponStat:
				fight.FindWeapon(upgrade.Weapon)?.AddStat(upgrade.WeaponStat, upgrade.Amount);
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
				fight.DronesBuiltByUpgrades += fight.BuildDronesNow(DronesToBuild(fight, upgrade));
				break;
		}
	}

	/// <summary>True for upgrades that give a new weapon rather than improve something.</summary>
	public static bool IsNewWeapon(UpgradeDefinition upgrade) =>
		upgrade.Effect is UpgradeEffect.EquipWeapon or UpgradeEffect.SwitchDroneWeapon;

	/// <summary>
	/// How many drones a BuildDrones upgrade would build right now: its amount, or fewer if that would go
	/// over the cap. Drones already being built count toward the cap.
	/// </summary>
	public static int DronesToBuild(Fight fight, UpgradeDefinition upgrade)
	{
		int room = fight.Drones.Cap - fight.Drones.CountWithBuilds;
		return Mathf.Clamp(Mathf.RoundToInt(upgrade.Amount), 0, Mathf.Max(0, room));
	}

	/// <summary>The card text, with the numbers filled in for this moment in the fight.</summary>
	public static string Describe(UpgradeDefinition upgrade, Fight fight)
	{
		string text = (upgrade.Description ?? "")
			.Replace("{percent}", $"{Mathf.Abs(upgrade.Amount) * 100f:0}%")
			.Replace("{amount}", $"{upgrade.Amount:0.##}");
		if (upgrade.Effect == UpgradeEffect.BuildDrones)
		{
			int drones = DronesToBuild(fight, upgrade);
			text = text.Replace("{drones}", drones == 1 ? "1 drone" : $"{drones} drones");
		}
		int? count = upgrade.Weapon switch
		{
			OrbitalShardsDefinition shards => shards.Count,
			ScatterShotDefinition scatter => scatter.ProjectileCount,
			SeekerMissilesDefinition seekers => seekers.MissilesPerVolley,
			_ => null,
		};
		if (count != null)
			text = text.Replace("{count}", count.Value.ToString());
		return text;
	}

	/// <summary>
	/// The total change from taking an upgrade <paramref name="times"/> times, as shown in the upgrade list.
	/// Percentage upgrades multiply, so two +25% upgrades come to +56% and two -15% come to -28%; they
	/// end in %. Flat upgrades add up, and are shown as plain numbers, such as +50. Upgrades that equip or
	/// switch a weapon have no number, and give null.
	/// </summary>
	public static string TotalBonus(UpgradeDefinition upgrade, int times, Fight fight)
	{
		switch (upgrade.Effect)
		{
			case UpgradeEffect.BuildDrones:
				// The drones actually built, which can be fewer than offered near the cap.
				return $"+{fight.DronesBuiltByUpgrades}";
			case UpgradeEffect.ScaleWeaponStat:
			case UpgradeEffect.ScaleStat:
				float percent = (Mathf.Pow(1f + upgrade.Amount, times) - 1f) * 100f;
				return $"{(percent >= 0f ? "+" : "-")}{Mathf.Abs(percent):0}%";
			case UpgradeEffect.AddStat:
			case UpgradeEffect.AddWeaponStat:
				float total = upgrade.Amount * times;
				return $"{(total >= 0f ? "+" : "-")}{Mathf.Abs(total):0.##}";
			default:
				return null;
		}
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
