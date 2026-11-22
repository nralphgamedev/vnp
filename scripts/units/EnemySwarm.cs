using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Every enemy in the fight, of every type. Enemies are kept as plain data in one array and each type is
/// drawn in one batch, so the count can be raised a long way before it costs much.
///
/// What every enemy shares is done here: an enemy roams with its flock around its home point. When the
/// VNP or a drone comes inside its aggro range it chases it. When everything gets far enough away it gives
/// up and roams around wherever it ended up. How it closes in and attacks belongs to its type's behavior
/// (see EnemyBehavior.cs): the basic enemy holds at a distance, circles and fires, and the Goo absorbs
/// drones and splits.
///
/// Waves: every so often several groups arrive at once from all around the VNP, already hunting it. They
/// chase from any distance and do not give up.
/// </summary>
public partial class EnemySwarm : Node3D
{
	/// <summary>One enemy. Public so the enemy types' behaviors in res://enemies/ can work on it.</summary>
	public struct Enemy
	{
		public int Id;                  // Stays the same for its whole life, unlike its place in the array.
		public int Type;                // Which enemy type, as an index into the swarm's list of types.
		public Vector2 Position;
		public Vector2 Velocity;
		public Vector2 Home;            // Point an idle enemy roams around.
		public Vector2 WanderDirection;
		public float WanderTimer;       // Seconds until it picks a new wander direction.
		public float Yaw;
		public float Health;
		public float Radius;            // How far from its center it can be hit, and touches things.
		public float Scale;             // Size of its model.
		public float Damage;            // Damage of its attack, at the strength it spawned with.
		public float Drain;             // Health per second it drains from a drone touching it (the Goo).
		public float FireTimer;         // Seconds until it can attack again.
		public float OrbitSign;         // 1 or -1: which way it circles or turns.
		public float Flash;             // Seconds left of the hit flash.
		public bool Aggro;
		public bool Hunting;            // Part of a wave: chases from any distance and never gives up.
		public int TargetDroneId;       // The drone it is chasing, or 0 for the VNP.
		public float TargetTimer;       // Seconds until it looks again for a target.
		public Vector2 TrackDirection;  // Its sense of where its target is, turning at its turn rate.
		public float TrackTurnSign;     // Which way it last turned to follow its target: 1, -1, or 0.

		// Used by the basic enemy.
		public float Accuracy;          // From 0 (sloppy) to 1 (sharpshooter), rolled when it spawns.

		// Used by the Goo.
		public float BaseHealth;        // Health it spawned with. Growth is measured against it.
		public float Bulk;              // 1 as it spawns. Absorbed drones add to it, and a split halves it.
		public float Absorbed;          // Drones it has absorbed, shared out between halves when it splits.
		public int AbsorbedSinceSplit;  // Drones absorbed since it spawned or last split.
		public float Pulse;             // Seconds left of the swell after absorbing a drone.
		public float SplitTimer;        // Seconds left of bulging before it splits, or 0.
		public float SplitAngle;        // The line it will split along, as an angle on the plane.
		public float Phase;             // Where it is in its wobble.
	}

	/// <summary>What a chasing enemy is after this frame, for its behavior to attack.</summary>
	public struct Target
	{
		public Vector2 Position;
		public Vector2 Velocity;
		public float Distance;
		public Vector2 Direction;  // Straight toward it.
		public Vector2 Track;      // The enemy's sense of where it is, which turns at the enemy's turn rate.
	}

	public Fight Fight;

	/// <summary>Enemies currently alive.</summary>
	public int Count { get; private set; }

	/// <summary>Waves sent so far.</summary>
	public int WavesSent { get; private set; }

	/// <summary>Fight time, in seconds, when the next wave arrives.</summary>
	public float NextWaveTime => Tuning.EnemySpawning.WaveFirstSeconds + WavesSent * Tuning.EnemySpawning.WaveIntervalSeconds;

	/// <summary>The radius of the largest enemy alive, as of the start of this frame's enemy update.</summary>
	public float MaxRadius { get; private set; }

	/// <summary>The largest DroneAvoidDistance of any enemy type, so drones know how far to look.</summary>
	public float MaxDroneAvoid { get; private set; }

	/// <summary>Every enemy type that can spawn, from the weights in enemy_spawning.tres.</summary>
	public IReadOnlyList<EnemyBehavior> Types => _types;

