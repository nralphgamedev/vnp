using Godot;

/// <summary>How enemies grow stronger during the fight and past the time limit. Values live in res://tuning/difficulty.tres.</summary>
[GlobalClass]
public partial class DifficultyTuning : Resource
{
	// Each value is the extra fraction added per minute of fight time. 0.2 means +20% a minute.
	// An enemy keeps the strength it had when it spawned.

	/// <summary>Extra enemy health per minute.</summary>
	[ExportGroup("Ramp during the fight")]
	[Export] public float RampEnemyHealthPerMinute;

	/// <summary>Extra enemy projectile damage per minute.</summary>
	[Export] public float RampEnemyDamagePerMinute;

	/// <summary>Extra spawn rate per minute.</summary>
	[Export] public float RampSpawnRatePerMinute;

	// At the limit, and again every step after it, difficulty rises by these amounts, on top of the
	// ramp during the fight. Each is the fraction added per step: 0.6 means +60% at the limit,
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

	/// <summary>Seconds the red edge flash and "!" last at each step.</summary>
	[Export] public float EscalationAlertSeconds;
}
