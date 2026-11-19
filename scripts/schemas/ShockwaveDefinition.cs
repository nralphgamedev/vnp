using Godot;

/// <summary>
/// A weapon that sends out a ring from the VNP at intervals. The ring spreads from where the VNP was when
/// it went out, and damages each enemy once as it passes through. Area of effect upgrades make it reach
/// further.
/// </summary>
[GlobalClass]
public partial class ShockwaveDefinition : WeaponDefinition
{
	/// <summary>How far the ring reaches before it fades, in units, before area of effect upgrades.</summary>
	[Export] public float Radius;

	/// <summary>How fast the ring spreads, in units per second.</summary>
	[Export] public float RingSpeed;
}
