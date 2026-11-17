using Godot;
using System;

/// <summary>
/// Every enemy in the fight. Enemies are kept as plain data in one array and drawn in one
/// batch, so the count can be raised a long way before it costs much.
///
/// Behaviour: an enemy roams with its flock around its home point. When the VNP comes inside
/// its aggro range it chases, holds at a distance, circles and fires. When the VNP gets far
/// enough away it gives up and roams around wherever it ended up.
/// </summary>
public partial class EnemySwarm : Node3D
{
	private struct Enemy
	{
		public Vector2 Position;
		public Vector2 Velocity;
		public Vector2 Home;            // Point an idle enemy roams around.
		public Vector2 WanderDirection;
		public float WanderTimer;       // Seconds until it picks a new wander direction.
		public float Yaw;
		public float Health;
		public float ProjectileDamage;
		public float FireTimer;         // Seconds until it can fire again.
		public float OrbitSign;         // 1 or -1: which way it circles the VNP.
		public float Flash;             // Seconds left of the hit flash.
		public bool Aggro;
	}

	// Placeholder silhouette: two prongs forward, like a pincer. Forward is -y.
	private static readonly Vector2[] Outline =
	{
		new(-1.3f, -1.1f), new(0f, -0.15f), new(1.3f, -1.1f),
		new(0.85f, 0.9f), new(0f, 0.5f), new(-0.85f, 0.9f),
	};

	public Fight Fight;

	/// <summary>Enemies currently alive.</summary>
	public int Count { get; private set; }

