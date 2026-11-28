using Godot;

/// <summary>
/// The Fight Director (docs/dev/FIGHT_DIRECTOR_SPEC.md): makes each fight's plan from its brief as the fight
/// starts, then decides when, where and what enemies arrive: groups between waves, large announced waves, and
/// small unannounced ones. The enemy swarm does the placing and everything after.
///
/// Its own choices use generators of their own, seeded from the fight's seed, one for each kind of choice, never
/// the fight's random numbers. So the plan depends on the brief alone, which lets the galactic map know a fight's
/// plan before it starts; and a choice added later doesn't change the fights that already exist. Where each group
/// goes and what it is still come from the fight's random numbers, as the swarm places it.
/// </summary>
public sealed class FightDirector
{
	public FightBrief Brief { get; }

	/// <summary>What this fight follows, picked as it starts.</summary>
	public FightPlan Plan { get; }

	/// <summary>Large, announced waves sent so far.</summary>
	public int WavesSent => _large?.Sent ?? 0;

	/// <summary>Small, unannounced waves sent so far.</summary>
	public int SmallWavesSent => _small?.Sent ?? 0;

#if RECORDING
	/// <summary>How many groups the last large wave had, for the run review's log.</summary>
	public int LastWaveGroups { get; private set; }

	/// <summary>How many groups the last small wave had, for the run review's log.</summary>
	public int LastSmallWaveGroups { get; private set; }
#endif

	/// <summary>Fight time, in seconds, when the next large wave arrives.</summary>
	public float NextWaveTime => _large?.NextTime ?? float.PositiveInfinity;

	/// <summary>Fight time, in seconds, when the last large wave was due (one interval before the first, before any).</summary>
	public float LastWaveTime => _large?.LastTime ?? float.NegativeInfinity;

	// The streams of the director's own choices, one each, so adding one leaves the others alone. The asteroid
	// amount's is the one it had before the director, so seeds keep their amounts.
	private const int AsteroidAmountStream = 3;
	private const int PlanStream = 5;
	private const int LargeWaveStream = 6;
	private const int SmallWaveStream = 7;
	private const int StreamsStream = 8;

	// The most streams at once.
	private const int MaxStreams = 8;

	private readonly int _seed;
	private WaveStream _large;
	private WaveStream _small;
	private float _spawnTimer;
	private float _refillTimer;
	private bool _startingGroupsSpawned;

	// Streams of single enemies (StreamsDefinition): how many there are, where the first comes from, how far the
	// spawn points have turned, each stream's time to its next enemy, and the next time a stream is added.
	private int _streamCount;
	private float _streamAngle;
	private float _streamTurn;
	private readonly float[] _streamTimers = new float[MaxStreams];
	private int _nextStreamAdd;

	public FightDirector(FightBrief brief, int seed)
	{
		Brief = brief;
		_seed = seed;
		FactionDefinition faction = brief.Faction ?? Tuning.FightDirector?.DefaultFaction;
		if (faction == null)
			GD.PushError("No faction to fight. Set DefaultFaction in res://director/fight_director.tres.");
		Plan = new FightPlan
		{
			Faction = faction,
			Definition = PickPlan(faction, brief.PlanId, seed),
			AsteroidAmount = PickAsteroidAmount(seed, brief.AsteroidAmountId),
		};
		StartPlan();
	}

	/// <summary>Sets up the plan's wave streams and spawn timer, before the fight's first frame.</summary>
	private void StartPlan()
	{
		FightPlanDefinition plan = Plan.Definition;
		_large = _small = null;
		_streamCount = 0;
		_streamTurn = 0f;
		_nextStreamAdd = 0;
		if (plan == null)
			return;
		_streamAngle = (float)Stream(_seed, StreamsStream).NextDouble() * Mathf.Tau;
		if (plan.LargeWaves != null)
			_large = new WaveStream(plan.LargeWaves, Stream(_seed, LargeWaveStream));
		if (plan.SmallWaves != null)
			_small = new WaveStream(plan.SmallWaves, Stream(_seed, SmallWaveStream));
		if (plan.Curve == null)
			GD.PushError($"Plan {plan.Id} has no Curve, so the fight doesn't grow harder before the time limit.");
		_spawnTimer = plan.SpawnInterval;
	}

#if TOOLS
	/// <summary>Playtest cheat, editor only: follows this plan instead of the one picked, as the fight starts,
	/// before anything has spawned. Through Fight.ChoosePlan, which also changes the difficulty curve.</summary>
	public void ChoosePlan(FightPlanDefinition plan)
	{
		Plan.Definition = plan;
		StartPlan();
	}
#endif

