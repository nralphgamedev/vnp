using Godot;

/// <summary>
/// The asteroid mimic, a Grey Goo type: asleep, it is an asteroid in an asteroid field, indistinguishable from the
/// rest; some fields hold a few, most none. It wakes when the VNP comes within its WakeRange, or when the VNP or
/// a drone tries to harvest it, which also hurts it a little and gives nothing. Awake, it notices targets by its aggro ranges, as
/// every enemy does. Awake, it drains and absorbs drones like the basic
/// goo, never splits, and fires single shots at the nearest of the VNP or a drone. Everything else, touch damage,
/// growth per drone, debris and the wobbling look, is shared by the Grey Goo types (GreyGooDefinition).
/// AsteroidMimic.cs runs it awake; Harvestables.cs keeps it asleep and wakes it. It never spawns in groups or
/// waves: its weights in enemy_spawning.tres stay 0, and it is placed in fields instead, so its GroupSize is unused.
/// </summary>
[GlobalClass]
public partial class AsteroidMimicDefinition : GreyGooDefinition
{
	/// <summary>The chance an asteroid field, cluster or ring, holds mimics, and how many it holds if so.</summary>
	[ExportGroup("Hiding")]
	[Export] public float FieldChance;
	[Export] public int PerFieldMin;
	[Export] public int PerFieldMax;

	/// <summary>Asleep, it wakes when the VNP comes within this distance of it; drones coming near don't wake it.
	/// Once awake, its aggro ranges take over.</summary>
	[Export] public float WakeRange;

	/// <summary>Share of its health a harvesting attempt costs it as it wakes, from 0 to 1.</summary>
	[Export] public float WakeDamageFraction;

	/// <summary>It fires at the nearest of the VNP or a drone within this distance of its edge, once every
	/// cooldown, varied a little.</summary>
	[ExportGroup("Shot")]
	[Export] public float FireRange;
	[Export] public float FireCooldown;

	/// <summary>Its shot's damage at the start of the fight (it grows with the Grey Goo damage ramp in
	/// difficulty.tres), speed, how long it flies, colour and size (0 for the fight's usual enemy shot size).</summary>
	[Export] public float ShotDamage;
	[Export] public float ShotSpeed;
	[Export] public float ShotLifetime;
	[Export] public Color ShotColor = new(1f, 0.55f, 0.15f);
	[Export] public float ShotSize;

	/// <summary>How far ahead of a moving target it aims, from 0 (where it is) to 1 (where it will be).</summary>
	[Export] public float AimLead;
}
