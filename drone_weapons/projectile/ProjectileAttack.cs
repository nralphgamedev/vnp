using Godot;

/// <summary>
/// Projectile: each drone fires single shots at its target once it is in range and off cooldown, aimed
/// ahead of where the target is going. Damage upgrades make the shots bigger.
/// </summary>
public sealed class ProjectileAttack : DroneAttack
{
	public ProjectileAttack(DroneSwarm swarm) : base(swarm)
	{
	}

	public override bool Attack(ref DroneSwarm.Drone drone, int enemy, ref Vector2 wantedVelocity, float dt)
	{
		EnemySwarm enemies = Fight.Enemies;
		if (drone.FireTimer > 0f || EdgeDistance(drone, enemy) > Swarm.WeaponRange)
			return false;

		// Let the shot fly a little past the enemy before it fizzles out.
		float lifetime = (Swarm.WeaponRange + enemies.RadiusOf(enemy)) * 1.5f / Weapon.ProjectileSpeed;

		// Aim ahead of a moving enemy, at where it will be when the shot arrives.
		Vector2 aimAt = PlaneMath.AimAhead(drone.Position, enemies.PositionOf(enemy), enemies.VelocityOf(enemy),
			Weapon.ProjectileSpeed, lifetime);
		Vector2 toAim = aimAt - drone.Position;
		if (toAim.LengthSquared() < 0.0001f)
			return false;

		Fight.Projectiles.Fire(drone.Position, toAim.Normalized(), Swarm.WeaponDamage,
			Weapon.ProjectileSpeed, lifetime, fromEnemy: false, Weapon.Color, Swarm.Source, 0.6f * Swarm.Power);
		Fight.Effects.MuzzleFlash(drone.Position, toAim, Weapon.Color, 0.5f);
		Fight.Sounds.Play(Sound.DroneProjectileFire);
		drone.FireTimer = Swarm.WeaponCooldown * Rng.Range(0.9f, 1.1f);
		return false;
	}
}
