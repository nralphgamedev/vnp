using Godot;

/// <summary>Winning, losing and retreating, and the gateway. Values live in res://tuning/fight.tres.</summary>
[GlobalClass]
public partial class FightTuning : Resource
{
	/// <summary>Enemies to destroy to win the fight. Reaching it ends the fight at once.</summary>
	[ExportGroup("Winning, losing and retreating")]
	[Export] public int KillTarget;

	/// <summary>The time limit, in seconds. The fight does not end there; difficulty starts to escalate.</summary>
	[Export] public float TimeLimitSeconds;

	/// <summary>Seconds the VNP must stay on the gateway before the option to retreat appears.
	/// Leaving the gateway starts the count again.</summary>
	[Export] public float RetreatHoldSeconds;

	/// <summary>Where the gateway sits on the plane. The fight starts here.</summary>
	[ExportGroup("Gateway")]
	[Export] public Vector2 GatewayPosition;

	/// <summary>Size of the gateway ring. Later this is also the area that counts as "on the gateway".</summary>
	[Export] public float GatewayRadius;
}
