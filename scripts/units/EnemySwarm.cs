using Godot;
using System;

/// <summary>
/// Every enemy in the fight. Enemies are kept as plain data in one array and drawn in one
/// batch, so the count can be raised a long way before it costs much.
///
/// Behaviour: an enemy roams with its flock around its home point. When the VNP or a drone
/// comes inside its aggro range it chases whichever is nearest, holds at a distance, circles
/// and fires. When everything gets far enough away it gives up and roams around wherever it
/// ended up.
///
/// Waves: every so often several groups arrive at once from all around the VNP, already hunting
/// it. They chase from any distance and do not give up.
/// </summary>
public partial class EnemySwarm : Node3D
{
	private struct Enemy
	{
		public int Id;                  // Stays the same for its whole life, unlike its place in the array.
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
		public bool Hunting;            // Part of a wave: chases from any distance and never gives up.
	}

	/// <summary>The enemy type that spawns, from enemy_spawning.tres.</summary>
	private static EnemyDefinition Type => Tuning.EnemySpawning.Enemy;

	public Fight Fight;

	/// <summary>Enemies currently alive.</summary>
	public int Count { get; private set; }

	/// <summary>Waves sent so far.</summary>
	public int WavesSent { get; private set; }

	/// <summary>Fight time, in seconds, when the next wave arrives.</summary>
	public float NextWaveTime => Tuning.EnemySpawning.WaveFirstSeconds + WavesSent * Tuning.EnemySpawning.WaveIntervalSeconds;

	private Enemy[] _enemies = new Enemy[256];
	private readonly SpatialGrid _grid = new(Type.NeighborRadius);
	private readonly int[] _nearby = new int[64];
	private readonly int[] _hitCandidates = new int[64];
	private InstanceBatch _batch;

