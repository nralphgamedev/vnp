using Godot;

/// <summary>
/// A VNP Weapon in one of the VNP's weapon slots. It starts with the numbers in its definition and keeps
/// its own copy, so upgrades change this weapon for the rest of the fight without touching the file.
/// </summary>
public abstract partial class VnpWeapon : Node3D
{
	public Fight Fight { get; private set; }

	/// <summary>The definition this weapon was made from, in its folder in res://weapons/.</summary>
	public WeaponDefinition Definition { get; private set; }

	/// <summary>This weapon, as the source of its hits, for fight statistics.</summary>
	public DamageSource Source { get; private set; }

	public float Damage;
	public float Cooldown;

	/// <summary>Area of effect, as a multiple of the definition's sizes. Starts at 1.</summary>
	public float Area = 1f;

	/// <summary>How fast the weapon's shapes move, as a multiple of the definition's speed. Starts at 1.</summary>
	public float Speed = 1f;

	/// <summary>Range, as a multiple of the definition's range. Starts at 1.</summary>
	public float Range = 1f;

	/// <summary>Added to the definition's count: extra projectiles, missiles or shards from upgrades.</summary>
	public int ExtraCount;

	/// <summary>How far from the VNP this weapon reaches enemies right now, with its upgrades, in units; 0 for a
	/// weapon with no single reach, such as Mine Layer. Read-only, for pilots, which keep a distance from enemies
	/// as a fraction of it, as the designer does.</summary>
	public virtual float Reach => 0f;

	/// <summary>
	/// Declares where this weapon works right now, for pilots (Arbiter, docs/dev/PILOT_SPEC.md) and the run review's
	/// maps: its reach around the VNP (DeclareReach), or traps it has left, such as mines. <paramref name="source"/>
	/// is the caller's ID for this weapon, carried on each zone. Required, so a pilot always knows what a new weapon
	/// does. Game code, since rival and escort VNPs will need it. Read-only.
	/// </summary>
	public abstract void DeclareZones(Arbiter.Situation situation, int source);

	/// <summary>Declares a circle of <see cref="Reach"/> around the VNP, for a weapon that hits what comes within it.</summary>
	protected void DeclareReach(Arbiter.Situation situation, int source)
	{
		if (Reach > 0f)
			situation.Add(Arbiter.Zone.ReachAround(Reach, source));
	}

#if RECORDING
	/// <summary>
	/// Adds what this weapon has out in the world right now, such as its shards, mines, missiles, rings or beams.
	/// Read-only, for whatever needs to see them, such as the run review's recordings and maps. Every weapon must
	/// say: one with nothing of its own out there writes an empty body with a comment saying why, such as shots
	/// that go through the fight's projectiles, which are recorded there. Required so a new weapon can't be left
	/// off the maps by accident. Only in builds with RECORDING (the editor's), like the recorder itself: every
	/// weapon's version goes inside #if RECORDING too.
	/// </summary>
	public abstract void CollectPlaced(System.Collections.Generic.List<PlacedObject> placed);

	/// <summary>The features (mods) upgrades have switched on for this weapon, such as Orbital Shards harvesting.</summary>
	public System.Collections.Generic.IEnumerable<WeaponFeature> Features => _features;
#endif

	private readonly System.Collections.Generic.HashSet<WeaponFeature> _features = new();

	/// <summary>Whether an upgrade has switched on this behaviour for this weapon.</summary>
	public bool HasFeature(WeaponFeature feature) => _features.Contains(feature);

	/// <summary>Whether an upgrade has switched on any of this weapon's features.</summary>
	public bool HasAnyFeature => _features.Count > 0;

	/// <summary>Switches on a behaviour, for an upgrade.</summary>
	public void Unlock(WeaponFeature feature) => _features.Add(feature);

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
			ScatterShotDefinition => new ScatterShot(),
			SeekerMissilesDefinition => new SeekerMissiles(),
			ShockwaveDefinition => new Shockwave(),
			MineLayerDefinition => new MineLayer(),
			_ => null,
		};
		if (weapon == null)
		{
			GD.PushError($"No weapon behaviour for {definition?.GetType().Name ?? "a missing definition"} ({definition?.ResourcePath}).");
			return null;
		}

		weapon.Fight = fight;
		weapon.Definition = definition;
		weapon.Source = new DamageSource(definition.Id, definition.Name, DamageSourceKind.VnpWeapon, definition.Color);
		weapon.Name = definition.Id;
		weapon.Damage = definition.Damage;
		weapon.Cooldown = definition.Cooldown;
		return weapon;
	}

	/// <summary>Adds to one of this weapon's stats, for an upgrade.</summary>
	public void AddStat(WeaponStat stat, float amount)
	{
		switch (stat)
		{
			case WeaponStat.Damage: Damage += amount; break;
			case WeaponStat.Cooldown: Cooldown = Mathf.Max(0.01f, Cooldown + amount); break;
			case WeaponStat.Area: Area += amount; break;
			case WeaponStat.Speed: Speed += amount; break;
			case WeaponStat.Count: ExtraCount += Mathf.RoundToInt(amount); break;
			case WeaponStat.Range: Range += amount; break;
		}
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
			case WeaponStat.Range: Range *= factor; break;
		}
	}
}