	private Enemy[] _enemies = new Enemy[256];
	private readonly SpatialGrid _grid = new(Tuning.EnemyNeighborRadius);
	private readonly int[] _nearby = new int[64];
	private InstanceBatch _batch;
	private float _spawnTimer;
	private bool _startingGroupsSpawned;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Enemies;
		_batch = new InstanceBatch(this, "EnemyShapes", Placeholder.ExtrudedShape(Outline, 0.7f), Placeholder.InstanceMaterial());
		_spawnTimer = Tuning.EnemySpawnInterval;
	}

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		float dt = (float)delta;
		Vector2 vnpPosition = Fight.Vnp.PlanePosition;

		RemoveDeadAndDistant(vnpPosition);
		Spawn(dt, vnpPosition);
		Simulate(dt, vnpPosition, vnpAlive: !Fight.Vnp.IsDestroyed);
		Draw();
	}

	// ───────────── Used by weapons ─────────────

	public Vector2 PositionOf(int index) => _enemies[index].Position;

	/// <summary>The living enemy closest to a point, or -1 if none is within range.</summary>
	public int FindNearest(Vector2 from, float range)
	{
		int nearest = -1;
		float nearestSquared = range * range;
		for (int i = 0; i < Count; i++)
		{
			if (_enemies[i].Health <= 0f)
				continue;
			float distanceSquared = from.DistanceSquaredTo(_enemies[i].Position);
			if (distanceSquared <= nearestSquared)
			{
				nearestSquared = distanceSquared;
				nearest = i;
			}
		}
		return nearest;
	}

	/// <summary>Damages an enemy. If that destroys it, it counts as a kill and leaves debris.</summary>
	public void Damage(int index, float amount)
	{
		ref Enemy enemy = ref _enemies[index];
		if (enemy.Health <= 0f)
			return;

		enemy.Health -= amount;
		enemy.Flash = Tuning.HitFlashTime;
		enemy.Aggro = true; // Being shot gets its attention, even from outside its aggro range.
		if (enemy.Health <= 0f)
		{
			Fight.Kills++;
			Fight.Debris.Add(enemy.Position, Rng.Range(Tuning.DebrisMassMin, Tuning.DebrisMassMax));
			// The enemy itself is cleared away at the start of the next update.
		}
	}

	// ───────────── Each frame ─────────────

	private void RemoveDeadAndDistant(Vector2 vnpPosition)
	{
		float despawnSquared = Tuning.EnemyDespawnDistance * Tuning.EnemyDespawnDistance;
		for (int i = Count - 1; i >= 0; i--)
		{
			bool dead = _enemies[i].Health <= 0f;
			bool tooFar = _enemies[i].Position.DistanceSquaredTo(vnpPosition) > despawnSquared;
			if (dead || tooFar)
				_enemies[i] = _enemies[--Count]; // Fill the gap with the last enemy.
		}
	}

	private void Spawn(float dt, Vector2 vnpPosition)
	{
		if (!_startingGroupsSpawned)
		{
			_startingGroupsSpawned = true;
			for (int i = 0; i < Tuning.EnemyStartingGroups; i++)
				SpawnGroup(vnpPosition);
		}

		_spawnTimer -= dt;
		if (_spawnTimer > 0f)
			return;

		// The spawn rate climbs through the fight.
		_spawnTimer = Tuning.EnemySpawnInterval / (1f + Tuning.RampSpawnRatePerMinute * Fight.Minutes);
		if (Count + Tuning.EnemyGroupSize <= Tuning.EnemyMaxAlive)
			SpawnGroup(vnpPosition);
	}

	/// <summary>Spawns one group at a random spot off screen around the VNP. That spot is the group's home.</summary>
	private void SpawnGroup(Vector2 vnpPosition)
	{
		Vector2 home = vnpPosition + Rng.Direction() * Rng.Range(Tuning.EnemySpawnDistanceMin, Tuning.EnemySpawnDistanceMax);

		// Enemies spawned later in the fight are stronger, and keep that strength.
		float health = Tuning.EnemyHealth * (1f + Tuning.RampEnemyHealthPerMinute * Fight.Minutes);
		float damage = Tuning.EnemyProjectileDamage * (1f + Tuning.RampEnemyDamagePerMinute * Fight.Minutes);

		for (int i = 0; i < Tuning.EnemyGroupSize; i++)
		{
			if (Count == _enemies.Length)
				Array.Resize(ref _enemies, _enemies.Length * 2);

			Vector2 direction = Rng.Direction();
			_enemies[Count++] = new Enemy
			{
				Position = home + Rng.InsideCircle(4f),
				Home = home,
				WanderDirection = direction,
				WanderTimer = Rng.Range(1.5f, 4f),
				Yaw = PlaneMath.YawFacing(direction),
				Health = health,
				ProjectileDamage = damage,
				FireTimer = Rng.Range(0f, Tuning.EnemyFireCooldown), // So a group does not fire in unison.
				OrbitSign = Rng.Sign(),
			};
		}
	}

	private void Simulate(float dt, Vector2 vnpPosition, bool vnpAlive)
	{
		_grid.Clear();
		for (int i = 0; i < Count; i++)
			_grid.Insert(i, _enemies[i].Position);

		float neighborRadius = Tuning.EnemyNeighborRadius;
		float neighborRadiusSquared = neighborRadius * neighborRadius;
		float separationRadius = Tuning.EnemySeparationRadius;
		float turnStep = Mathf.DegToRad(Tuning.EnemyTurnSpeed) * dt;

		for (int i = 0; i < Count; i++)
		{
			ref Enemy enemy = ref _enemies[i];

			Vector2 toVnp = vnpPosition - enemy.Position;
			float distance = toVnp.Length();
			Vector2 directionToVnp = distance > 0.001f ? toVnp / distance : Vector2.Up;

			// Notice the VNP at the aggro range. Once chasing, keep chasing until it is well clear.
			bool aggro = vnpAlive && distance <= (enemy.Aggro ? Tuning.EnemyLoseAggroRange : Tuning.EnemyAggroRange);
			if (enemy.Aggro && !aggro)
				enemy.Home = enemy.Position; // Gave up: roam around here from now on.
			enemy.Aggro = aggro;

			// Where this enemy wants to go on its own.
			Vector2 goal;
			float speed;
			if (aggro)
			{
				speed = Tuning.EnemySpeed;
				if (distance > Tuning.EnemyHoldDistance)
				{
					goal = directionToVnp;
				}
				else
				{
					// Close enough: circle the VNP, backing off harder the closer it is.
					Vector2 around = new Vector2(-directionToVnp.Y, directionToVnp.X) * enemy.OrbitSign;
					float tooClose = 1f - distance / Tuning.EnemyHoldDistance;
					goal = (around - directionToVnp * tooClose * 2f).Normalized();
				}
			}
			else
			{
				speed = Tuning.EnemyWanderSpeed;
				enemy.WanderTimer -= dt;
				if (enemy.WanderTimer <= 0f)
				{
					enemy.WanderDirection = Rng.Direction();
					enemy.WanderTimer = Rng.Range(1.5f, 4f);
				}

				// Its own whim counts for less than the flock's pull, so idle groups travel together.
				Vector2 fromHome = enemy.Position - enemy.Home;
				goal = fromHome.Length() > Tuning.EnemyWanderRadius
					? -fromHome.Normalized()
					: enemy.WanderDirection * Tuning.EnemyWanderWeight;
			}

			// Flocking: push away from close flockmates, match their heading, drift toward their middle.
			Vector2 separation = Vector2.Zero;
			Vector2 flockVelocity = Vector2.Zero;
			Vector2 flockCenter = Vector2.Zero;
			int flockmates = 0;

			int nearbyCount = _grid.Query(enemy.Position, neighborRadius, _nearby);
			for (int n = 0; n < nearbyCount; n++)
			{
				int other = _nearby[n];
				if (other == i)
					continue;

				Vector2 away = enemy.Position - _enemies[other].Position;
				float gapSquared = away.LengthSquared();
				if (gapSquared > neighborRadiusSquared)
					continue;

				flockmates++;
				flockVelocity += _enemies[other].Velocity;
				flockCenter += _enemies[other].Position;

				float gap = Mathf.Sqrt(gapSquared);
				if (gap < separationRadius)
				{
					Vector2 awayDirection = gap > 0.001f ? away / gap : Rng.Direction();
					separation += awayDirection * (1f - gap / separationRadius);
				}
			}

			Vector2 steer = goal + separation * Tuning.EnemySeparation;
			if (flockmates > 0)
			{
				steer += (flockVelocity / flockmates / speed).LimitLength(1f) * Tuning.EnemyAlignment;
				steer += (flockCenter / flockmates - enemy.Position) / neighborRadius * Tuning.EnemyCohesion;
			}

			Vector2 wantedVelocity = steer.LimitLength(1f) * speed;
			enemy.Velocity = enemy.Velocity.MoveToward(wantedVelocity, Tuning.EnemyAcceleration * dt);
			enemy.Position += enemy.Velocity * dt;

			// Face the VNP while in firing range, otherwise face the direction of travel.
			bool inAttackRange = aggro && distance <= Tuning.EnemyAttackRange;
			if (inAttackRange)
				enemy.Yaw = Mathf.RotateToward(enemy.Yaw, PlaneMath.YawFacing(directionToVnp), turnStep);
			else if (enemy.Velocity.LengthSquared() > 0.25f)
				enemy.Yaw = Mathf.RotateToward(enemy.Yaw, PlaneMath.YawFacing(enemy.Velocity), turnStep);

			enemy.FireTimer = Mathf.Max(0f, enemy.FireTimer - dt);
			if (inAttackRange && enemy.FireTimer <= 0f)
			{
				Fight.Projectiles.Fire(enemy.Position, directionToVnp, enemy.ProjectileDamage);
				enemy.FireTimer = Tuning.EnemyFireCooldown * Rng.Range(0.85f, 1.15f);
			}

			enemy.Flash = Mathf.Max(0f, enemy.Flash - dt);
		}
	}

	private void Draw()
	{
		// Idle enemies are drawn darker, so it is easy to see which ones have noticed the VNP.
		Color chasing = Tuning.EnemyColor;
		Color idle = Tuning.EnemyColor.Darkened(0.45f);

		_batch.Begin(Count);
		for (int i = 0; i < Count; i++)
		{
			ref Enemy enemy = ref _enemies[i];
			Color color = enemy.Aggro ? chasing : idle;
			if (enemy.Flash > 0f)
				color = color.Lerp(Colors.White, enemy.Flash / Tuning.HitFlashTime);
			_batch.Set(i, enemy.Position, 0f, enemy.Yaw, Tuning.EnemySize, color);
		}
		_batch.Commit(Count);
	}
}
