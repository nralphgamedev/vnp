using Godot;

/// <summary>
/// How many asteroids a fight has: one of a few amounts (Sparse, Normal, Rich), one picked for each fight by
/// its weight. The GDD makes the amount of asteroids one of the settings that set a fight's difficulty; phase 3
/// tests how much it matters. Files are in res://tuning/asteroid_amounts/, listed in asteroids.tres.
///
/// It scales the numbers in asteroids.tres rather than replacing them: the fields and lone asteroids around the
/// gateway, and those filled in as the VNP explores, so the arena stays as rich or as sparse however far it goes.
/// Normal scales by 1, the fights as they were before amounts.
/// </summary>
[GlobalClass]
public partial class AsteroidAmountDefinition : Resource
{
	[Export] public string Id;

	/// <summary>Shown on the result screen and the starting choice cheat.</summary>
	[Export] public string Name;

	/// <summary>How likely this amount is to be picked for a fight, against the others' weights.</summary>
	[Export] public float Weight = 1f;

	/// <summary>Multiplies how many asteroid fields there are (FieldsMin and FieldsMax, and FieldsPerCellMin
	/// and FieldsPerCellMax), rounded to whole fields.</summary>
	[Export] public float FieldScale = 1f;

	/// <summary>Multiplies how many lone asteroids there are (LoneCount and LonePerCell), rounded.</summary>
	[Export] public float LoneScale = 1f;
}
