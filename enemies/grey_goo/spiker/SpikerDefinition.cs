using Godot;

/// <summary>
/// The Spiker, a Grey Goo type: it pushes out one or two spikes at a time, aimed well at drones within its reach,
/// leading them as they move, or at the VNP when no drone is in reach. Each spike can impale a drone, which it
/// then draws back in and absorbs, growing larger and tougher; spikes damage the VNP but can't impale it. Everything else, touch damage, growth per absorbed drone,
/// debris and the wobbling look, is shared by the Grey Goo types (GreyGooDefinition). Spiker.cs runs it.
/// </summary>
[GlobalClass]
public partial class SpikerDefinition : GreyGooDefinition
{
	/// <summary>It thrusts once a drone or the VNP is this close to its edge and its cooldown is done, at those
	/// within this distance.</summary>
	[ExportGroup("Thrust")]
	[Export] public float Reach;

	/// <summary>Seconds it bristles before the spikes go out: the player's warning. A spike at the VNP has its line
	/// set as it starts; one at a drone is aimed again as it goes out.</summary>
	[Export] public float FormSeconds;

	/// <summary>How many spikes one thrust has, picked at random between these, each at a different drone, the
	/// nearest first. With fewer drones in reach than spikes, the VNP gets one if it is in reach.</summary>
	[Export] public int MinSpikes;
	[Export] public int MaxSpikes;

	/// <summary>How far ahead of a moving drone a spike aims, from 0 (where it is) to 1 (exactly where it will be
	/// when the tip gets there, if it keeps going), and how far it strays either side, in degrees.</summary>
	[Export] public float AimLead;
	[Export] public float AimSpreadDegrees;

	/// <summary>How fast a spike at a drone can swing toward it as it shoots out, in degrees a second, so a drone
	/// turning away is still caught. 0 for a straight thrust.</summary>
	[Export] public float TrackDegrees;

	/// <summary>Seconds between thrusts, counted once every spike of the last is back in, varied a little. Each
	/// Spiker's first comes sooner, at random.</summary>
	[Export] public float Cooldown;

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