	/// <summary>Called by the swarm each frame, where it used to spawn, before enemies move.</summary>
	public void Update(Fight fight, EnemySwarm swarm, float dt, Vector2 vnpPosition)
	{
		FightPlanDefinition plan = Plan.Definition;
		if (plan == null)
			return;

		if (!_startingGroupsSpawned)
		{
			_startingGroupsSpawned = true;
			for (int i = 0; i < plan.StartingGroups; i++)
				swarm.SpawnScattered(vnpPosition, FieldExtraDistance(plan), respectMaxAlive: false, Vector2.Zero, 0f,
					plan.GroupStrength, plan.StartClearance);
		}

		// Keep the field dense: a group at a time while the population is below what the plan keeps alive.
		if (plan.KeepAlive > 0)
		{
			_refillTimer -= dt;
			if (_refillTimer <= 0f && swarm.Count < plan.KeepAlive + plan.KeepAlivePerMinute * fight.Minutes)
			{
				_refillTimer = Mathf.Max(0.05f, plan.RefillSeconds);
				swarm.SpawnScattered(vnpPosition, FieldExtraDistance(plan), respectMaxAlive: true, Vector2.Zero, 0f,
					plan.GroupStrength);
			}
		}

		if (_large != null && fight.Elapsed >= _large.NextTime)
			SendWave(fight, swarm, vnpPosition, _large, announced: true);
		if (_small != null && fight.Elapsed >= _small.NextTime)
			SendWave(fight, swarm, vnpPosition, _small, announced: false);
		if (plan.Streams != null && fight.Elapsed >= plan.Streams.FirstSeconds)
			UpdateStreams(fight, swarm, dt, vnpPosition, plan.Streams);

		_spawnTimer -= dt;
		if (_spawnTimer > 0f)
			return;

		// The spawn rate climbs through the fight.
		_spawnTimer = plan.SpawnInterval / fight.EnemySpawnRateScale;
		// Some groups ahead of a moving VNP. No number is drawn for a plan without them, so classic fights are unchanged.
		Vector2 ahead = plan.AheadShare > 0f && Heading(fight) is Vector2 heading && Rng.Value() < plan.AheadShare
			? heading : Vector2.Zero;
		swarm.SpawnScattered(vnpPosition, plan.SpawnExtraDistance, respectMaxAlive: true, ahead,
			Mathf.DegToRad(plan.AheadSpreadDegrees), plan.GroupStrength);
	}

	/// <summary>How far beyond the margin starting and refill groups go: the plan's field distance, or its usual
	/// extra distance.</summary>
	private static float FieldExtraDistance(FightPlanDefinition plan) =>
		plan.FieldExtraDistance > 0f ? plan.FieldExtraDistance : plan.SpawnExtraDistance;

	/// <summary>The way the VNP is heading, or null while it is nearly still (below a third of its starting top
	/// speed), when there is no ahead.</summary>
	private static Vector2? Heading(Fight fight)
	{
		Vector2 velocity = fight.Vnp.Velocity;
		float slowest = Tuning.Vnp.MoveSpeed / 3f;
		return velocity.LengthSquared() > slowest * slowest ? velocity.Normalized() : null;
	}

