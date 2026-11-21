using Godot;

/// <summary>
/// What every enemy type has: its body, movement, targeting and flocking. Each type adds its own attack in
/// a subclass in its folder, such as BasicEnemyDefinition in res://enemies/basic_enemy/, with the code that
/// runs it beside it. Health and damage are for an enemy spawned at the start of the fight; the difficulty
/// ramp raises them for enemies spawned later.
/// </summary>
[GlobalClass]
public partial class EnemyDefinition : Resource
{
	/// <summary>Stays the same if the name changes. Used to tell enemy types apart.</summary>
	[Export] public string Id;

	/// <summary>Shown to the player.</summary>
	[Export] public string Name;

	/// <summary>Enemies in each spawned group of this type, including in waves.</summary>
	[Export] public int GroupSize;

	/// <summary>Health of an enemy spawned at the start of the fight.</summary>
	[ExportGroup("Body")]
	[Export] public float Health;

	/// <summary>How far from its center an enemy can be hit, and touches things. Weapons that hit an area,
	/// and drones keeping their distance, measure to this edge.</summary>
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

	/// <summary>How fast an enemy turns to face where it is going or attacking, in degrees per second.</summary>
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
	[ExportGroup("Targeting")]
	[Export] public float AggroRange;

	/// <summary>A chasing enemy gives up once nothing of the player's is within this distance.
	/// Keep it above the aggro range.</summary>
	[Export] public float LoseAggroRange;

	/// <summary>Off: it goes after whichever is nearest, the VNP or a drone. On: it goes after the nearest
	/// drone, and after the VNP only when no drone is within its aggro range.</summary>
	[Export] public bool TargetsDronesFirst;

	/// <summary>Seconds an enemy keeps chasing the same drone, or the VNP, before it looks again for a
	/// target. It looks again sooner if its target is destroyed or gets out of reach. Stops enemies
	/// flicking between drones that fly past them, such as ramming drones.</summary>
	[Export] public float TargetHoldSeconds;

	/// <summary>How much farther back than usual drones stay from this enemy's edge while they surround it.
	/// Ramming drones ignore it. 0 for an enemy drones can safely get close to.</summary>
	[ExportGroup("Drones attacking it")]
	[Export] public float DroneKeepBack;

	/// <summary>Enemies of the same type this close to each other count as flockmates.</summary>
	[ExportGroup("Flocking")]
	[Export] public float NeighborRadius;

	/// <summary>Enemies closer than this push apart. Larger enemies also push apart before they overlap.</summary>
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

	/// <summary>Scale of the enemy model.</summary>
	[ExportGroup("Look")]
	[Export] public float Size;

	[Export] public Color Color;
}
