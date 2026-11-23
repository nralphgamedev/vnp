using Godot;

/// <summary>
/// The Lancer, a Grey Goo type: it drifts after the VNP, then forms itself into a spike and launches along a line,
/// carrying on through what it hits. Everything else, touch damage, debris and the wobbling look, is shared by
/// the Grey Goo types (GreyGooDefinition). Lancer.cs runs it.
/// </summary>
[GlobalClass]
public partial class LancerDefinition : GreyGooDefinition
{
	/// <summary>It starts forming its spike once the VNP is this close and its cooldown is done. Longer launches
	/// from farther off, giving the player more warning.</summary>
	[ExportGroup("Launching")]
	[Export] public float LaunchRange;

	/// <summary>Seconds it takes to form its spike before launching: the player's warning. Its aim is set as it
	/// starts, so moving off the line dodges it. Shorter is harder to dodge.</summary>
	[Export] public float FormSeconds;

	/// <summary>How fast the spike flies, in units a second.</summary>
	[Export] public float LaunchSpeed;

	/// <summary>How far each launch carries it past the point it aimed at, through whatever it meets, so it
	/// overshoots its target by the same margin from near or far.</summary>
	[Export] public float LaunchBeyond;

	/// <summary>Damage to the VNP, and to each drone in its path, once per launch, at the start of the fight. It
	/// grows with the Grey Goo damage ramp in difficulty.tres.</summary>
	[Export] public float LaunchDamage;

	/// <summary>Seconds it takes to turn back into a blob after a launch.</summary>
	[Export] public float RecoverSeconds;

	/// <summary>Seconds between launches, from the end of one recovery to the start of the next forming, varied a
	/// little. Each Lancer's first comes sooner, at random, so a group doesn't launch at once.</summary>
	[Export] public float LaunchCooldown;

	/// <summary>How rare accurate Lancers are: each rolls its accuracy as it spawns, from 0 (sloppy) to 1, leaning
	/// low by this power, then the difficulty ramp's accuracy is added, as for the basic enemy. Higher makes
	/// accurate ones rarer.</summary>
	[ExportGroup("Aiming")]
	[Export] public float SharpshooterRarity;

	/// <summary>How far ahead of a moving VNP it aims, from its least accurate (min) to its most (max): 0 aims where
	/// the VNP is as it starts forming, 1 where the VNP will be when the spike arrives if it keeps going.</summary>
	[Export] public float AimLeadMin;
	[Export] public float AimLeadMax;

	/// <summary>How far its aim strays at random either side, in degrees, from its most accurate (min) to its least
	/// (max).</summary>
	[Export] public float AimSpreadMin;
	[Export] public float AimSpreadMax;

	/// <summary>How much longer it stretches while formed into a spike, as a multiple of its length.</summary>
	[ExportGroup("Look")]
	[Export] public float SpikeStretch;

	/// <summary>How much paler it glows as it forms and launches, from 0 (none) to 1 (white).</summary>
	[Export] public float FormGlow;
}
