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
/// (see EnemyBehavior.cs): the basic enemy holds at a distance, circles and fires, and the basic goo absorbs
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
		public float SpeedScale;        // Its speed as a multiple of its type's, from escalation when it spawned.
		public float Drain;             // Health per second it drains from a drone touching it (Grey Goo that absorbs drones).
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

		// Used by Grey Goo.
		public float BaseHealth;        // Health it spawned with. Growth is measured against it.
		public float Bulk;              // 1 as it spawns. Absorbed drones add to it, and a split halves it.
		public float Absorbed;          // Drones it has absorbed, shared out between halves when it splits.
		public float Consumed;          // Mass of material it has consumed, such as a Devourer's.
		public int AbsorbedSinceSplit;  // Drones absorbed since it spawned or last split.
		public float Pulse;             // Seconds left of the swell after absorbing a drone.
		public float SplitTimer;        // Seconds left of bulging before it splits, or 0.
		public float SplitAngle;        // The line it will split along, as an angle on the plane.
		public float Phase;             // Where it is in its wobble.

		// Used by Grey Goo attacks that take time, such as the Lancer's launch: which phase it is in (the type's own
		// numbering, 0 between attacks), seconds left in it, where the attack started, which way it goes, how far it
		// reaches, and its damage at the strength it spawned with.
		public int AttackPhase;
		public float AttackTimer;
		public Vector2 AttackFrom;
		public Vector2 AttackDirection;
		public float AttackLength;
		public float AttackDamage;
		public bool AttackHitVnp;       // Whether this attack has hit the VNP yet, so it hits once.
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

	/// <summary>The radius of the largest enemy alive, as of the start of this frame's enemy update.</summary>
	public float MaxRadius { get; private set; }

	/// <summary>The largest DroneAvoidDistance of any enemy type, so drones know how far to look.</summary>
	public float MaxDroneAvoid { get; private set; }

	/// <summary>Every enemy type the fight knows, from the keys of its faction's weights.</summary>
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
	private int _lastId;
	private FactionDefinition _faction;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Enemies;

		// The fight made its plan as it entered the tree, before this is ready.
		_faction = GetParentOrNull<Fight>()?.Director.Plan.Faction ?? Tuning.FightDirector.DefaultFaction;
		AddTypes(_faction?.GroupWeights);
		AddTypes(_faction?.WaveGroupWeights);
		if (_types.Count == 0)
			GD.PushError("No enemy types to spawn. List at least one in the faction's weights, such as res://enemies/grey_goo/grey_goo.tres.");

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
		Fight.Director.Update(Fight, this, dt, vnpPosition);
		Simulate(dt, vnpPosition, Fight.Vnp.Velocity, vnpAlive: !Fight.Vnp.IsDestroyed);
		SplitQueued();
		SpawnQueued();
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

	/// <summary>Whether the enemy at this place in the list is still alive.</summary>
	public bool IsAlive(int index) => _enemies[index].Health > 0f;

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
	/// Has every living enemy within <paramref name="radius"/> of a point declare its body and hazards
	/// (EnemyBehavior.DeclareThreats) to a situation, for pilots and the run review. With <paramref name="visible"/>,
	/// only those it says can be seen, such as what is on screen. Read-only.
	/// </summary>
	public void DeclareThreats(Arbiter.Situation situation, Vector2 center, float radius, System.Func<Vector2, bool> visible = null)
	{
		if (_declareNear.Length < Count)
			_declareNear = new int[Count * 2];
		int count = CollectNear(center, radius + MaxRadius, _declareNear);
		for (int c = 0; c < count; c++)
		{
			ref Enemy enemy = ref _enemies[_declareNear[c]];
			if (visible == null || visible(enemy.Position))
				_types[enemy.Type].DeclareThreats(enemy, situation);
		}
	}

	private int[] _declareNear = new int[256];

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

	/// <summary>Whether an enemy damages the VNP by touching it, as Grey Goo does.</summary>
	public bool DamagesByTouchOf(int index) => _types[_enemies[index].Type].DamagesByTouch;

	/// <summary>Whether an enemy absorbs drones that touch it, as the basic goo does.</summary>
	public bool AbsorbsDronesOf(int index) => _types[_enemies[index].Type].AbsorbsDrones;

	/// <summary>Which enemy type an enemy is, by the type's ID.</summary>
	public string TypeIdOf(int index) => _types[_enemies[index].Type].Definition.Id;