	/// <summary>Debris waiting to appear while its enemy bursts apart.</summary>
	private struct PendingDebris
	{
		public Vector2 Position;
		public float Mass;
		public float Delay;
	}

	private Enemy[] _enemies = new Enemy[256];
	private PendingDebris[] _pendingDebris = new PendingDebris[32];
	private int _pendingDebrisCount;
	private readonly List<EnemyBehavior> _types = new();
	private readonly Dictionary<EnemyDefinition, int> _typeIndex = new();
	private SpatialGrid _grid;
	private readonly int[] _nearby = new int[64];
	private readonly int[] _hitCandidates = new int[64];
	private readonly List<int> _splits = new();
	private int[] _drawCounts = Array.Empty<int>();

	// Extra search distance for lookups made after enemies have moved this frame.
	private const float GridSlack = 1f;
	private float _spawnTimer;
	private bool _startingGroupsSpawned;
	private int _lastId;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Enemies;
		_spawnTimer = Tuning.EnemySpawning.SpawnInterval;

		AddTypes(Tuning.EnemySpawning.GroupWeights);
		AddTypes(Tuning.EnemySpawning.WaveGroupWeights);
		if (_types.Count == 0)
			GD.PushError("No enemy types to spawn. Give at least one a weight in res://tuning/enemy_spawning.tres.");

