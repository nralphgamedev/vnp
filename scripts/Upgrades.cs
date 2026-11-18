using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>One fight upgrade: its name, what its card says, when it can be offered, and what it does.</summary>
public sealed class Upgrade
{
	public string Name;

	/// <summary>Marks the card as a new weapon rather than an improvement.</summary>
	public bool IsWeapon;

	/// <summary>The card text. A function, so the card always shows the current Tuning numbers.</summary>
	public Func<string> Describe;

	/// <summary>Most times it can be taken in one fight. A function, so it follows the Tuning value.
	/// Leave unset for upgrades that switch something on once, like a new weapon.</summary>
	public Func<int> Limit;

	/// <summary>Whether the upgrade can be offered right now, apart from its limit. Leave unset for "always".</summary>
	public Func<Fight, bool> CanOffer = _ => true;

	public Action<Fight> Apply;
}

/// <summary>
/// The fight upgrade pool. To add, remove or change an upgrade, edit this list.
/// The amounts are in Tuning, under "Levels and fight upgrades".
/// </summary>
public static class Upgrades
{
	public static readonly Upgrade[] Pool =
	{
		new()
		{
			Name = "Orbital Shards",
			IsWeapon = true,
			Describe = () => $"New VNP Weapon. {Tuning.ShardCount} shards circle the VNP and damage enemies they touch.",
			CanOffer = f => f.OrbitalShards == null && f.WeaponCount < Tuning.WeaponSlots,
			Apply = f => f.EquipOrbitalShards(),
		},
		new()
		{
			Name = "Pulse Laser damage",
			Limit = () => Tuning.UpgradeLimitPulseLaserDamage,
			Describe = () => $"Pulse Laser damage +{Percent(Tuning.UpgradePulseLaserDamage)}.",
			Apply = f => f.PulseLaser.Damage *= 1f + Tuning.UpgradePulseLaserDamage,
		},
		new()
		{
			Name = "Pulse Laser cooldown",
			Limit = () => Tuning.UpgradeLimitPulseLaserCooldown,
			Describe = () => $"Pulse Laser cooldown -{Percent(Tuning.UpgradePulseLaserCooldown)}, so it fires more often.",
			Apply = f => f.PulseLaser.Cooldown *= 1f - Tuning.UpgradePulseLaserCooldown,
		},
		new()
		{
			Name = "Orbital Shards damage",
			Limit = () => Tuning.UpgradeLimitShardDamage,
			Describe = () => $"Orbital Shards damage +{Percent(Tuning.UpgradeShardDamage)}.",
			CanOffer = f => f.OrbitalShards != null,
			Apply = f => f.OrbitalShards.Damage *= 1f + Tuning.UpgradeShardDamage,
		},
		new()
		{
			Name = "Orbital Shards area of effect",
			Limit = () => Tuning.UpgradeLimitShardArea,
			Describe = () => $"Orbital Shards circle wider and grow +{Percent(Tuning.UpgradeShardArea)}.",
			CanOffer = f => f.OrbitalShards != null,
			Apply = f => f.OrbitalShards.Area *= 1f + Tuning.UpgradeShardArea,
		},
		new()
		{
			Name = "Orbital Shards speed",
			Limit = () => Tuning.UpgradeLimitShardSpeed,
			Describe = () => $"Orbital Shards circle {Percent(Tuning.UpgradeShardSpeed)} faster, so they hit more often.",
			CanOffer = f => f.OrbitalShards != null,
			Apply = f => f.OrbitalShards.Speed *= 1f + Tuning.UpgradeShardSpeed,
		},
		new()
		{
			Name = "Beam",
			IsWeapon = true,
			Describe = () => "New Drone Weapon. Every drone swaps its Projectile for a short-range beam that never misses.",
			CanOffer = f => !f.Drones.UsesBeam,
			Apply = f => f.Drones.SwitchToBeam(),
		},
		new()
		{
			Name = "Health",
			Limit = () => Tuning.UpgradeLimitHealth,
			Describe = () => $"VNP health +{Tuning.UpgradeHealth:0}, and heals {Tuning.UpgradeHealth:0}.",
			Apply = f => f.Vnp.RaiseMaxHealth(Tuning.UpgradeHealth),
		},
		new()
		{
			Name = "Movement speed",
			Limit = () => Tuning.UpgradeLimitMoveSpeed,
			Describe = () => $"VNP movement speed +{Percent(Tuning.UpgradeMoveSpeed)}. Drones speed up to match.",
			Apply = f => f.Vnp.MoveSpeed *= 1f + Tuning.UpgradeMoveSpeed,
		},
		new()
		{
			Name = "Harvest rate",
			Limit = () => Tuning.UpgradeLimitHarvestRate,
			Describe = () => $"All harvest beams work {Percent(Tuning.UpgradeHarvestRate)} faster, so fight experience comes in faster.",
			Apply = f =>
			{
				f.VnpHarvester.Rate *= 1f + Tuning.UpgradeHarvestRate;
				f.Drones.HarvestRate *= 1f + Tuning.UpgradeHarvestRate;
			},
		},
		new()
		{
			Name = "Drone cap",
			Limit = () => Tuning.UpgradeLimitDroneCap,
			Describe = () => $"Drone cap +{Tuning.UpgradeDroneCap} for the rest of the fight.",
			Apply = f => f.Drones.Cap += Tuning.UpgradeDroneCap,
		},
		new()
		{
			Name = "New drones",
			Describe = () => $"Build {Tuning.UpgradeNewDrones} drones right away, up to the cap.",
			// Drones already being built count, so it is not offered when they would fill the cap anyway.
			CanOffer = f => f.Drones.CountWithBuilds < f.Drones.Cap,
			Apply = f => f.BuildDronesNow(Tuning.UpgradeNewDrones),
		},
	};

	/// <summary>
	/// Picks up to <paramref name="count"/> different upgrades that can be offered right now, at random.
	/// Upgrades in <paramref name="avoid"/> are used only if there are not enough others.
	/// </summary>
	public static List<Upgrade> Draw(Fight fight, int count, ICollection<Upgrade> avoid = null)
	{
		var fresh = new List<Upgrade>();
		var avoided = new List<Upgrade>();
		foreach (Upgrade upgrade in Pool)
		{
			if (!upgrade.CanOffer(fight))
				continue;
			if (upgrade.Limit != null && TimesTaken(fight, upgrade) >= upgrade.Limit())
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

	/// <summary>How many times this upgrade has been taken this fight.</summary>
	public static int TimesTaken(Fight fight, Upgrade upgrade) => fight.TakenUpgrades.Count(name => name == upgrade.Name);

	private static void Shuffle(List<Upgrade> list)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = Rng.Int(0, i);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}

	private static string Percent(float fraction) => $"{fraction * 100f:0}%";
}