#if RECORDING
	/// <summary>An enemy's definition, for testing tools such as the run review's maps. Read it, don't change it.</summary>
	public EnemyDefinition DefinitionOf(int index) => _types[_enemies[index].Type].Definition;
#endif

#if RECORDING
	/// <summary>How many living enemies there are of each type, in the order of Types. For the fight log.</summary>
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
#endif

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

	/// <summary>Splits the enemy at this index after this frame's update. Used by the basic goo.</summary>
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

	/// <summary>Which of the faction's tiers a pick draws from.</summary>
	private enum TierFilter { Any, Easiest, Harder }

	/// <summary>
	/// An enemy type picked at random, by the weights given, or -1 if none has a weight. With a filter, only from
	/// the easiest tier among the weighted types, or only from those above it; if the filter leaves none, from any.
	/// </summary>
	private int PickType(Godot.Collections.Dictionary<EnemyDefinition, float> weights, TierFilter filter = TierFilter.Any)
	{
		if (weights == null)
			return -1;
		int easiest = int.MaxValue;
		if (filter != TierFilter.Any)
		{
			foreach ((EnemyDefinition definition, float weight) in weights)
			{
				if (definition != null && weight > 0f && _typeIndex.ContainsKey(definition))
					easiest = Mathf.Min(easiest, TierOf(definition));
			}
		}
		float total = 0f;
		foreach ((EnemyDefinition definition, float weight) in weights)
		{
			if (definition != null && weight > 0f && _typeIndex.ContainsKey(definition) && Passes(definition, filter, easiest))
				total += weight;
		}
		if (total <= 0f)
			return filter == TierFilter.Any ? -1 : PickType(weights);

		float roll = Rng.Range(0f, total);
		int picked = -1;
		foreach ((EnemyDefinition definition, float weight) in weights)
		{
			if (definition == null || weight <= 0f || !_typeIndex.TryGetValue(definition, out int index)
				|| !Passes(definition, filter, easiest))
				continue;
			picked = index;
			roll -= weight;
			if (roll <= 0f)
				break;
		}
		return picked;
	}

	private bool Passes(EnemyDefinition definition, TierFilter filter, int easiest) => filter switch
	{
		TierFilter.Easiest => TierOf(definition) == easiest,
		TierFilter.Harder => TierOf(definition) > easiest,
		_ => true,
	};

	/// <summary>How hard a type is in the fight's faction, from 1, the easiest; 1 if it isn't ranked.</summary>
	private int TierOf(EnemyDefinition definition) =>
		_faction.Tiers != null && _faction.Tiers.TryGetValue(definition, out int tier) ? tier : 1;

	/// <summary>
	/// A type for one group of a wave, by the wave's makeup: from every type by the wave weights (Mixed), from the
	/// easiest tier (Swarm), or from a harder tier with the chance <paramref name="hardShare"/> and the easiest
	/// otherwise (Growing). -1 if the faction has no wave types.
	/// </summary>
	public int PickWaveType(WaveMakeup makeup, float hardShare)
	{
		switch (makeup)
		{
			case WaveMakeup.Swarm:
				return PickType(_faction.WaveGroupWeights, TierFilter.Easiest);
			case WaveMakeup.Growing:
				bool hard = Rng.Value() < hardShare;
				return PickType(_faction.WaveGroupWeights, hard ? TierFilter.Harder : TierFilter.Easiest);
			default:
				return PickType(_faction.WaveGroupWeights);
		}
	}

	/// <summary>
	/// Spawns one group of a wave for the Fight Director, of this type, just off screen from the VNP in this
	/// direction, hunting it. Waves ignore the MaxAlive limit.
	/// </summary>
	public void SpawnWaveGroup(Vector2 vnpPosition, Vector2 direction, int type)
	{
		Vector2 home = vnpPosition + direction * OffScreenDistance(vnpPosition, direction, 0f);
		SpawnGroupAt(home, type, hunting: true);
	}

	/// <summary>Spawns one group for the Fight Director, of a type picked by the group weights, at a random spot
	/// off screen around the VNP, up to <paramref name="extraDistance"/> beyond the margin. That spot is the group's
	/// home. With <paramref name="respectMaxAlive"/>, it spawns nothing while the swarm is at MaxAlive.</summary>
	public void SpawnScattered(Vector2 vnpPosition, float extraDistance, bool respectMaxAlive)
	{
		int type = PickType(_faction.GroupWeights);
		if (type < 0)
			return;
		if (respectMaxAlive && Count + _types[type].Definition.GroupSize > _faction.MaxAlive)
			return;
		Vector2 direction = Rng.Direction();
		Vector2 home = vnpPosition + direction * OffScreenDistance(vnpPosition, direction, Rng.Range(0f, extraDistance));
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
				SpeedScale = Fight?.EnemySpeedScale ?? 1f,
			};
			behavior.Spawned(ref enemy);
			Append(enemy);
		}
	}

	/// <summary>
	/// Adds one enemy of a type, already awake (it has noticed what is near), at a point, with
	/// <paramref name="healthFraction"/> of its health, such as an asteroid mimic woken in a field. False if the
	/// fight has no such type.
	/// </summary>
	public bool SpawnAwake(EnemyDefinition definition, Vector2 at, float healthFraction = 1f)
	{
		if (definition == null || !_typeIndex.TryGetValue(definition, out int type))
			return false;
		Enemy enemy = NewAwake(type, at);
		enemy.Health *= Mathf.Clamp(healthFraction, 0.01f, 1f);
		Append(enemy);
		return true;
	}

	/// <summary>A new enemy of a type, already awake, at a point, at the fight's current difficulty.</summary>
	private Enemy NewAwake(int type, Vector2 at)
	{
		Vector2 direction = Rng.Direction();
		var enemy = new Enemy
		{
			Id = ++_lastId,
			Type = type,
			Position = at,
			Home = at,
			WanderDirection = direction,
			WanderTimer = Rng.Range(1.5f, 4f),
			Yaw = PlaneMath.YawFacing(direction),
			TrackDirection = direction,
			OrbitSign = Rng.Sign(),
			Aggro = true,
			SpeedScale = Fight?.EnemySpeedScale ?? 1f,
		};
		_types[type].Spawned(ref enemy);
		return enemy;
	}

	/// <summary>Another Grey Goo type for a split's second half, each equally likely, or -1 if there is none.</summary>
	private int OtherGreyGooType()
	{
		_otherTypes.Clear();
		for (int t = 0; t < _types.Count; t++)
		{
			if (_types[t] is GreyGoo and not BasicGoo)
				_otherTypes.Add(t);
		}
		return _otherTypes.Count == 0 ? -1 : _otherTypes[Rng.Int(0, _otherTypes.Count - 1)];
	}

	private readonly List<int> _otherTypes = new();

	private readonly List<(EnemyDefinition Definition, Vector2 At, Vector2 Velocity)> _queuedSpawns = new();

	/// <summary>
	/// Adds an enemy of a type, awake, at a point and moving at a velocity, once the enemies' update is done, so one
	/// enemy can make another during its own update, as a Devourer produces a basic goo. It then moves as usual,
	/// slowing to its own speed at its own acceleration. Nothing happens if the fight has no such type.
	/// </summary>
	public void QueueSpawn(EnemyDefinition definition, Vector2 at, Vector2 velocity) =>
		_queuedSpawns.Add((definition, at, velocity));

	private void SpawnQueued()
	{
		foreach ((EnemyDefinition definition, Vector2 at, Vector2 velocity) in _queuedSpawns)
		{
			if (definition == null || !_typeIndex.TryGetValue(definition, out int type))
				continue;
			Enemy enemy = NewAwake(type, at);
			enemy.Velocity = velocity;
			if (velocity.LengthSquared() > 0.0001f)
			{
				enemy.Yaw = PlaneMath.YawFacing(velocity.Normalized());
				enemy.TrackDirection = velocity.Normalized();
			}
			Append(enemy);
		}
		_queuedSpawns.Clear();
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
			if (index >= Count || _enemies[index].Health <= 0f || _types[_enemies[index].Type] is not BasicGoo goo)
				continue;
			Enemy other = default;
			goo.Split(ref _enemies[index], ref other);
			// Sometimes the second half is another Grey Goo type: a new one at the fight's difficulty, awake, carrying
			// on with the half's push apart. It takes nothing from the basic goo, which keeps its own half.
			if (goo.OtherTypeChance > 0f && Rng.Value() < goo.OtherTypeChance && OtherGreyGooType() is int type and >= 0)
			{
				Vector2 velocity = other.Velocity;
				other = NewAwake(type, other.Position);
				other.Velocity = velocity;
				Append(other);
				continue;
			}
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
			if (!keptDrone && type.Targets != EnemyTargets.VnpOnly && (enemy.TargetDroneId != 0 || enemy.TargetTimer <= 0f))
			{
				// Look again: the hold ran out, or the drone it was chasing is gone or out of reach.
				enemy.TargetDroneId = 0;
				enemy.TargetTimer = type.TargetHoldSeconds * Rng.Range(0.8f, 1.2f);
				float searchRange = type.Targets == EnemyTargets.DronesFirst ? noticeRange : Mathf.Min(distance, noticeRange);
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

			bool aggro = distance <= noticeRange && behavior.Chases;
			if (enemy.Aggro && !aggro)
				enemy.Home = enemy.Position; // Gave up: roam around here from now on.
			enemy.Aggro = aggro;

			// Where this enemy wants to go on its own.
			Vector2 goal;
			float speed;
			if (aggro)
			{
				speed = type.Speed * enemy.SpeedScale;
				goal = behavior.Chase(ref enemy, trackDirection, distance);
			}
			else if (behavior.Roam(ref enemy, dt) is Vector2 roam)
			{
				// Its own business while nothing has its attention, such as a Devourer heading for material.
				speed = type.Speed * enemy.SpeedScale;
				goal = roam;
				enemy.Home = enemy.Position;
			}
			else
			{
				speed = type.WanderSpeed * enemy.SpeedScale;
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

			Vector2 steer = goal + Flocking(i, type, speed, aggro ? type.ChaseFlocking : 1f);
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
			else if (!aggro && enemy.Velocity.Length() > type.WanderSpeed * enemy.SpeedScale * 0.5f)
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
	/// <param name="flock">How much alignment and cohesion count: ChaseFlocking while chasing, 1 otherwise.</param>
	private Vector2 Flocking(int index, EnemyDefinition type, float speed, float flock)
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
			steer += (flockVelocity / flockmates / speed).LimitLength(1f) * type.Alignment * flock;
			steer += (flockCenter / flockmates - enemy.Position) / neighborRadius * type.Cohesion * flock;
		}
		return steer;
	}

	private void Draw()
	{
		Array.Clear(_drawCounts);
		foreach (EnemyBehavior type in _types)
		{
			type.Batch.Begin(Count);
			type.BeginDraw();
		}

		for (int i = 0; i < Count; i++)
		{
			ref Enemy enemy = ref _enemies[i];
			EnemyBehavior type = _types[enemy.Type];
			// Idle enemies are drawn darker, so it is easy to see which ones have noticed the VNP.
			Color color = enemy.Aggro || !type.Chases ? type.Definition.Color : type.Definition.Color.Darkened(0.45f);
			if (enemy.Flash > 0f)
				color = color.Lerp(Colors.White, enemy.Flash / Tuning.Feedback.HitFlashTime);
			type.Draw(_drawCounts[enemy.Type]++, enemy, color);
		}

		for (int t = 0; t < _types.Count; t++)
		{
			_types[t].Batch.Commit(_drawCounts[t]);
			_types[t].EndDraw();
		}
	}
}
