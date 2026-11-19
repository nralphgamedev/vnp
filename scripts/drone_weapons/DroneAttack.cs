using Godot;

/// <summary>
/// How the drones attack with one kind of Drone Weapon. The swarm picks each drone's target and steers it
/// to its spot around that target; the attack then fires, beams, or rams. There is one attack at a time,
/// for whichever Drone Weapon the drones use, and it is swapped when they switch. A new kind of Drone
/// Weapon is a new subclass in its own folder, like the VNP Weapons in scripts/weapons/.
///
/// An attack keeps any per-drone state in the drone's AttackState and AttackTime, which start at 0 and
/// are reset to 0 when the drones switch weapon.
/// </summary>
public abstract class DroneAttack
{
	protected readonly DroneSwarm Swarm;

	protected DroneAttack(DroneSwarm swarm)
	{
		Swarm = swarm;
	}

	protected Fight Fight => Swarm.Fight;
	protected DroneWeaponDefinition Weapon => Swarm.Weapon;

	/// <summary>The attack for a Drone Weapon's kind.</summary>
	public static DroneAttack Create(DroneSwarm swarm, DroneWeaponDefinition weapon) => weapon.Kind switch
	{
		DroneWeaponKind.Beam => new BeamAttack(swarm),
		DroneWeaponKind.Ram => new RamAttack(swarm),
		_ => new ProjectileAttack(swarm),
	};

	/// <summary>
	/// Attacks an enemy for one frame. <paramref name="wantedVelocity"/> comes in as the swarm's steering
	/// toward the drone's spot around the enemy. Return false to let the swarm steer the drone as usual,
	/// or true when the attack has set <paramref name="wantedVelocity"/> itself, to be used as the drone's
	/// velocity exactly, with no speed limit or smoothing.
	/// </summary>
	public abstract bool Attack(ref DroneSwarm.Drone drone, int enemy, ref Vector2 wantedVelocity, float dt);

	/// <summary>Called before Attack when a drone's target changes to another enemy.</summary>
	public virtual void TargetChanged(ref DroneSwarm.Drone drone)
	{
	}

	/// <summary>
	/// Called each frame for a drone with no enemy to attack, after the swarm has steered it to harvest or
	/// flock. Lets an attack finish a move first. Returns true, like Attack, when it has set the velocity.
	/// </summary>
	public virtual bool WithoutTarget(ref DroneSwarm.Drone drone, ref Vector2 wantedVelocity, float dt) => false;
}