	// Extra search distance for lookups made after enemies have moved this frame.
	private const float GridSlack = 1f;
	private float _spawnTimer;
	private bool _startingGroupsSpawned;
	private int _lastId;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Enemies;
		_batch = new InstanceBatch(this, "EnemyShapes", Placeholders.Mesh("enemy"), Placeholders.Material("instanced_lit"));
		_spawnTimer = Tuning.EnemySpawning.SpawnInterval;
	}

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		float dt = (float)delta;
		Vector2 vnpPosition = Fight.Vnp.PlanePosition;

		RemoveDeadAndDistant(vnpPosition);
		Spawn(dt, vnpPosition);
		Simulate(dt, vnpPosition, Fight.Vnp.Velocity, vnpAlive: !Fight.Vnp.IsDestroyed);
		Draw();
	}

	// ───────────── Used by weapons ─────────────

	public Vector2 PositionOf(int index) => _enemies[index].Position;

	public Vector2 VelocityOf(int index) => _enemies[index].Velocity;

	/// <summary>A number that identifies an enemy for its whole life. Use it to remember a target between frames.</summary>
	public int IdOf(int index) => _enemies[index].Id;

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

	/// <summary>
	/// Fills <paramref name="results"/> with the living enemies within a distance of a point
	/// and returns how many there are. Stops when <paramref name="results"/> is full.
	/// </summary>
	public int CollectNear(Vector2 center, float radius, int[] results)
	{
		// The grid is filled at the start of the enemy update, so search a little wide to
		// allow for how far enemies have moved since.
		int candidates = _grid.Query(center, radius + GridSlack, results);
		float radiusSquared = radius * radius;
		int kept = 0;
		for (int c = 0; c < candidates; c++)
		{
			int i = results[c];
			if (_enemies[i].Health > 0f && _enemies[i].Position.DistanceSquaredTo(center) <= radiusSquared)
				results[kept++] = i;
		}
		return kept;
	}

	/// <summary>The first living enemy a shot travelling between two points would hit, or -1.</summary>
	public int FindHit(Vector2 from, Vector2 to)
	{
		float reach = from.DistanceTo(to) * 0.5f + Type.HitRadius + GridSlack;
		int candidates = _grid.Query((from + to) * 0.5f, reach, _hitCandidates);

		int hit = -1;
		float hitDistanceSquared = float.MaxValue;
		for (int c = 0; c < candidates; c++)
		{
			int i = _hitCandidates[c];
			if (_enemies[i].Health <= 0f)
				continue;
			if (PlaneMath.DistanceToSegment(_enemies[i].Position, from, to) > Type.HitRadius)
				continue;

			float distanceSquared = from.DistanceSquaredTo(_enemies[i].Position);
			if (distanceSquared < hitDistanceSquared)
			{
				hitDistanceSquared = distanceSquared;
				hit = i;
			}
		}
		return hit;
	}

	/// <summary>Damages an enemy. If that destroys it, it counts as a kill and leaves debris.</summary>
	public void Damage(int index, float amount)
	{
		ref Enemy enemy = ref _enemies[index];
		if (enemy.Health <= 0f)
			return;

		enemy.Health -= amount;
		enemy.Flash = Tuning.Look.HitFlashTime;
		enemy.Aggro = true; // Being shot gets its attention, even from outside its aggro range.
		if (enemy.Health <= 0f)
		{
			Fight.Kills++;
			Fight.Harvestables.AddDebris(enemy.Position, Rng.Range(Type.DebrisMassMin, Type.DebrisMassMax));
			// The enemy itself is cleared away at the start of the next update.
		}
	}

	// ───────────── Each frame ─────────────

	private void RemoveDeadAndDistant(Vector2 vnpPosition)
	{
		float despawnSquared = Tuning.EnemySpawning.DespawnDistance * Tuning.EnemySpawning.DespawnDistance;
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
			for (int i = 0; i < Tuning.EnemySpawning.StartingGroups; i++)
				SpawnGroup(vnpPosition);
		}

		if (Fight.Elapsed >= NextWaveTime)
			SpawnWave(vnpPosition);

		_spawnTimer -= dt;
		if (_spawnTimer > 0f)
			return;

		// The spawn rate climbs through the fight.
		_spawnTimer = Tuning.EnemySpawning.SpawnInterval / Fight.EnemySpawnRateScale;
		if (Count + Tuning.EnemySpawning.GroupSize <= Tuning.EnemySpawning.MaxAlive)
			SpawnGroup(vnpPosition);
	}

	/// <summary>
	/// Spawns a wave: groups spaced evenly on a ring just off screen, all hunting the VNP.
	/// Each wave has more groups than the last. Waves ignore the MaxAlive limit.
	/// </summary>
	private void SpawnWave(Vector2 vnpPosition)
	{
		int groups = Tuning.EnemySpawning.WaveGroups + Tuning.EnemySpawning.WaveGroupsAddedPerWave * WavesSent;
		WavesSent++;
		float startAngle = Rng.Range(0f, Mathf.Tau);
		for (int g = 0; g < groups; g++)
		{
			float angle = startAngle + Mathf.Tau * g / groups;
			Vector2 direction = Vector2.FromAngle(angle);
			Vector2 home = vnpPosition + direction * OffScreenDistance(vnpPosition, direction, 0f);
			SpawnGroupAt(home, hunting: true);
		}
	}

	/// <summary>Spawns one group at a random spot off screen around the VNP. That spot is the group's home.</summary>
	private void SpawnGroup(Vector2 vnpPosition)
	{
		Vector2 direction = Rng.Direction();
		Vector2 home = vnpPosition + direction * OffScreenDistance(vnpPosition, direction, Rng.Range(0f, Tuning.EnemySpawning.SpawnExtraDistance));
		SpawnGroupAt(home, hunting: false);
	}

	/// <summary>How far from the VNP a group must spawn in a direction to be out of sight:
	/// past the edge of the screen by the spawn margin, plus <paramref name="extra"/>.</summary>
	private float OffScreenDistance(Vector2 vnpPosition, Vector2 direction, float extra)
	{
		return Fight.Camera.DistanceOutOfView(vnpPosition, direction, Tuning.EnemySpawning.SpawnMargin) + extra;
	}

	private void SpawnGroupAt(Vector2 home, bool hunting)
	{
		// Enemies spawned later in the fight are stronger, and keep that strength.
		float health = Type.Health * Fight.EnemyHealthScale;
		float damage = Type.ProjectileDamage * Fight.EnemyDamageScale;

		for (int i = 0; i < Tuning.EnemySpawning.GroupSize; i++)
		{
			if (Count == _enemies.Length)
				Array.Resize(ref _enemies, _enemies.Length * 2);

			Vector2 direction = Rng.Direction();
			_enemies[Count++] = new Enemy
			{
				Id = ++_lastId,
				Position = home + Rng.InsideCircle(4f),
				Home = home,
				WanderDirection = direction,
				WanderTimer = Rng.Range(1.5f, 4f),
				Yaw = PlaneMath.YawFacing(direction),
				Health = health,
				ProjectileDamage = damage,
				FireTimer = Rng.Range(0f, Type.FireCooldown), // So a group does not fire in unison.
				OrbitSign = Rng.Sign(),
				Aggro = hunting,
				Hunting = hunting,
			};
		}
	}

	private void Simulate(float dt, Vector2 vnpPosition, Vector2 vnpVelocity, bool vnpAlive)
	{
		_grid.Clear();
		for (int i = 0; i < Count; i++)
			_grid.Insert(i, _enemies[i].Position);

		float neighborRadius = Type.NeighborRadius;
		float neighborRadiusSquared = neighborRadius * neighborRadius;
		float separationRadius = Type.SeparationRadius;
		float turnStep = Mathf.DegToRad(Type.TurnSpeed) * dt;
		float idleTurnStep = Mathf.DegToRad(Type.IdleTurnSpeed) * dt;
		DroneSwarm drones = Fight.Drones;

		for (int i = 0; i < Count; i++)
		{
			ref Enemy enemy = ref _enemies[i];

			// Notice things at the aggro range. Once chasing, keep chasing until everything is well clear.
			// Wave enemies hunt from any distance.
			float noticeRange = enemy.Hunting
				? float.MaxValue
				: (enemy.Aggro ? Type.LoseAggroRange : Type.AggroRange) * Fight.EnemyAggroScale;

			// Go after whichever is nearest: the VNP or one of its drones.
			float vnpDistance = enemy.Position.DistanceTo(vnpPosition);
			Vector2 targetPosition = vnpPosition;
			Vector2 targetVelocity = vnpVelocity;
			float distance = vnpAlive ? vnpDistance : float.MaxValue;
			// Skip the drone search when even the farthest drone could not be near enough.
			if (vnpDistance - drones.Reach <= noticeRange
				&& drones.FindNearest(enemy.Position, Mathf.Min(distance, noticeRange),
					out Vector2 dronePosition, out Vector2 droneVelocity, out float droneDistance))
			{
				targetPosition = dronePosition;
				targetVelocity = droneVelocity;
				distance = droneDistance;
			}

			Vector2 toTarget = targetPosition - enemy.Position;
			Vector2 directionToTarget = distance > 0.001f ? toTarget / distance : Vector2.Up;

			bool aggro = distance <= noticeRange;
			if (enemy.Aggro && !aggro)
				enemy.Home = enemy.Position; // Gave up: roam around here from now on.
			enemy.Aggro = aggro;

			// Where this enemy wants to go on its own.
			Vector2 goal;
			float speed;
			if (aggro)
			{
				speed = Type.Speed;
				if (distance > Type.HoldDistance)
				{
					goal = directionToTarget;
				}
				else
				{
					// Close enough: stop closing in and circle the target at half speed.
					goal = new Vector2(-directionToTarget.Y, directionToTarget.X) * enemy.OrbitSign * 0.5f;
				}
			}
			else
			{
				speed = Type.WanderSpeed;
				enemy.WanderTimer -= dt;
				if (enemy.WanderTimer <= 0f)
				{
					enemy.WanderDirection = Rng.Direction();
					enemy.WanderTimer = Rng.Range(1.5f, 4f);
				}

				// Its own whim counts for less than the flock's pull, so idle groups travel together.
				// The pull back toward home builds up gradually past half the wander radius, rather than
				// switching on at the edge, which made enemies sitting on the edge flip back and forth.
				Vector2 fromHome = enemy.Position - enemy.Home;
				float homeDistance = fromHome.Length();
				float halfRadius = Type.WanderRadius * 0.5f;
				float homePull = Mathf.Clamp((homeDistance - halfRadius) / halfRadius, 0f, 1.5f);
				goal = enemy.WanderDirection * Type.WanderWeight;
				if (homeDistance > 0.001f)
					goal -= fromHome / homeDistance * homePull;
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

			Vector2 steer = goal + separation * Type.Separation;
			if (flockmates > 0)
			{
				steer += (flockVelocity / flockmates / speed).LimitLength(1f) * Type.Alignment;
				steer += (flockCenter / flockmates - enemy.Position) / neighborRadius * Type.Cohesion;
			}

			Vector2 wantedVelocity = steer.LimitLength(1f) * speed;
			enemy.Velocity = enemy.Velocity.MoveToward(wantedVelocity, Type.Acceleration * dt);
			enemy.Position += enemy.Velocity * dt;

			// Face the target while in firing range, otherwise face the direction of travel.
			bool inAttackRange = aggro && distance <= Type.AttackRange;
			if (inAttackRange)
				enemy.Yaw = Mathf.RotateToward(enemy.Yaw, PlaneMath.YawFacing(directionToTarget), turnStep);
			else if (aggro && enemy.Velocity.LengthSquared() > 0.25f)
				enemy.Yaw = Mathf.RotateToward(enemy.Yaw, PlaneMath.YawFacing(enemy.Velocity), turnStep);
			else if (!aggro && enemy.Velocity.Length() > Type.WanderSpeed * 0.5f)
			{
				// Idle: turn slowly, and ignore small changes of course, so nudges from flockmates
				// do not show as twitching.
				float heading = PlaneMath.YawFacing(enemy.Velocity);
				if (Mathf.Abs(Mathf.AngleDifference(enemy.Yaw, heading)) > Mathf.DegToRad(Type.IdleTurnDeadband))
					enemy.Yaw = Mathf.RotateToward(enemy.Yaw, heading, idleTurnStep);
			}

			enemy.FireTimer = Mathf.Max(0f, enemy.FireTimer - dt);
			if (inAttackRange && enemy.FireTimer <= 0f)
			{
				// Aim ahead of a moving target. EnemyAimLead scales how far ahead: 1 aims exactly
				// where the target will be, 0 aims at where it is now.
				Vector2 meetingPoint = PlaneMath.AimAhead(enemy.Position, targetPosition, targetVelocity,
					Type.ProjectileSpeed, Type.ProjectileLifetime);
				Vector2 aimAt = targetPosition.Lerp(meetingPoint, Type.AimLead);
				Vector2 aim = aimAt.DistanceSquaredTo(enemy.Position) > 0.0001f
					? (aimAt - enemy.Position).Normalized()
					: directionToTarget;

				Fight.Projectiles.Fire(enemy.Position, aim, enemy.ProjectileDamage,
					Type.ProjectileSpeed, Type.ProjectileLifetime, fromEnemy: true, Type.ProjectileColor);
				enemy.FireTimer = Type.FireCooldown * Rng.Range(0.85f, 1.15f);
			}

			enemy.Flash = Mathf.Max(0f, enemy.Flash - dt);
		}
	}

	private void Draw()
	{
		// Idle enemies are drawn darker, so it is easy to see which ones have noticed the VNP.
		Color chasing = Type.Color;
		Color idle = Type.Color.Darkened(0.45f);

		_batch.Begin(Count);
		for (int i = 0; i < Count; i++)
		{
			ref Enemy enemy = ref _enemies[i];
			Color color = enemy.Aggro ? chasing : idle;
			if (enemy.Flash > 0f)
				color = color.Lerp(Colors.White, enemy.Flash / Tuning.Look.HitFlashTime);
			_batch.Set(i, enemy.Position, 0f, enemy.Yaw, Type.Size, color);
		}
		_batch.Commit(Count);
	}
}
