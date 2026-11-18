using Godot;

/// <summary>
/// An enemy type and all its numbers. An enemy goes after whichever is nearest: the VNP or one of its
/// drones. Health and projectile damage are for an enemy spawned at the start of the fight; the
/// difficulty ramp raises them for enemies spawned later.
/// </summary>
[GlobalClass]
public partial class EnemyDefinition : Resource
{
	/// <summary>Stays the same if the name changes. Used to tell enemy types apart.</summary>
	[Export] public string Id;

	/// <summary>Shown to the player.</summary>
	[Export] public string Name;

	/// <summary>Health of an enemy spawned at the start of the fight.</summary>
	[ExportGroup("Body")]
	[Export] public float Health;

	/// <summary>How close a drone projectile must pass to an enemy's center to hit it.
	/// Also how big an enemy is for Orbital Shards.</summary>
	[Export] public float HitRadius;

	/// <summary>Smallest mass of the debris an enemy leaves.</summary>
	[Export] public float DebrisMassMin;

	/// <summary>Largest mass of the debris an enemy leaves.</summary>
	[Export] public float DebrisMassMax;

	/// <summary>Speed while chasing. Below the VNP's speed, so the player can get away.</summary>
	[ExportGroup("Movement")]
	[Export] public float Speed;

	/// <summary>Speed while roaming with nothing to chase.</summary>
	[Export] public float WanderSpeed;

	/// <summary>How quickly an enemy changes speed or direction, in units per second squared.</summary>
	[Export] public float Acceleration;

	/// <summary>How fast an enemy turns to face where it is going or shooting, in degrees per second.</summary>
	[Export] public float TurnSpeed;

	/// <summary>How fast an idle enemy turns to face where it is going, in degrees per second.
	/// Lower looks calmer; higher follows every small change of course.</summary>
	[Export] public float IdleTurnSpeed;

	/// <summary>An idle enemy ignores changes of course smaller than this, in degrees, so small nudges
	/// from flockmates do not show as twitching. 0 follows every change.</summary>
	[Export] public float IdleTurnDeadband;

	/// <summary>How far an idle enemy strays from its home point before turning back.</summary>
	[Export] public float WanderRadius;

	/// <summary>An idle enemy notices the VNP or a drone that comes this close, then chases and attacks.</summary>
	[ExportGroup("Attacking")]
	[Export] public float AggroRange;

	/// <summary>A chasing enemy gives up once nothing of the player's is within this distance.
	/// Keep it above the aggro range.</summary>
	[Export] public float LoseAggroRange;

	/// <summary>An enemy fires once its target is this close.</summary>
	[Export] public float AttackRange;

	/// <summary>A chasing enemy stops closing in at this distance and slowly circles its target instead.
	/// 0 makes it keep charging.</summary>
	[Export] public float HoldDistance;

	/// <summary>Seconds between shots from one enemy.</summary>
	[Export] public float FireCooldown;

	/// <summary>Damage of one enemy projectile at the start of the fight.</summary>
	[Export] public float ProjectileDamage;

	/// <summary>Speed of an enemy projectile. The further this is above the VNP's speed,
	/// the harder shots are to outrun.</summary>
	[Export] public float ProjectileSpeed;

	/// <summary>Seconds an enemy projectile flies before it fizzles out. Speed times this is its reach.</summary>
	[Export] public float ProjectileLifetime;

	/// <summary>How well enemies aim at a moving target. 1 aims exactly where the target will be
	/// when the shot arrives, so only a change of direction dodges it. 0 aims at where the target
	/// is now, so anything moving is missed. The effect is steep: against a VNP circling at full
	/// speed, roughly 70% of shots hit at 1, 50% at 0.75, and under 20% at 0.5.</summary>
	[Export] public float AimLead;

	/// <summary>Enemies this close to each other count as flockmates.</summary>
	[ExportGroup("Flocking")]
	[Export] public float NeighborRadius;

	/// <summary>Enemies closer than this push apart.</summary>
	[Export] public float SeparationRadius;

	/// <summary>Strength of the push apart. Higher gives looser groups.</summary>
	[Export] public float Separation;

	/// <summary>How strongly an enemy matches the heading of its flockmates.</summary>
	[Export] public float Alignment;

	/// <summary>How strongly an enemy is pulled toward the middle of its flockmates.</summary>
	[Export] public float Cohesion;

	/// <summary>How strongly an idle enemy follows its own random heading instead of its flock.
	/// Lower gives tighter groups that move as one. Higher scatters them.</summary>
	[Export] public float WanderWeight;

	/// <summary>Scale of the enemy model. 1 is about 2.5 units across.</summary>
	[ExportGroup("Look")]
	[Export] public float Size;

	[Export] public Color Color;

	[Export] public Color ProjectileColor;
}
