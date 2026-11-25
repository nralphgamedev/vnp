using Godot;

/// <summary>
/// The Spiker, a Grey Goo type: it moves after the VNP, hurting it by touch, and thrusts spikes at drones within its
/// reach, nearest first, or at the VNP when no drone is in reach. A drone a spike meets is impaled, drawn back in
/// and absorbed, so the Spiker grows larger and tougher; spikes damage the VNP but can't impale it. Everything else, touch damage, growth per absorbed drone, debris and the wobbling look, is
/// shared by the Grey Goo types (GreyGooDefinition). Spiker.cs runs it.
/// </summary>
[GlobalClass]
public partial class SpikerDefinition : GreyGooDefinition
{
	/// <summary>It thrusts once a drone or the VNP is this close to its edge and its cooldown is done, at those
	/// within this distance.</summary>
	[ExportGroup("Thrust")]
	[Export] public float Reach;

	/// <summary>Seconds it bristles before the spikes go out: the player's warning. A spike at the VNP has its line
	/// set as it starts, so moving off it dodges; one at a drone is aimed again as it goes out.</summary>
	[Export] public float FormSeconds;

	/// <summary>How many spikes one thrust has, picked at random between these, each at a different drone, the
	/// nearest first. With fewer drones in reach than spikes, the VNP gets one if it is in reach.</summary>
	[Export] public int MinSpikes;
	[Export] public int MaxSpikes;

	/// <summary>Seconds between thrusts, counted once every spike of the last is back in, varied a little. Each
	/// Spiker's first comes sooner, at random.</summary>
	[Export] public float ThrustCooldown;

	/// <summary>How rare accurate ones are: each Spiker rolls its accuracy from 0 to 1 as it spawns, leaning low by
	/// this power, then the difficulty ramp's accuracy is added, as for the Lancer. Higher makes accurate ones
	/// rarer.</summary>
	[ExportGroup("Aiming")]
	[Export] public float SharpshooterRarity;

	/// <summary>How far ahead of a moving target it aims, from its least accurate (min) to its most (max): 0 aims
	/// where the target is (the VNP as it starts forming, a drone as the thrust starts), 1 where it will be when the
	/// spike gets there if it keeps going.</summary>
	[Export] public float AimLeadMin;
	[Export] public float AimLeadMax;

	/// <summary>How far each spike strays at random either side, in degrees, from its most accurate (min) to its
	/// least (max), so several spikes fan out.</summary>
	[Export] public float AimSpreadMin;
	[Export] public float AimSpreadMax;

	/// <summary>How fast a spike at a drone can swing toward it as it shoots out, in degrees a second, so a drone
	/// turning away is still caught. A spike at the VNP never swings, so it can be dodged. 0 for a straight thrust.</summary>
	[Export] public float TrackDegrees;

	/// <summary>How fast a spike's tip shoots out, in units a second.</summary>
	[ExportGroup("Spikes")]
	[Export] public float SpikeSpeed;

	/// <summary>How far a spike reaches past the point its target was when the thrust was aimed.</summary>
	[Export] public float SpikeBeyond;

	/// <summary>How fast a spike draws an impaled drone back in, in units a second. Slower gives the player longer
	/// to kill the Spiker and free it. An empty spike comes back at its outward speed.</summary>
	[Export] public float PullSpeed;

	/// <summary>Damage a spike does to the VNP, once per spike, at the start of the fight. It grows with the Grey Goo
	/// damage ramp in difficulty.tres.</summary>
	[Export] public float SpikeDamage;

	/// <summary>How wide a spike is, for hitting and for its look.</summary>
	[Export] public float SpikeWidth;

	/// <summary>How much it swells while bristling, as a fraction of its size.</summary>
	[ExportGroup("Look")]
	[Export] public float FormSwell;

	/// <summary>How much paler it glows while bristling, from 0 (none) to 1 (white).</summary>
	[Export] public float FormGlow;
}