	/// <summary>
	/// Streams of single enemies: one at first, more added at their times and spread evenly round the VNP, each
	/// spawning an enemy every gap, all turning round it together, faster from RotateFromSeconds.
	/// </summary>
	private void UpdateStreams(Fight fight, EnemySwarm swarm, float dt, Vector2 vnpPosition, StreamsDefinition streams)
	{
		if (_streamCount == 0)
		{
			_streamCount = 1;
			_streamTimers[0] = 0f;
		}
		float gap = Mathf.Max(streams.MinIntervalSeconds, streams.IntervalSeconds / (1f + streams.RatePerMinute * fight.Minutes))
			* (1f + streams.IntervalPerExtraStream * (_streamCount - 1));
		while (_nextStreamAdd < streams.AddStreamSeconds.Count && fight.Elapsed >= streams.AddStreamSeconds[_nextStreamAdd])
		{
			_nextStreamAdd++;
			if (_streamCount >= MaxStreams)
				continue;
			// The new stream's first enemy comes half a gap later, so the streams take turns.
			_streamTimers[_streamCount] = gap * 0.5f;
			_streamCount++;
		}
		// Turning all the while, faster from RotateFromSeconds.
		bool faster = streams.RotateFromSeconds > 0f && fight.Elapsed >= streams.RotateFromSeconds;
		_streamTurn += Mathf.DegToRad(faster ? streams.RotationDegreesPerSecond : streams.StartRotationDegreesPerSecond) * dt;

		float hardShare = Mathf.Clamp(streams.HardShareStart + streams.HardSharePerMinute * fight.Minutes, 0f, streams.HardShareMax);
		float jitter = Mathf.DegToRad(streams.JitterDegrees);
		for (int s = 0; s < _streamCount; s++)
		{
			_streamTimers[s] -= dt;
			if (_streamTimers[s] > 0f)
				continue;
			_streamTimers[s] += gap;
			int type = swarm.PickWaveType(streams.Makeup, hardShare);
			if (type < 0)
				return;
			float angle = _streamAngle + _streamTurn + Mathf.Tau * s / _streamCount + Rng.Range(-jitter, jitter);
			swarm.SpawnStreamEnemy(vnpPosition, Vector2.FromAngle(angle), type, streams.Strength);
		}
	}

	/// <summary>A wave: an announced one shakes the screen; the HUD and the sounds warn of it from the director's
	/// timing.</summary>
	private void SendWave(Fight fight, EnemySwarm swarm, Vector2 vnpPosition, WaveStream stream, bool announced)
	{
		int groups = stream.NextGroups;
		stream.Advance();
#if RECORDING
		if (announced)
			LastWaveGroups = groups;
		else
			LastSmallWaveGroups = groups;
#endif
		if (announced)
			fight.Camera.Shake(Tuning.Feedback.ShakeOnWave);
		PlaceWave(fight, swarm, vnpPosition, stream, groups);
	}

	/// <summary>
	/// Places a wave's groups just off screen around the VNP, by its arrival, each of a type picked by its makeup.
	/// The direction is drawn from the fight's random numbers, then each group's type in turn, as the ring wave
	/// always did, so classic fights are unchanged.
	/// </summary>
	private static void PlaceWave(Fight fight, EnemySwarm swarm, Vector2 vnpPosition, WaveStream stream, int groups)
	{
		WavesDefinition waves = stream.Definition;
		float hardShare = Mathf.Clamp(waves.HardShareStart + waves.HardSharePerMinute * fight.Minutes, 0f, waves.HardShareMax);
		float spread = Mathf.DegToRad(waves.SpreadDegrees);
		float startAngle = Rng.Range(0f, Mathf.Tau);
		// A front or pincer aimed ahead of a moving VNP. No number is drawn for a wave without it.
		if (waves.Arrival != WaveArrival.Ring && waves.AheadShare > 0f && Heading(fight) is Vector2 heading
			&& Rng.Value() < waves.AheadShare)
			startAngle = heading.Angle();

		// The formation, for every group of this wave. No number is drawn for the usual one.
		WaveFormation formation = waves.Formation == WaveFormation.Varied
			? (Rng.Value() < 0.5f ? WaveFormation.Pack : WaveFormation.Loose)
			: waves.Formation;
		for (int g = 0; g < groups; g++)
		{
			int type = swarm.PickWaveType(waves.Makeup, hardShare, waves.ThemedType);
			if (type < 0)
				return;
			float angle = waves.Arrival switch
			{
				WaveArrival.Front => startAngle + Fan(g, groups, spread),
				// Alternate sides, so an odd group goes to the first side.
				WaveArrival.Pincer => startAngle + (g % 2) * Mathf.Pi + Fan(g / 2, (groups + 1 - g % 2) / 2, spread),
				_ => startAngle + Mathf.Tau * g / groups,
			};
			swarm.SpawnWaveGroup(vnpPosition, Vector2.FromAngle(angle), type, waves.Strength, formation,
				waves.LooseSpread, Mathf.DegToRad(waves.LooseAngleDegrees));
		}
	}

