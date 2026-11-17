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

	/// <summary>Whether the upgrade can be offered right now. Leave unset for "always".</summary>
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
			Describe = () => $"Pulse Laser damage +{Percent(Tuning.UpgradePulseLaserDamage)}.",
			Apply = f => f.PulseLaser.Damage *= 1f + Tuning.UpgradePulseLaserDamage,
		},
		new()
		{
			Name = "Pulse Laser cooldown",
			Describe = () => $"Pulse Laser cooldown -{Percent(Tuning.UpgradePulseLaserCooldown)}, so it fires more often.",
			Apply = f => f.PulseLaser.Cooldown *= 1f - Tuning.UpgradePulseLaserCooldown,
		},
		new()
		{
			Name = "Orbital Shards damage",
			Describe = () => $"Orbital Shards damage +{Percent(Tuning.UpgradeShardDamage)}.",
			CanOffer = f => f.OrbitalShards != null,
			Apply = f => f.OrbitalShards.Damage *= 1f + Tuning.UpgradeShardDamage,
		},
		new()
		{
			Name = "Orbital Shards area of effect",
			Describe = () => $"Orbital Shards circle wider and grow +{Percent(Tuning.UpgradeShardArea)}.",
			CanOffer = f => f.OrbitalShards != null,
			Apply = f => f.OrbitalShards.Area *= 1f + Tuning.UpgradeShardArea,
		},
		new()
		{
			Name = "Orbital Shards speed",
			Describe = () => $"Orbital Shards circle {Percent(Tuning.UpgradeShardSpeed)} faster, so they hit more often.",
			CanOffer = f => f.OrbitalShards != null,
			Apply = f => f.OrbitalShards.Speed *= 1f + Tuning.UpgradeShardSpeed,
		},
		new()
		{
			Name = "Beam",
			IsWeapon = true,
			Describe = () => "New Basic Probe Weapon. Every probe swaps its Projectile for a short-range beam that never misses.",
			CanOffer = f => !f.Probes.UsesBeam,
			Apply = f => f.Probes.SwitchToBeam(),
		},
		new()
		{
			Name = "Health",
			Describe = () => $"VNP health +{Tuning.UpgradeHealth:0}, and heals {Tuning.UpgradeHealth:0}.",
			Apply = f => f.Vnp.RaiseMaxHealth(Tuning.UpgradeHealth),
		},
		new()
		{
			Name = "Movement speed",
			Describe = () => $"VNP movement speed +{Percent(Tuning.UpgradeMoveSpeed)}. Probes speed up to match.",
			Apply = f => f.Vnp.MoveSpeed *= 1f + Tuning.UpgradeMoveSpeed,
		},
		new()
		{
			Name = "Harvest rate",
			Describe = () => $"All harvest beams work {Percent(Tuning.UpgradeHarvestRate)} faster, so fight experience comes in faster.",
			Apply = f =>
			{
				f.VnpHarvester.Rate *= 1f + Tuning.UpgradeHarvestRate;
				f.Probes.HarvestRate *= 1f + Tuning.UpgradeHarvestRate;
			},
		},
		new()
		{
			Name = "Harvest range",
			Describe = () => $"All harvest beams reach {Percent(Tuning.UpgradeHarvestRange)} farther.",
			Apply = f => f.Harvestables.BeamRange *= 1f + Tuning.UpgradeHarvestRange,
		},
		new()
		{
			Name = "Basic probe cap",
			Describe = () => $"Probe cap +{Tuning.UpgradeProbeCap} for the rest of the fight.",
			Apply = f => f.Probes.Cap += Tuning.UpgradeProbeCap,
		},
		new()
		{
			Name = "New basic probes",
			Describe = () => $"Build {Tuning.UpgradeNewProbes} probes right away, up to the cap.",
			Apply = f => f.BuildProbesNow(Tuning.UpgradeNewProbes),
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
			if (avoid != null && avoid.Contains(upgrade))
				avoided.Add(upgrade);
			else
				fresh.Add(upgrade);
		}

		Shuffle(fresh);
		Shuffle(avoided);
		return fresh.Concat(avoided).Take(count).ToList();
	}

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