		float cellSize = 1f;
		foreach (EnemyBehavior type in _types)
		{
			type.Batch = new InstanceBatch(this, $"{type.Definition.Id}_shapes", type.Mesh, type.Material);
			cellSize = Mathf.Max(cellSize, type.Definition.NeighborRadius);
		}
		_grid = new SpatialGrid(cellSize);
		_drawCounts = new int[_types.Count];
	}

	private void AddTypes(Godot.Collections.Dictionary<EnemyDefinition, float> weights)
	{
		if (weights == null)
			return;
		foreach (EnemyDefinition definition in weights.Keys)
		{
			if (definition == null || _typeIndex.ContainsKey(definition))
				continue;
			EnemyBehavior behavior = EnemyBehavior.Create(this, definition);
			if (behavior == null)
			{
				GD.PushError($"Enemy type {definition.Id} has no behavior. Its file must use one of the enemy definition classes.");
				continue;
			}
			_typeIndex[definition] = _types.Count;
			_types.Add(behavior);
		}
	}

	public override void _Process(double delta)
	{
		if (Fight == null || _types.Count == 0)
			return;

		float dt = (float)delta;
		Vector2 vnpPosition = Fight.Vnp.PlanePosition;

		RemoveDeadAndDistant(vnpPosition);
		DropPendingDebris(dt);
		Spawn(dt, vnpPosition);
		Simulate(dt, vnpPosition, Fight.Vnp.Velocity, vnpAlive: !Fight.Vnp.IsDestroyed);
		SplitQueued();
		Draw();
	}

	// ───────────── Used by weapons and drones ─────────────

	public Vector2 PositionOf(int index) => _enemies[index].Position;

	public Vector2 VelocityOf(int index) => _enemies[index].Velocity;

	/// <summary>How far from its center an enemy can be hit. Goos grow, so this changes.</summary>
	public float RadiusOf(int index) => _enemies[index].Radius;

	/// <summary>How much farther back than usual drones stay from this enemy's edge.</summary>
	public float DroneKeepBackOf(int index) => _types[_enemies[index].Type].Definition.DroneKeepBack;

	/// <summary>A number that identifies an enemy for its whole life. Use it to remember a target between frames.</summary>
	public int IdOf(int index) => _enemies[index].Id;

	/// <summary>Where the living enemy with this ID is in the list right now, or -1 if it is gone.</summary>
	public int IndexOfId(int id)
	{
		for (int i = 0; i < Count; i++)
		{
			if (_enemies[i].Id == id && _enemies[i].Health > 0f)
				return i;
		}
		return -1;
	}

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
	/// Fills <paramref name="results"/> with the living enemies whose centers are within a distance of a
	/// point and returns how many there are. Stops when <paramref name="results"/> is full.
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

	/// <summary>
	/// Fills <paramref name="results"/> with the living enemies that a circle around a point reaches,
	/// counting each enemy's own size, and returns how many there are. For weapons that hit an area.
	/// </summary>
	public int CollectTouching(Vector2 center, float radius, int[] results)
	{
		int candidates = _grid.Query(center, radius + MaxRadius + GridSlack, results);
		int kept = 0;
		for (int c = 0; c < candidates; c++)
		{
			int i = results[c];
			if (_enemies[i].Health <= 0f)
				continue;
			float reach = radius + _enemies[i].Radius;
			if (_enemies[i].Position.DistanceSquaredTo(center) <= reach * reach)
				results[kept++] = i;
		}
		return kept;
	}

	/// <summary>The first living enemy a shot travelling between two points would hit, or -1.</summary>
	public int FindHit(Vector2 from, Vector2 to)
	{
		float reach = from.DistanceTo(to) * 0.5f + MaxRadius + GridSlack;
		int candidates = _grid.Query((from + to) * 0.5f, reach, _hitCandidates);

		int hit = -1;
		float hitDistanceSquared = float.MaxValue;
		for (int c = 0; c < candidates; c++)
		{
			int i = _hitCandidates[c];
			if (_enemies[i].Health <= 0f)
				continue;
			if (PlaneMath.DistanceToSegment(_enemies[i].Position, from, to) > _enemies[i].Radius)
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

	/// <summary>A living enemy that absorbs drones and is touching a drone at this point, or -1.
	/// If several are, the first found.</summary>
	public int FindAbsorber(Vector2 position, float droneRadius)
	{
		int candidates = _grid.Query(position, droneRadius + MaxRadius + GridSlack, _hitCandidates);
		for (int c = 0; c < candidates; c++)
		{
			int i = _hitCandidates[c];
			ref Enemy enemy = ref _enemies[i];
			if (enemy.Health <= 0f || !_types[enemy.Type].AbsorbsDrones)
				continue;
			float reach = enemy.Radius + droneRadius;
			if (enemy.Position.DistanceSquaredTo(position) <= reach * reach)
				return i;
		}
		return -1;
	}

	/// <summary>How far from this enemy's edge drones that aren't attacking it steer clear.</summary>
	public float DroneAvoidOf(int index) => _types[_enemies[index].Type].Definition.DroneAvoidDistance;

	/// <summary>
	/// Whether material at a point, of a given radius, lies where drones keep clear of an enemy that absorbs
	/// drones: within that enemy's DroneAvoidDistance of its edge. Drones don't go in to harvest it.
	/// </summary>
	public bool NearDroneEater(Vector2 position, float radius)
	{
		int candidates = _grid.Query(position, radius + MaxRadius + MaxDroneAvoid + GridSlack, _hitCandidates);
		for (int c = 0; c < candidates; c++)
		{
			int i = _hitCandidates[c];
			ref Enemy enemy = ref _enemies[i];
			EnemyBehavior type = _types[enemy.Type];
			if (enemy.Health <= 0f || !type.AbsorbsDrones)
				continue;
			float reach = enemy.Radius + radius + type.Definition.DroneAvoidDistance;
			if (enemy.Position.DistanceSquaredTo(position) <= reach * reach)
				return true;
		}
		return false;
	}

	/// <summary>Whether an enemy damages the VNP by touching it, as the Goo does.</summary>
	public bool DamagesByTouchOf(int index) => _types[_enemies[index].Type].DamagesByTouch;

	/// <summary>Whether an enemy absorbs drones that touch it, as the Goo does.</summary>
	public bool AbsorbsDronesOf(int index) => _types[_enemies[index].Type].AbsorbsDrones;

	/// <summary>Which enemy type an enemy is, by the type's ID.</summary>
	public string TypeIdOf(int index) => _types[_enemies[index].Type].Definition.Id;

	/// <summary>An enemy's definition, for testing tools such as the run review's maps. Read it, don't change it.</summary>
	public EnemyDefinition DefinitionOf(int index) => _types[_enemies[index].Type].Definition;

	/// <summary>How many living enemies there are of each type, in the order of Types.</summary>
	public int[] CountByType()
	{
		var counts = new int[_types.Count];
		for (int i = 0; i < Count; i++)
		{
			if (_enemies[i].Health > 0f)
				counts[_enemies[i].Type]++;
		}
		return counts;
	}

	/// <summary>Health per second this enemy drains from a drone touching it.</summary>
	public float DroneDrainOf(int index) => _types[_enemies[index].Type].DroneDrain(_enemies[index]);

	/// <summary>A drone has been drained to nothing and absorbed by this enemy.</summary>
	public void Absorb(int index)
	{
		ref Enemy enemy = ref _enemies[index];
		if (enemy.Health > 0f)
			_types[enemy.Type].Absorb(ref enemy);
	}

	/// <summary>Finds a living enemy by its ID. False if it is gone.</summary>
	public bool TryGetPosition(int id, out Vector2 position)
	{
		int index = IndexOfId(id);
		position = index >= 0 ? _enemies[index].Position : default;
		return index >= 0;
	}

	/// <summary>
	/// Damages an enemy. If that destroys it, it counts as a kill and leaves debris. <paramref name="source"/>
	/// is what dealt it, for fight statistics, which count only the health it took off.
	/// </summary>
	public void Damage(int index, float amount, DamageSource source)
	{
		ref Enemy enemy = ref _enemies[index];
		if (enemy.Health <= 0f)
			return;

		EnemyBehavior type = _types[enemy.Type];
		Fight.Stats.EnemyDamaged(source, type.Definition.Id, Mathf.Min(amount, enemy.Health));
		enemy.Health -= amount;
		enemy.Flash = Tuning.Feedback.HitFlashTime;
		enemy.Aggro = true; // Being shot gets its attention, even from outside its aggro range.
		Fight.DamageNumbers.Add(enemy.Id, enemy.Position, amount);
		Fight.Sounds.Play(Sound.Hit);
		if (enemy.Health <= 0f)
		{
			Fight.Kills++;
			Fight.Stats.EnemyKilled(Fight.Elapsed, type.Definition.Id);
			// It bursts apart, and its debris appears a moment later.
			type.Died(enemy);
			AddPendingDebris(enemy.Position, type.DebrisMass(enemy));
			// The enemy itself is cleared away at the start of the next update.
		}
	}

	/// <summary>Splits the enemy at this index after this frame's update. Used by the Goo.</summary>
	public void QueueSplit(int index) => _splits.Add(index);

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

	private void AddPendingDebris(Vector2 position, float mass)
	{
		if (Tuning.Feedback.DebrisDelay <= 0f)
		{
			Fight.Harvestables.AddDebris(position, mass);
			return;
		}
		if (_pendingDebrisCount == _pendingDebris.Length)
			Array.Resize(ref _pendingDebris, _pendingDebris.Length * 2);
		_pendingDebris[_pendingDebrisCount++] = new PendingDebris
		{
			Position = position,
			Mass = mass,
			Delay = Tuning.Feedback.DebrisDelay,
		};
	}

	private void DropPendingDebris(float dt)
	{
		for (int i = _pendingDebrisCount - 1; i >= 0; i--)
		{
			_pendingDebris[i].Delay -= dt;
			if (_pendingDebris[i].Delay > 0f)
				continue;
			Fight.Harvestables.AddDebris(_pendingDebris[i].Position, _pendingDebris[i].Mass);
			_pendingDebris[i] = _pendingDebris[--_pendingDebrisCount];
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
		SpawnGroup(vnpPosition, respectMaxAlive: true);
	}

	/// <summary>An enemy type picked at random, by the weights given, or -1 if none has a weight.</summary>
	private int PickType(Godot.Collections.Dictionary<EnemyDefinition, float> weights)
	{
		if (weights == null)
			return -1;
		float total = 0f;
		foreach ((EnemyDefinition definition, float weight) in weights)
		{
			if (definition != null && weight > 0f && _typeIndex.ContainsKey(definition))
				total += weight;
		}
		if (total <= 0f)
			return -1;

		float roll = Rng.Range(0f, total);
		int picked = -1;
		foreach ((EnemyDefinition definition, float weight) in weights)
		{
			if (definition == null || weight <= 0f || !_typeIndex.TryGetValue(definition, out int index))
				continue;
			picked = index;
			roll -= weight;
			if (roll <= 0f)
				break;
		}
		return picked;
	}

	/// <summary>
	/// Spawns a wave: groups spaced evenly on a ring just off screen, all hunting the VNP. Each group's type
	/// is picked by the wave weights. Each wave has more groups than the last. Waves ignore the MaxAlive limit.
	/// </summary>
	private void SpawnWave(Vector2 vnpPosition)
	{
		int groups = Tuning.EnemySpawning.WaveGroups + Tuning.EnemySpawning.WaveGroupsAddedPerWave * WavesSent;
		WavesSent++;
		Fight.Camera.Shake(Tuning.Feedback.ShakeOnWave);
		float startAngle = Rng.Range(0f, Mathf.Tau);
		for (int g = 0; g < groups; g++)
		{
			int type = PickType(Tuning.EnemySpawning.WaveGroupWeights);
			if (type < 0)
				return;
			float angle = startAngle + Mathf.Tau * g / groups;
			Vector2 direction = Vector2.FromAngle(angle);
			Vector2 home = vnpPosition + direction * OffScreenDistance(vnpPosition, direction, 0f);
			SpawnGroupAt(home, type, hunting: true);
		}
	}

	/// <summary>Spawns one group, of a type picked by the group weights, at a random spot off screen around
	/// the VNP. That spot is the group's home.</summary>
	private void SpawnGroup(Vector2 vnpPosition, bool respectMaxAlive = false)
	{
		int type = PickType(Tuning.EnemySpawning.GroupWeights);
		if (type < 0)
			return;
		if (respectMaxAlive && Count + _types[type].Definition.GroupSize > Tuning.EnemySpawning.MaxAlive)
			return;
		Vector2 direction = Rng.Direction();
		Vector2 home = vnpPosition + direction * OffScreenDistance(vnpPosition, direction, Rng.Range(0f, Tuning.EnemySpawning.SpawnExtraDistance));
		SpawnGroupAt(home, type, hunting: false);
	}

	/// <summary>How far from the VNP a group must spawn in a direction to be out of sight:
	/// past the edge of the screen by the spawn margin, plus <paramref name="extra"/>.</summary>
	private float OffScreenDistance(Vector2 vnpPosition, Vector2 direction, float extra)
	{
		return Fight.Camera.DistanceOutOfView(vnpPosition, direction, Tuning.EnemySpawning.SpawnMargin) + extra;
	}

	private void SpawnGroupAt(Vector2 home, int type, bool hunting)
	{
		EnemyBehavior behavior = _types[type];
		for (int i = 0; i < behavior.Definition.GroupSize; i++)
		{
			Vector2 direction = Rng.Direction();
			var enemy = new Enemy
			{
				Id = ++_lastId,
				Type = type,
				Position = home + Rng.InsideCircle(4f),
				Home = home,
				WanderDirection = direction,
				WanderTimer = Rng.Range(1.5f, 4f),
				Yaw = PlaneMath.YawFacing(direction),
				TrackDirection = direction,
				OrbitSign = Rng.Sign(),
				Aggro = hunting,
				Hunting = hunting,
			};
			behavior.Spawned(ref enemy);
			Append(enemy);
		}
	}

	private void Append(in Enemy enemy)
	{
		if (Count == _enemies.Length)
			Array.Resize(ref _enemies, _enemies.Length * 2);
		_enemies[Count++] = enemy;
	}

	/// <summary>Splits the enemies that finished bulging this frame. Each split adds one enemy.</summary>
	private void SplitQueued()
	{
		foreach (int index in _splits)
		{
			if (index >= Count || _enemies[index].Health <= 0f || _types[_enemies[index].Type] is not Goo goo)
				continue;
			Enemy other = default;
			goo.Split(ref _enemies[index], ref other);
			other.Id = ++_lastId;
			Append(other);
		}
		_splits.Clear();
	}

	private void Simulate(float dt, Vector2 vnpPosition, Vector2 vnpVelocity, bool vnpAlive)
	{
		_grid.Clear();
		float maxRadius = 0f;
		for (int i = 0; i < Count; i++)
		{
			_grid.Insert(i, _enemies[i].Position);
			maxRadius = Mathf.Max(maxRadius, _enemies[i].Radius);
		}
		MaxRadius = maxRadius;
		float maxAvoid = 0f;
		foreach (EnemyBehavior type in _types)
			maxAvoid = Mathf.Max(maxAvoid, type.Definition.DroneAvoidDistance);
		MaxDroneAvoid = maxAvoid;

		DroneSwarm drones = Fight.Drones;
		// Only the enemies alive at the start are updated: any added by a split wait until next frame.
		int count = Count;

		for (int i = 0; i < count; i++)
		{
			ref Enemy enemy = ref _enemies[i];
			EnemyBehavior behavior = _types[enemy.Type];
			EnemyDefinition type = behavior.Definition;
			float turnStep = Mathf.DegToRad(type.TurnSpeed) * dt;

			// Notice things at the aggro range. Once chasing, keep chasing until everything is well clear.
			// Wave enemies hunt from any distance.
			float noticeRange = enemy.Hunting
				? float.MaxValue
				: (enemy.Aggro ? type.LoseAggroRange : type.AggroRange) * Fight.EnemyAggroScale;

			// Go after a target: whichever is nearest of the VNP and its drones, or for a type that targets
			// drones first, the nearest drone, and the VNP only if no drone is in range. Keep after the same
			// one for a while (TargetHoldSeconds), so drones flying past do not make the enemy flick between them.
			float vnpDistance = enemy.Position.DistanceTo(vnpPosition);
			Vector2 targetPosition = vnpPosition;
			Vector2 targetVelocity = vnpVelocity;
			float distance = vnpAlive ? vnpDistance : float.MaxValue;
			enemy.TargetTimer -= dt;
			bool keptDrone = false;
			if (enemy.TargetDroneId != 0 && enemy.TargetTimer > 0f
				&& drones.TryGet(enemy.TargetDroneId, out Vector2 heldPosition, out Vector2 heldVelocity))
			{
				float heldDistance = enemy.Position.DistanceTo(heldPosition);
				if (heldDistance <= noticeRange)
				{
					targetPosition = heldPosition;
					targetVelocity = heldVelocity;
					distance = heldDistance;
					keptDrone = true;
				}
			}
			if (!keptDrone && (enemy.TargetDroneId != 0 || enemy.TargetTimer <= 0f))
			{
				// Look again: the hold ran out, or the drone it was chasing is gone or out of reach.
				enemy.TargetDroneId = 0;
				enemy.TargetTimer = type.TargetHoldSeconds * Rng.Range(0.8f, 1.2f);
				float searchRange = type.TargetsDronesFirst ? noticeRange : Mathf.Min(distance, noticeRange);
				// Skip the drone search when even the farthest drone could not be near enough.
				if (vnpDistance - drones.Reach <= noticeRange
					&& drones.FindNearest(enemy.Position, searchRange,
						out Vector2 dronePosition, out Vector2 droneVelocity, out float droneDistance, out int droneId))
				{
					targetPosition = dronePosition;
					targetVelocity = droneVelocity;
					distance = droneDistance;
					enemy.TargetDroneId = droneId;
				}
			}

			Vector2 toTarget = targetPosition - enemy.Position;
			Vector2 directionToTarget = distance > 0.001f ? toTarget / distance : Vector2.Up;
			// Turn its sense of where the target is at its turn rate, so a target that darts past or
			// through it, like a ramming drone, makes it swing round rather than jerk. While the target is
			// nearly behind, keep turning the way it already was, so a target crossing behind it does not
			// make it swing back and forth.
			float trackDifference = enemy.TrackDirection.AngleTo(directionToTarget);
			if (Mathf.Abs(trackDifference) > Mathf.DegToRad(150f) && enemy.TrackTurnSign != 0f)
				trackDifference = Mathf.Abs(trackDifference) * enemy.TrackTurnSign;
			float trackStep = Mathf.Clamp(trackDifference, -turnStep, turnStep);
			if (Mathf.Abs(trackStep) > 0.0001f)
				enemy.TrackTurnSign = Mathf.Sign(trackStep);
			enemy.TrackDirection = enemy.TrackDirection.Rotated(trackStep);
			Vector2 trackDirection = enemy.TrackDirection;

			bool aggro = distance <= noticeRange;
			if (enemy.Aggro && !aggro)
				enemy.Home = enemy.Position; // Gave up: roam around here from now on.
			enemy.Aggro = aggro;

			// Where this enemy wants to go on its own.
			Vector2 goal;
			float speed;
			if (aggro)
			{
				speed = type.Speed;
				goal = behavior.Chase(ref enemy, trackDirection, distance);
			}
			else
			{
				speed = type.WanderSpeed;
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
				float halfRadius = type.WanderRadius * 0.5f;
				float homePull = Mathf.Clamp((homeDistance - halfRadius) / halfRadius, 0f, 1.5f);
				goal = enemy.WanderDirection * type.WanderWeight;
				if (homeDistance > 0.001f)
					goal -= fromHome / homeDistance * homePull;
			}

			Vector2 steer = goal + Flocking(i, type, speed);
			Vector2 wantedVelocity = steer.LimitLength(1f) * speed;
			enemy.Velocity = enemy.Velocity.MoveToward(wantedVelocity, type.Acceleration * dt);
			enemy.Position += enemy.Velocity * dt;

			var target = new Target
			{
				Position = targetPosition,
				Velocity = targetVelocity,
				Distance = distance,
				Direction = directionToTarget,
				Track = trackDirection,
			};
			bool faced = behavior.Act(ref enemy, i, target, dt);

			// Face the direction of travel, unless its attack faces it somewhere else.
			if (faced)
			{
			}
			else if (aggro && enemy.Velocity.LengthSquared() > 0.25f)
				enemy.Yaw = Mathf.RotateToward(enemy.Yaw, PlaneMath.YawFacing(enemy.Velocity), turnStep);
			else if (!aggro && enemy.Velocity.Length() > type.WanderSpeed * 0.5f)
			{
				// Idle: turn slowly, and ignore small changes of course, so nudges from flockmates
				// do not show as twitching.
				float heading = PlaneMath.YawFacing(enemy.Velocity);
				if (Mathf.Abs(Mathf.AngleDifference(enemy.Yaw, heading)) > Mathf.DegToRad(type.IdleTurnDeadband))
					enemy.Yaw = Mathf.RotateToward(enemy.Yaw, heading, Mathf.DegToRad(type.IdleTurnSpeed) * dt);
			}

			enemy.Flash = Mathf.Max(0f, enemy.Flash - dt);
		}
	}

	/// <summary>
	/// Flocking: push away from any close enemy, and match the heading of and drift toward the middle of
	/// nearby enemies of the same type. Large enemies push apart before they overlap.
	/// </summary>
	private Vector2 Flocking(int index, EnemyDefinition type, float speed)
	{
		ref Enemy enemy = ref _enemies[index];
		float neighborRadius = type.NeighborRadius;
		float neighborRadiusSquared = neighborRadius * neighborRadius;

		Vector2 separation = Vector2.Zero;
		Vector2 flockVelocity = Vector2.Zero;
		Vector2 flockCenter = Vector2.Zero;
		int flockmates = 0;

		float searchRadius = Mathf.Max(neighborRadius, enemy.Radius + MaxRadius);
		int nearbyCount = _grid.Query(enemy.Position, searchRadius, _nearby);
		for (int n = 0; n < nearbyCount; n++)
		{
			int other = _nearby[n];
			if (other == index)
				continue;

			ref Enemy neighbor = ref _enemies[other];
			Vector2 away = enemy.Position - neighbor.Position;
			float gapSquared = away.LengthSquared();
			float separationRadius = Mathf.Max(type.SeparationRadius, (enemy.Radius + neighbor.Radius) * 1.1f);
			if (gapSquared > neighborRadiusSquared && gapSquared > separationRadius * separationRadius)
				continue;

			float gap = Mathf.Sqrt(gapSquared);
			if (gap < separationRadius)
			{
				Vector2 awayDirection = gap > 0.001f ? away / gap : Rng.Direction();
				separation += awayDirection * (1f - gap / separationRadius);
			}

			if (neighbor.Type != enemy.Type || gapSquared > neighborRadiusSquared)
				continue;
			flockmates++;
			flockVelocity += neighbor.Velocity;
			flockCenter += neighbor.Position;
		}

		Vector2 steer = separation * type.Separation;
		if (flockmates > 0)
		{
			steer += (flockVelocity / flockmates / speed).LimitLength(1f) * type.Alignment;
			steer += (flockCenter / flockmates - enemy.Position) / neighborRadius * type.Cohesion;
		}
		return steer;
	}

	private void Draw()
	{
		Array.Clear(_drawCounts);
		foreach (EnemyBehavior type in _types)
			type.Batch.Begin(Count);

		for (int i = 0; i < Count; i++)
		{
			ref Enemy enemy = ref _enemies[i];
			EnemyBehavior type = _types[enemy.Type];
			// Idle enemies are drawn darker, so it is easy to see which ones have noticed the VNP.
			Color color = enemy.Aggro ? type.Definition.Color : type.Definition.Color.Darkened(0.45f);
			if (enemy.Flash > 0f)
				color = color.Lerp(Colors.White, enemy.Flash / Tuning.Feedback.HitFlashTime);
			type.Draw(_drawCounts[enemy.Type]++, enemy, color);
		}

		for (int t = 0; t < _types.Count; t++)
			_types[t].Batch.Commit(_drawCounts[t]);
	}
}