	/// <summary>The angle of the i-th of n groups spread evenly across an arc, from its middle.</summary>
	private static float Fan(int i, int n, float arc) => n <= 1 ? 0f : arc * ((float)i / (n - 1) - 0.5f);

	/// <summary>A generator for one kind of the director's choices, from the fight's seed.</summary>
	private static System.Random Stream(int seed, int stream) => new(seed * 7919 + stream);

	/// <summary>The plan with this ID from the faction's plans, or, for none, one picked by their weights.</summary>
	private static FightPlanDefinition PickPlan(FactionDefinition faction, string id, int seed)
	{
		Godot.Collections.Dictionary<FightPlanDefinition, float> plans = faction?.Plans;
		if (id != null && plans != null)
		{
			foreach (FightPlanDefinition plan in plans.Keys)
			{
				if (plan != null && plan.Id == id)
					return plan;
			}
			GD.PushError($"No plan {id} among {faction.Id}'s plans; picking one at random.");
		}
		float total = 0f;
		if (plans != null)
		{
			foreach ((FightPlanDefinition plan, float weight) in plans)
				total += plan == null ? 0f : Mathf.Max(0f, weight);
		}
		if (total <= 0f)
		{
			GD.PushError($"No fight plan to pick. Give at least one a weight in {faction?.ResourcePath ?? "the faction's file"}.");
			return null;
		}
		float roll = (float)Stream(seed, PlanStream).NextDouble() * total;
		FightPlanDefinition picked = null;
		foreach ((FightPlanDefinition plan, float weight) in plans)
		{
			if (plan == null || weight <= 0f)
				continue;
			picked = plan;
			roll -= weight;
			if (roll < 0f)
				break;
		}
		return picked;
	}

	/// <summary>
	/// The asteroid amount with this ID, or, for none, one picked by the amounts' weights. It was picked this way,
	/// from a generator of its own, before the director existed, so a seed keeps the amount it always had.
	/// </summary>
	private static AsteroidAmountDefinition PickAsteroidAmount(int seed, string id)
	{
		Godot.Collections.Array<AsteroidAmountDefinition> amounts = Tuning.Asteroids.Amounts;
		if (amounts == null || amounts.Count == 0)
			return null;
		if (id != null)
		{
			foreach (AsteroidAmountDefinition amount in amounts)
			{
				if (amount != null && amount.Id == id)
					return amount;
			}
			GD.PushError($"No asteroid amount {id} in asteroids.tres; picking one at random.");
		}
		float total = 0f;
		foreach (AsteroidAmountDefinition amount in amounts)
			total += amount == null ? 0f : Mathf.Max(0f, amount.Weight);
		if (total <= 0f)
			return amounts[0];
		float roll = (float)Stream(seed, AsteroidAmountStream).NextDouble() * total;
		foreach (AsteroidAmountDefinition amount in amounts)
		{
			if (amount == null)
				continue;
			roll -= Mathf.Max(0f, amount.Weight);
			if (roll < 0f)
				return amount;
		}
		return amounts[amounts.Count - 1];
	}
}
