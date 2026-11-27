using Godot;

/// <summary>
/// How a fight grows harder before the time limit (docs/dev/FIGHT_DIRECTOR_SPEC.md): what enemies spawned each
/// minute gain over those spawned at the start, and how much faster groups spawn. A piece of a plan: each is a file
/// in res://director/curves/, and plans share them. Escalation past the time limit is shared by every plan, in
/// res://tuning/difficulty.tres.
///
/// Each value is added per minute of fight time: 0.2 health per minute means enemies spawned at 5:00 have twice the
/// health of those spawned at the start. Each enemy keeps the strength it spawned with. The classic curve is
/// the ramps every fight had before plans owned them; new plans lean on numbers, at the designer's request, so
/// each enemy stays one drones can beat and the pressure comes from how many there are.
/// </summary>
[GlobalClass]
public partial class DifficultyCurveDefinition : Resource
{
	[Export] public string Id;

	/// <summary>How much faster groups spawn, per minute (each wave's growth is the plan's waves').</summary>
	[ExportGroup("Numbers")]
	[Export] public float RampSpawnRatePerMinute;

	/// <summary>Speed of every enemy type, per minute.</summary>
	[ExportGroup("Stats")]
	[Export] public float RampEnemySpeedPerMinute;

	/// <summary>Grey Goo's health and damage, per minute; its drain on drones grows with its damage.</summary>
	[Export] public float RampGreyGooHealthPerMinute;
	[Export] public float RampGreyGooDamagePerMinute;

	/// <summary>The basic enemy's health, damage and accuracy (on the 0 to 1 scale), per minute.</summary>
	[Export] public float RampEnemyHealthPerMinute;
	[Export] public float RampEnemyDamagePerMinute;
	[Export] public float RampEnemyAccuracyPerMinute;
}
