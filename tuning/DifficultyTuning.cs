using Godot;

/// <summary>
/// How enemies grow stronger past the time limit, the same for every fight. How a fight grows harder before the limit
/// is its plan's difficulty curve, in res://director/curves/ (docs/dev/FIGHT_DIRECTOR_SPEC.md). Values live in
/// res://tuning/difficulty.tres.
/// </summary>
[GlobalClass]
public partial class DifficultyTuning : Resource
{
	// At the limit, and again every step after it, difficulty rises by these amounts, on top of the
	// plan's difficulty curve during the fight. Each is the fraction added per step: 0.6 means +60% at the limit,
	// +120% a step later, and so on. Every step flashes the screen edges red with a "!".

	/// <summary>Seconds between escalation steps after the limit.</summary>
	[ExportGroup("Escalation past the time limit")]
	[Export] public float EscalationStepSeconds;

	/// <summary>Extra health of newly spawned enemies per step.</summary>
	[Export] public float EscalationEnemyHealthPerStep;

	/// <summary>Extra projectile damage of newly spawned enemies per step.</summary>
	[Export] public float EscalationEnemyDamagePerStep;

	/// <summary>Extra spawn rate per step. 1 doubles it at the limit, triples it a step later.</summary>
	[Export] public float EscalationSpawnRatePerStep;

	/// <summary>Extra aggro range per step, for every enemy. Wider aggro makes enemies come for the
	/// VNP from farther away, which is what pushes the player to retreat.</summary>
	[Export] public float EscalationAggroRangePerStep;

	/// <summary>Accuracy added to newly spawned enemies at each step, on the 0 to 1 accuracy scale.</summary>
	[Export] public float EscalationEnemyAccuracyPerStep;

	/// <summary>Extra speed of newly spawned enemies per step, for every enemy type, chasing and wandering alike.
	/// Each keeps the speed it spawned with. 0.1 makes enemies a tenth faster at the limit and half as fast again
	/// five steps later. Enemies that become faster than the VNP can no longer be outrun.</summary>
	[Export] public float EscalationEnemySpeedPerStep;

	/// <summary>Extra health of newly spawned Grey Goo per step.</summary>
	[Export] public float EscalationGreyGooHealthPerStep;

	/// <summary>Extra damage of newly spawned Grey Goo per step: contact damage, and how fast it drains drones.</summary>
	[Export] public float EscalationGreyGooDamagePerStep;

	/// <summary>Seconds the red edge flash and "!" last at each step.</summary>
	[Export] public float EscalationAlertSeconds;
}
