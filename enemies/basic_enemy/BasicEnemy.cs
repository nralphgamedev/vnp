using Arbiter;
using Godot;

/// <summary>
/// The basic enemy: when chasing, it closes in to its hold distance, then circles its target and fires
/// projectiles aimed ahead of it. Its numbers are in basic_enemy.tres.
/// </summary>
public sealed class BasicEnemy : EnemyBehavior
{
	private readonly BasicEnemyDefinition _type;

	public BasicEnemy(EnemySwarm swarm, BasicEnemyDefinition definition) : base(swarm, definition)
	{
		_type = definition;
	}

	public override Mesh Mesh => Placeholders.Mesh("enemy");

	/// <summary>Its body, tagged as one that shoots. It gives no warning before it fires, so it declares no hazard
	/// of its own: its shots are declared by the projectiles as they fly (Projectiles.DeclareThreats).</summary>
	public override void DeclareThreats(in EnemySwarm.Enemy enemy, Situation situation) =>
		DeclareBody(enemy, situation, ThreatTags.Shoots);

	public override void Spawned(ref EnemySwarm.Enemy enemy)
	{
		// Enemies spawned later in the fight are stronger, and keep that strength.
		enemy.Health = _type.Health * Fight.EnemyHealthScale;
		enemy.Damage = _type.ProjectileDamage * Fight.EnemyDamageScale;
		enemy.Accuracy = RollAccuracy(Fight.EnemyAccuracyBonus);
		enemy.FireTimer = Rng.Range(0f, _type.FireCooldown); // So a group does not fire in unison.
		enemy.Radius = _type.HitRadius;
		enemy.Scale = _type.Size;
	}

	/// <summary>
	/// An enemy's accuracy, from 0 to 1. Rolled so that most are sloppy and sharpshooters are rare,
	/// then raised by the difficulty ramp and escalation, and capped at 1.
	/// </summary>
	private float RollAccuracy(float bonus)
	{
		float roll = Mathf.Pow(Rng.Value(), Mathf.Max(0.01f, _type.SharpshooterRarity));
		return Mathf.Clamp(roll + bonus, 0f, 1f);
	}

	public override Vector2 Chase(ref EnemySwarm.Enemy enemy, Vector2 track, float distance)
	{
		// Close enough: stop closing in and circle the target at half speed. Blend between the two around
		// the hold distance, so a target moving back and forth across it does not make the enemy flip
		// between them.
		float blend = _type.HoldBlendDistance > 0f
			? Mathf.Clamp((distance - _type.HoldDistance) / (2f * _type.HoldBlendDistance) + 0.5f, 0f, 1f)
			: (distance > _type.HoldDistance ? 1f : 0f);
		Vector2 circle = new Vector2(-track.Y, track.X) * enemy.OrbitSign * 0.5f;
		return track * blend + circle * (1f - blend);
	}

	public override bool Act(ref EnemySwarm.Enemy enemy, int index, in EnemySwarm.Target target, float dt)
	{
		enemy.FireTimer = Mathf.Max(0f, enemy.FireTimer - dt);
		bool inAttackRange = enemy.Aggro && target.Distance <= _type.AttackRange;
		if (!inAttackRange)
			return false;

		// Face the target while in firing range.
		float turnStep = Mathf.DegToRad(_type.TurnSpeed) * dt;
		enemy.Yaw = Mathf.RotateToward(enemy.Yaw, PlaneMath.YawFacing(target.Track), turnStep);

		if (enemy.FireTimer <= 0f)
		{
			// Aim ahead of a moving target, by as much as this enemy's accuracy allows: 1 aims exactly where
			// the target will be, 0 at where it is now. Then stray a little, less for more accurate enemies.
			float lead = Mathf.Lerp(_type.AimLeadMin, _type.AimLeadMax, enemy.Accuracy);
			Vector2 meetingPoint = PlaneMath.AimAhead(enemy.Position, target.Position, target.Velocity,
				_type.ProjectileSpeed, _type.ProjectileLifetime);
			Vector2 aimAt = target.Position.Lerp(meetingPoint, lead);
			Vector2 aim = aimAt.DistanceSquaredTo(enemy.Position) > 0.0001f
				? (aimAt - enemy.Position).Normalized()
				: target.Direction;
			float spread = Mathf.DegToRad(Mathf.Lerp(_type.AimSpreadMax, _type.AimSpreadMin, enemy.Accuracy));
			aim = aim.Rotated(Rng.Range(-spread, spread));

			Fight.Projectiles.Fire(enemy.Position, aim, enemy.Damage,
				_type.ProjectileSpeed, _type.ProjectileLifetime, fromEnemy: true, _type.ProjectileColor, Source);
			Fight.Sounds.Play(Sound.EnemyFire);
			enemy.FireTimer = _type.FireCooldown * Rng.Range(0.85f, 1.15f);
		}
		return true;
	}

	public override void Died(in EnemySwarm.Enemy enemy)
	{
		Fight.Effects.EnemyDeath(enemy.Position, _type.Color);
		Fight.Sounds.Play(Sound.EnemyDeath);
	}

	public override void Draw(int slot, in EnemySwarm.Enemy enemy, Color color)
	{
		Batch.Set(slot, enemy.Position, 0f, enemy.Yaw, enemy.Scale, color);
	}
}
