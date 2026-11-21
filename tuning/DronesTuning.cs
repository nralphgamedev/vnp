using Godot;

/// <summary>The drones that flock around the VNP. Values live in res://tuning/drones.tres.</summary>
[GlobalClass]
public partial class DronesTuning : Resource
{
	/// <summary>Drones the VNP starts the fight with.</summary>
	[ExportGroup("Drones")]
	[Export] public int StartingCount;

	/// <summary>Most drones the VNP can have. Harvesting builds drones up to this,
	/// and gives only fight experience once it is reached.</summary>
	[Export] public int Cap;

	/// <summary>Health of one drone.</summary>
	[Export] public float Health;

	/// <summary>How close an enemy projectile must pass to a drone's center to hit it.</summary>
	[Export] public float HitRadius;

	/// <summary>The Drone Weapon every drone starts with. One folder each in res://drone_weapons/.</summary>
	[Export] public DroneWeaponDefinition StartingWeapon;

	/// <summary>Top speed of a drone. Above the VNP's speed, so drones can catch up.
	/// The gap between the two is how fast a drone left behind closes the distance.</summary>
	[ExportGroup("Movement")]
	[Export] public float Speed;

	/// <summary>How quickly a drone changes speed or direction, in units per second squared.
	/// Higher makes the flock tighter and twitchier. Lower makes it looser and floatier.</summary>
	[Export] public float Acceleration;

	/// <summary>How fast a drone turns to face where it is going or shooting, in degrees per second.</summary>
	[Export] public float TurnSpeed;

	/// <summary>How far from the VNP the flock hovers when it has nothing to attack.</summary>
	[Export] public float FlockRadius;

	/// <summary>How fast drones mill about within the flock, on top of keeping pace with the VNP.
	/// Also how quickly a drone that has dropped a little behind closes the gap.</summary>
	[Export] public float FlockSpeed;

	/// <summary>A drone farther than this from the VNP drops everything and sprints back at top speed.</summary>
	[Export] public float SprintDistance;

	/// <summary>Drones closer than this to each other push apart.</summary>
	[Export] public float SeparationRadius;

	/// <summary>Strength of the push apart.</summary>
	[Export] public float Separation;

	/// <summary>Drones attack enemies that come this close to the VNP. An enemy that ends up farther
	/// away than this, because it fled or the VNP moved off, is dropped and the drones return.
	/// While any enemy is in this range, attacking comes before harvesting.</summary>
	[ExportGroup("Attacking")]
	[Export] public float EngageRange;

	/// <summary>How fast drones circle whatever they surround, in degrees per second.</summary>
	[Export] public float CircleSpeed;

	/// <summary>How hard drones brake as they reach their spot around a target, as a fraction of
	/// their acceleration. Lower starts braking earlier and arrives more gently; higher arrives
	/// faster but closer to overshooting. Keep it at or below 0.5 to avoid bouncing.</summary>
	[Export] public float ArriveBraking;

	/// <summary>Scale of the drone model. 1 is about 1.3 units long.</summary>
	[ExportGroup("Look")]
	[Export] public float Size;

	/// <summary>Seconds a destroyed drone's wreckage takes to disintegrate.</summary>
	[Export] public float WreckTime;

	/// <summary>Seconds an absorbed drone takes to be pulled into the enemy that absorbed it.</summary>
	[Export] public float AbsorbTime;

	[Export] public Color Color;
}
