using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>What dealt or took damage: a VNP Weapon, a Drone Weapon, or an enemy type.</summary>
public enum DamageSourceKind
{
	VnpWeapon,
	DroneWeapon,
	Enemy,
	Other,
}

/// <summary>
/// Where a hit came from, carried with it so fight statistics can say what did the damage. Weapons, Drone
/// Weapons and enemy types each make one from their definition, and pass it with every hit, including
/// shots in flight.
/// </summary>
public sealed class DamageSource
{
	public readonly string Id;
	public readonly string Name;
	public readonly DamageSourceKind Kind;
	public readonly Color Color;

	public DamageSource(string id, string name, DamageSourceKind kind, Color color)
	{
		Id = id;
		Name = name;
		Kind = kind;
		Color = color;
	}

	/// <summary>For damage with no known source.</summary>
	public static readonly DamageSource Unknown = new("other", "Other", DamageSourceKind.Other, Colors.White);
}

/// <summary>
/// What happened in one fight: damage by source and enemy type, drones built and lost, shots blocked, kills,
/// level-ups and the offers behind them, damage the VNP took, and fight experience by where it came from.
/// The fight keeps one of these; the result screen shows part of it, and in the editor the fight log saves all
/// of it. It only counts, so it changes nothing about how a fight plays.
///
/// Enemy types and sources are keyed by their IDs, so the numbers can be saved and compared between fights.
/// </summary>
public sealed class FightStats
{
	/// <summary>One source's damage to enemies.</summary>
	public sealed class SourceDamage
	{
		public DamageSource Source;
		public float Total;
		/// <summary>Damage to each enemy type, by its ID.</summary>
		public readonly Dictionary<string, float> ByEnemy = new();
	}

	/// <summary>One level-up: what was offered, each reroll's new offer, and what was taken.</summary>
	public sealed class LevelUp
	{
		public double Time;
		public int Level;
		/// <summary>Each offer shown, first the original and then one per reroll, as upgrade IDs.</summary>
		public readonly List<List<string>> Offers = new();
		public int Rerolls;
		/// <summary>Rerolls that cost nothing, through the editor's free reroll cheat.</summary>
		public int FreeRerolls;
		/// <summary>The upgrade taken, or null if the fight ended before one was chosen.</summary>
		public string Taken;
	}

	/// <summary>Damage to enemies by source, in the order each source first did damage.</summary>
	public readonly List<SourceDamage> Damage = new();

	/// <summary>All damage to enemies. Only damage that took health off counts, not the part past what was left.</summary>
	public float TotalDamage { get; private set; }

	public int DronesStarted;
	public int DronesBuilt;
	/// <summary>Drones destroyed (popped), by the ID of what destroyed them.</summary>
	public readonly Dictionary<string, int> DronesDestroyed = new();
	/// <summary>Drones absorbed, by the ID of the enemy type that absorbed them.</summary>
	public readonly Dictionary<string, int> DronesAbsorbed = new();

	/// <summary>Enemy shots destroyed by Orbital Shards blocking.</summary>
	public int ShotsBlocked;

	/// <summary>Every kill: the fight time and the enemy type's ID.</summary>
	public readonly List<(double Time, string Enemy)> Kills = new();

	public readonly List<LevelUp> LevelUps = new();

	/// <summary>Damage the VNP took, after armor, by the ID of what dealt it.</summary>
	public readonly Dictionary<string, float> DamageTaken = new();

	// Double precision: thousands of small additions in single precision drift apart by rounding.
	public double ExperienceFromAsteroids;
	public double ExperienceFromDebris;

	private readonly Dictionary<string, SourceDamage> _damageById = new();

	public int DronesDestroyedTotal => DronesDestroyed.Values.Sum();
	public int DronesAbsorbedTotal => DronesAbsorbed.Values.Sum();

	/// <summary>Kills of each enemy type, by its ID.</summary>
	public Dictionary<string, int> KillsByEnemy() =>
		Kills.GroupBy(k => k.Enemy).ToDictionary(g => g.Key, g => g.Count());

	/// <summary>Records damage that took health off an enemy.</summary>
	public void EnemyDamaged(DamageSource source, string enemyId, float amount)
	{
		if (amount <= 0f)
			return;
		source ??= DamageSource.Unknown;
		if (!_damageById.TryGetValue(source.Id, out SourceDamage entry))
		{
			entry = new SourceDamage { Source = source };
			_damageById[source.Id] = entry;
			Damage.Add(entry);
		}
		entry.Total += amount;
		entry.ByEnemy[enemyId] = entry.ByEnemy.GetValueOrDefault(enemyId) + amount;
		TotalDamage += amount;
	}

	public void EnemyKilled(double time, string enemyId) => Kills.Add((time, enemyId));

	public void VnpDamaged(DamageSource source, float amount)
	{
		string id = (source ?? DamageSource.Unknown).Id;
		DamageTaken[id] = DamageTaken.GetValueOrDefault(id) + amount;
	}

	public void DroneDestroyed(DamageSource source)
	{
		string id = (source ?? DamageSource.Unknown).Id;
		DronesDestroyed[id] = DronesDestroyed.GetValueOrDefault(id) + 1;
	}

	public void DroneAbsorbed(string enemyId) => DronesAbsorbed[enemyId] = DronesAbsorbed.GetValueOrDefault(enemyId) + 1;

	public void OfferShown(double time, int level, IEnumerable<UpgradeDefinition> offer)
	{
		var levelUp = new LevelUp { Time = time, Level = level };
		levelUp.Offers.Add(offer.Select(u => u.Id).ToList());
		LevelUps.Add(levelUp);
	}

	public void OfferRerolled(IEnumerable<UpgradeDefinition> offer, bool free)
	{
		if (LevelUps.Count == 0)
			return;
		LevelUp levelUp = LevelUps[^1];
		levelUp.Offers.Add(offer.Select(u => u.Id).ToList());
		levelUp.Rerolls++;
		if (free)
			levelUp.FreeRerolls++;
	}

	public void UpgradeTaken(UpgradeDefinition upgrade)
	{
		if (LevelUps.Count > 0 && LevelUps[^1].Taken == null)
			LevelUps[^1].Taken = upgrade.Id;
	}
}
