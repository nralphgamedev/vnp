using Godot;

/// <summary>
/// The Quiller, a Grey Goo type: it forms a volley of spikes at the VNP, fired from every side to curl in on it,
/// and takes them back as they arc home; each one broken first costs it health. Everything else,
/// touch damage, debris and the wobbling look, is shared by the Grey Goo types (GreyGooDefinition). Quiller.cs
/// runs it; its spikes fly as fragile shots (FragileShots.cs).
/// </summary>
[GlobalClass]
public partial class QuillerDefinition : GreyGooDefinition
{
	/// <summary>It forms a volley once the VNP is this close and its cooldown is done.</summary>
	[ExportGroup("Volley")]
	[Export] public float VolleyRange;

	/// <summary>Seconds it swells, forming its spikes, before it fires: the player's warning. Each spike's aim is set
	/// as it starts.</summary>
	[Export] public float FormSeconds;

	/// <summary>Spikes in each volley.</summary>
	[Export] public int SpikeCount;

	/// <summary>How far each spike's target point strays either side of the VNP, in degrees around the Quiller,
	/// so a volley spreads rather than all meeting at one point.</summary>
	[Export] public float SpreadDegrees;

	/// <summary>Seconds between volleys, counted once all of the last volley's spikes are back or broken, varied a
	/// little. Each Quiller's first comes sooner, at random.</summary>
	[Export] public float VolleyCooldown;

	/// <summary>How fast a spike flies out, in units a second.</summary>
	[ExportGroup("Spikes")]
	[Export] public float SpikeSpeed;

	/// <summary>How quickly a spike can turn while curling in on its target point, in degrees a second. Spikes leave
	/// from every side, so one launched away from its target sweeps round in an arc: faster turns make tighter arcs.</summary>
	[Export] public float OutTurnDegrees;

	/// <summary>How far a spike flies on past the point its target was when the volley was aimed, before it turns
	/// back, unless it hits something first.</summary>
	[Export] public float SpikeBeyond;

	/// <summary>How fast a spike flies back, and how quickly it can turn to arc home, in degrees a second. A slower
	/// turn makes a wider arc.</summary>
	[Export] public float ReturnSpeed;
	[Export] public float ReturnTurnDegrees;

	/// <summary>Damage each spike does to the VNP or a drone it hits, going out or coming back, at the start of the
	/// fight. It grows with the Grey Goo damage ramp in difficulty.tres.</summary>
	[Export] public float SpikeDamage;

	/// <summary>How wide a spike is, for hitting and being hit.</summary>
	[Export] public float SpikeRadius;

	/// <summary>Health it loses for each of its spikes broken before it gets back.</summary>
	[Export] public float SpikeLossHealth;

	/// <summary>How much it swells while forming a volley, as a fraction of its size.</summary>
	[ExportGroup("Look")]
	[Export] public float FormSwell;

	/// <summary>How much paler it glows as it forms, from 0 (none) to 1 (white).</summary>
	[Export] public float FormGlow;
}
