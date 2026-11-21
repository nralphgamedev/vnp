using Godot;

/// <summary>
/// Beam: a short-range beam that never misses. It shows the whole time a drone's target is in range,
/// and deals damage in ticks, one each cooldown. Damage upgrades make the beam wider.
/// </summary>
public sealed class BeamAttack : DroneAttack
{
	public BeamAttack(DroneSwarm swarm) : base(swarm)
	{
	}

	public override bool Attack(ref DroneSwarm.Drone drone, int enemy, ref Vector2 wantedVelocity, float dt)
	{
		EnemySwarm enemies = Fight.Enemies;
		Vector2 enemyPosition = enemies.PositionOf(enemy);
		if (drone.Position.DistanceTo(enemyPosition) > Swarm.WeaponRange)
			return false;

		Swarm.AddAttackBeam(drone.Position, enemyPosition, 0.6f);
		if (drone.FireTimer <= 0f)
		{
			Fight.Effects.Impact(enemyPosition, Weapon.Color, 0.4f);
			Fight.Sounds.Play(Sound.DroneBeamTick);
			enemies.Damage(enemy, Swarm.WeaponDamage);
			drone.FireTimer = Swarm.WeaponCooldown;
		}
		return false;
	}
}
