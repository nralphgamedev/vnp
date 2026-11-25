using Godot;

/// <summary>
/// The Burster, a Grey Goo type: faster than the others, with a swollen, throbbing body that throbs faster as it is
/// hurt. When destroyed, it bursts into tiny blobs thrown out all round it that curl in on the VNP, each popping on
/// the first thing it hits, the VNP or a drone. The player's weapons can break the blobs in flight, as they break a
/// Quiller's spikes. Everything else, touch damage, debris and the wobbling look, is shared by the Grey Goo types
/// (GreyGooDefinition). Burster.cs runs it; its blobs fly as fragile shots (FragileShots.cs).
/// </summary>
[GlobalClass]
public partial class BursterDefinition : GreyGooDefinition
{
	/// <summary>How many tiny blobs it bursts into.</summary>
	[ExportGroup("Burst")]
	[Export] public int BlobCount;

	/// <summary>How fast the blobs fly, in units a second, and how quickly they can turn toward the VNP, in degrees
	/// a second. They leave in every direction, so a slower turn makes wider arcs.</summary>
	[Export] public float BlobSpeed;
	[Export] public float BlobTurnDegrees;

	/// <summary>Seconds a blob flies before it fizzles out, if it hits nothing.</summary>
	[Export] public float BlobLife;

	/// <summary>Damage a blob does to the VNP or a drone it hits, at the start of the fight. It grows with the Grey
	/// Goo damage ramp in difficulty.tres.</summary>
	[Export] public float BlobDamage;

	/// <summary>How big a blob is, for hitting and being hit.</summary>
	[Export] public float BlobRadius;

	/// <summary>A blob explodes as it pops, and its damage reaches the VNP and every drone within this distance of
	/// where it popped. It never hurts enemies. 0 hurts only what it hit.</summary>
	[Export] public float BlobBlastRadius;

	/// <summary>How much it swells and shrinks as it throbs, as a fraction of its size, and how many throbs a
	/// second at full health. It throbs up to <see cref="HurtThrobFactor"/> times as fast as its health runs out,
	/// so the player can see it is about to burst.</summary>
	[ExportGroup("Look")]
	[Export] public float Throb;
	[Export] public float ThrobSpeed;
	[Export] public float HurtThrobFactor;
}
