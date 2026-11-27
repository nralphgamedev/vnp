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

	private readonly WaveStream _large;
	private readonly WaveStream _small;
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
			Definition = PickPlan(faction, brief.PlanId, seed),
			AsteroidAmount = PickAsteroidAmount(seed, brief.AsteroidAmountId),
		};
		FightPlanDefinition plan = Plan.Definition;
		if (plan == null)
			return;
		if (plan.LargeWaves != null)
			_large = new WaveStream(plan.LargeWaves, Stream(seed, LargeWaveStream));
		else
			GD.PushError($"Plan {plan.Id} has no LargeWaves.");
		if (plan.SmallWaves != null)
			_small = new WaveStream(plan.SmallWaves, Stream(seed, SmallWaveStream));
		if (plan.Curve == null)
			GD.PushError($"Plan {plan.Id} has no Curve, so the fight doesn't grow harder before the time limit.");
		_spawnTimer = plan.SpawnInterval;
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

		if (_large != null && fight.Elapsed >= _large.NextTime)
			SendWave(fight, swarm, vnpPosition, _large, announced: true);
		if (_small != null && fight.Elapsed >= _small.NextTime)
			SendWave(fight, swarm, vnpPosition, _small, announced: false);

		_spawnTimer -= dt;
		if (_spawnTimer > 0f)
			return;

		// The spawn rate climbs through the fight.
		_spawnTimer = plan.SpawnInterval / fight.EnemySpawnRateScale;
		swarm.SpawnScattered(vnpPosition, plan.SpawnExtraDistance, respectMaxAlive: true);
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
		PlaceWave(fight, swarm, vnpPosition, stream.Definition, groups);
	}

	/// <summary>
	/// Places a wave's groups just off screen around the VNP, by its arrival, each of a type picked by its makeup.
	/// The direction is drawn from the fight's random numbers, then each group's type in turn, as the ring wave
	/// always did, so classic fights are unchanged.
	/// </summary>
	private static void PlaceWave(Fight fight, EnemySwarm swarm, Vector2 vnpPosition, WavesDefinition waves, int groups)
	{
		float hardShare = Mathf.Clamp(waves.HardShareStart + waves.HardSharePerMinute * fight.Minutes, 0f, waves.HardShareMax);
		float spread = Mathf.DegToRad(waves.SpreadDegrees);
		float startAngle = Rng.Range(0f, Mathf.Tau);
		for (int g = 0; g < groups; g++)
		{
			int type = swarm.PickWaveType(waves.Makeup, hardShare);
			if (type < 0)
				return;
			float angle = waves.Arrival switch
			{
				WaveArrival.Front => startAngle + Fan(g, groups, spread),
				// Alternate sides, so an odd group goes to the first side.
				WaveArrival.Pincer => startAngle + (g % 2) * Mathf.Pi + Fan(g / 2, (groups + 1 - g % 2) / 2, spread),
				_ => startAngle + Mathf.Tau * g / groups,
			};
			swarm.SpawnWaveGroup(vnpPosition, Vector2.FromAngle(angle), type);
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
