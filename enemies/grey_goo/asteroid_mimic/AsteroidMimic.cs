using Godot;

/// <summary>
/// The asteroid mimic, awake. Asleep, it isn't an enemy at all but an asteroid that Harvestables.cs marks as a
/// mimic, so it looks, moves and records exactly like one, and pilots see an asteroid. Harvestables wakes it, taking
/// the rock away and spawning this in its place (EnemySwarm.SpawnAwake), when the VNP or a drone comes within its
/// aggro range or tries to harvest it.
///
/// Awake, everything shared by Grey Goo (GreyGoo.cs) applies: it drains and absorbs drones and grows with each, but
/// never splits. Its own part is its shot: once every FireCooldown it fires one at the nearest of the VNP or a drone
/// within FireRange, through the fight's projectiles, so it is an ordinary enemy shot that Orbital Shards blocking
/// stops and that declares itself to pilots. Its numbers are in asteroid_mimic.tres.
/// </summary>
public sealed class AsteroidMimic : GreyGoo
{
	private readonly AsteroidMimicDefinition _type;

	public AsteroidMimic(EnemySwarm swarm, AsteroidMimicDefinition definition) : base(swarm, definition)
	{
		_type = definition;
	}

	public override Mesh Mesh => Placeholders.Mesh("asteroid_mimic");

	public override void Spawned(ref EnemySwarm.Enemy enemy)
	{
		base.Spawned(ref enemy);
		enemy.AttackDamage = _type.ShotDamage * Fight.GreyGooDamageScale;
		// Its first shot comes soon after it wakes, but not at once.
		enemy.AttackTimer = _type.FireCooldown * Rng.Range(0.4f, 0.8f);
	}

	/// <summary>Fires at the nearest of the VNP or a drone in range, once every cooldown. Its touch cooldown is the
	/// shared FireTimer, so the shot keeps its own, AttackTimer.</summary>
	protected override bool ActOwn(ref EnemySwarm.Enemy enemy, int index, in EnemySwarm.Target target, float dt)
	{
		enemy.AttackTimer -= dt;
		if (enemy.AttackTimer > 0f)
			return false;

		float reach = enemy.Radius + _type.FireRange;
		Vnp vnp = Fight.Vnp;
		bool found = false;
		Vector2 at = default, velocity = default;
		float best = reach;
		if (!vnp.IsDestroyed)
		{
			float distance = vnp.PlanePosition.DistanceTo(enemy.Position);
			if (distance <= best)
			{
				best = distance;
				at = vnp.PlanePosition;
				velocity = vnp.Velocity;
				found = true;
			}
		}
		if (Fight.Drones.FindNearest(enemy.Position, best, out Vector2 dronePosition, out Vector2 droneVelocity, out _))
		{
			at = dronePosition;
			velocity = droneVelocity;
			found = true;
		}
		if (!found)
			return false;

		Vector2 meeting = PlaneMath.AimAhead(enemy.Position, at, velocity, _type.ShotSpeed, _type.ShotLifetime);
		Vector2 aimAt = at.Lerp(meeting, _type.AimLead);
		Vector2 aim = aimAt.DistanceSquaredTo(enemy.Position) > 0.0001f ? (aimAt - enemy.Position).Normalized() : Vector2.Up;
		Fight.Projectiles.Fire(enemy.Position + aim * enemy.Radius, aim, enemy.AttackDamage, _type.ShotSpeed,
			_type.ShotLifetime, fromEnemy: true, _type.ShotColor, Source, _type.ShotSize);
		Fight.Sounds.Play(Sound.EnemyFire);
		enemy.AttackTimer = _type.FireCooldown * Rng.Range(0.85f, 1.15f);
		return false;
	}

	protected override ulong OwnTags => ThreatTags.Shoots;
}
