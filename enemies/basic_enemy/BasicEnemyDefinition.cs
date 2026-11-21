using Godot;

/// <summary>
/// The basic enemy: it chases, holds at a distance, circles its target and fires projectiles aimed ahead
/// of it. BasicEnemy.cs runs it.
/// </summary>
[GlobalClass]
public partial class BasicEnemyDefinition : EnemyDefinition
{
	/// <summary>An enemy fires once its target is this close.</summary>
	[ExportGroup("Attacking")]
	[Export] public float AttackRange;

	/// <summary>A chasing enemy stops closing in at this distance and slowly circles its target instead.
	/// 0 makes it keep charging.</summary>
	[Export] public float HoldDistance;

	/// <summary>Over this many units either side of the hold distance, an enemy blends from closing in to
	/// circling, rather than switching at one exact distance. Stops enemies twitching when their target
	/// moves back and forth across the hold distance.</summary>
	[Export] public float HoldBlendDistance;

	/// <summary>Seconds between shots from one enemy.</summary>
	[Export] public float FireCooldown;

	/// <summary>Damage of one enemy projectile at the start of the fight.</summary>
	[Export] public float ProjectileDamage;

	/// <summary>Speed of an enemy projectile. The further this is above the VNP's speed,
	/// the harder shots are to outrun.</summary>
	[Export] public float ProjectileSpeed;

	/// <summary>Seconds an enemy projectile flies before it fizzles out. Speed times this is its reach.</summary>
	[Export] public float ProjectileLifetime;

	// Accuracy. Each enemy rolls its own accuracy when it spawns, from 0 (the sloppiest) to 1 (a
	// sharpshooter), and keeps it. Accuracy sets how far ahead it leads a moving target and how far
	// each shot strays. The difficulty ramp and escalation steps raise the accuracy of enemies spawned
	// later (see difficulty.tres), capped at 1.

	/// <summary>How far ahead the sloppiest enemy leads a moving target. 1 aims exactly where the target
	/// will be when the shot arrives, so only a change of direction dodges it; 0 aims at where it is
	/// now, so anything moving is missed.</summary>
	[ExportGroup("Accuracy")]
	[Export] public float AimLeadMin;

	/// <summary>How far ahead a sharpshooter leads a moving target, on the same scale.</summary>
	[Export] public float AimLeadMax;

	/// <summary>How far, in degrees, a shot from the sloppiest enemy can stray either side of its aim.</summary>
	[Export] public float AimSpreadMax;

	/// <summary>How far, in degrees, a shot from a sharpshooter can stray either side of its aim.</summary>
	[Export] public float AimSpreadMin;

	/// <summary>How rare sharpshooters are. 1 spreads accuracy evenly from 0 to 1; higher makes most
	/// enemies sloppy and sharp ones rare.</summary>
	[Export] public float SharpshooterRarity;

	[ExportGroup("Look")]
	[Export] public Color ProjectileColor;
}
