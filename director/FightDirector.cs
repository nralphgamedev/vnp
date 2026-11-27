using Godot;

/// <summary>
/// The Fight Director (docs/dev/FIGHT_DIRECTOR_SPEC.md): makes each fight's plan from its brief as the fight
/// starts, then decides when and where enemies arrive. The enemy swarm does the placing and everything after.
///
/// Its own choices use generators of their own, seeded from the fight's seed, one for each kind of choice, never
/// the fight's random numbers. So the plan depends on the brief alone, which lets the galactic map know a fight's
/// plan before it starts; and a choice added later doesn't change the fights that already exist. Where and what
/// each group is still comes from the fight's random numbers, as the swarm places it.
/// </summary>
public sealed class FightDirector
{
	public FightBrief Brief { get; }

	/// <summary>What this fight follows, picked as it starts.</summary>
	public FightPlan Plan { get; }

	/// <summary>Waves sent so far.</summary>
	public int WavesSent { get; private set; }

#if RECORDING
	/// <summary>How many groups the last wave had, for the run review's log.</summary>
	public int LastWaveGroups { get; private set; }
#endif

	/// <summary>Fight time, in seconds, when the next wave arrives.</summary>
	public float NextWaveTime => _waves == null ? float.PositiveInfinity
		: _waves.WaveFirstSeconds + WavesSent * _waves.WaveIntervalSeconds;

	/// <summary>Fight time, in seconds, when the last wave was due (one interval before the first, before any).</summary>
	public float LastWaveTime => _waves == null ? float.NegativeInfinity : NextWaveTime - _waves.WaveIntervalSeconds;

	// The streams of the director's own choices, one each, so adding one leaves the others alone. The asteroid
	// amount's is the one it had before the director, so seeds keep their amounts.
	private const int AsteroidAmountStream = 3;
	private const int PlanStream = 5;

	private readonly FightPlanDefinition _waves;
	private float _spawnTimer;
	private bool _startingGroupsSpawned;

	public FightDirector(FightBrief brief, int seed)
	{
		Brief = brief;
		FactionDefinition faction = brief.Faction ?? Tuning.FightDirector?.DefaultFaction;
		if (faction == null)
			GD.PushError("No faction to fight. Set DefaultFaction in res://director/fight_director.tres.");
		Plan = new FightPlan
		{
			Faction = faction,
			Definition = PickPlan(faction, seed),
			AsteroidAmount = PickAsteroidAmount(seed, brief.AsteroidAmountId),
		};
		_waves = Plan.Definition;
		_spawnTimer = Plan.Definition?.SpawnInterval ?? 0f;
	}

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
				swarm.SpawnScattered(vnpPosition, plan.SpawnExtraDistance, respectMaxAlive: false);
		}

		if (fight.Elapsed >= NextWaveTime)
			SendWave(fight, swarm, vnpPosition);

		_spawnTimer -= dt;
		if (_spawnTimer > 0f)
			return;

		// The spawn rate climbs through the fight.
		_spawnTimer = plan.SpawnInterval / fight.EnemySpawnRateScale;
		swarm.SpawnScattered(vnpPosition, plan.SpawnExtraDistance, respectMaxAlive: true);
	}

	/// <summary>A wave: each one has more groups than the last.</summary>
	private void SendWave(Fight fight, EnemySwarm swarm, Vector2 vnpPosition)
	{
		int groups = _waves.WaveGroups + _waves.WaveGroupsAddedPerWave * WavesSent;
		WavesSent++;
#if RECORDING
		LastWaveGroups = groups;
#endif
		fight.Camera.Shake(Tuning.Feedback.ShakeOnWave);
		swarm.SpawnRing(vnpPosition, groups);
	}

	/// <summary>A generator for one kind of the director's choices, from the fight's seed.</summary>
	private static System.Random Stream(int seed, int stream) => new(seed * 7919 + stream);

	private static FightPlanDefinition PickPlan(FactionDefinition faction, int seed)
	{
		Godot.Collections.Dictionary<FightPlanDefinition, float> plans = faction?.Plans;
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
