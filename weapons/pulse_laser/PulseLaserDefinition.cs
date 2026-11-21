using Godot;

/// <summary>A weapon that fires a straight beam at the nearest enemy in range and damages only that enemy.</summary>
[GlobalClass]
public partial class PulseLaserDefinition : WeaponDefinition
{
	/// <summary>How far away an enemy can be and still be shot.</summary>
	[Export] public float Range;

	/// <summary>Seconds the beam stays visible after a shot. Cosmetic.</summary>
	[Export] public float BeamTime;

	/// <summary>Thickness of the drawn beam. Cosmetic, because the beam only hits the enemy it aimed at.</summary>
	[Export] public float BeamWidth;
}
