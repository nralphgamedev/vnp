using Godot;
using NVector2 = System.Numerics.Vector2;

/// <summary>
/// How the pilot decides (docs/dev/PILOT_SPEC.md). It makes its judgements (whether it can afford the fight,
/// whether to regroup or withdraw, what to harvest, whether to commit or hold still), then turns each want into a
/// term that scores whole candidate paths, and follows the best path:
/// - perception: the hazards and zones that enemies, shots and its weapons declare, on screen and within its
///   awareness, with fleeting hazards noticed by its dodge chance;
/// - paths: Arbiter's planner tries directions at a few speeds, and stopping, over its look-ahead, with the VNP's
///   own acceleration and the stick's lag, and assesses each against the hazards;
/// - terms: harm it would take (as a share of its health), how close it comes to hazards, danger to its drones,
///   progress toward its harvest target, keeping its fighting distance, circling or holding still, backing off,
///   leading chasers over its traps, exploring, and a small cost for turning sharply.
/// The first pilot added its wants up into one direction instead, which averaged conflicting wants into weak,
/// in-between moves; its code was removed once this replaced it (git history keeps it).
/// </summary>
public partial class Pilot
{
	private readonly Arbiter.Situation _situation = new();
	private readonly Arbiter.PathPlanner _planner = new();
	private Arbiter.Decision _move;
	private int _tHarm, _tClose, _tDrones, _tHarvest, _tFight, _tOrbit, _tStill, _tAway, _tLead, _tExplore, _tTurn, _tKeepMoving;

	// What this decision's terms aim at, in Arbiter's vectors.
	private NVector2 _aimAway;
	private NVector2 _fightAt, _fightVelocity;
	private float _fightRadius, _fightPreferred, _fightFarShare, _fightStillShare;
	private NVector2 _harvestAt;
	private float _harvestReach;
	private NVector2 _leadAt, _exploreAt;
	private float _keepMovingTravel;

	private sealed class GameRandom : Arbiter.IRandom
	{
		public float Value() => Rng.Value();
	}

	private static readonly GameRandom Random = new();

	private void SetUpPlanning()
	{
		_move = new Arbiter.Decision("move");
		_tHarm = _move.AddTerm("harm", p => -_planner.HarmOf(p) / Mathf.Max(1f, _vnp.Health));
		_tClose = _move.AddTerm("close", p => -_planner.ClosenessOf(p));
		_tDrones = _move.AddTerm("drones", p => -_planner.AllyDangerOf(p));
		_tHarvest = _move.AddTerm("harvest", HarvestTerm);
		_tFight = _move.AddTerm("fight", FightTerm);
		_tOrbit = _move.AddTerm("orbit", OrbitTerm);
		_tStill = _move.AddTerm("still", p => 1f - Travel(p));
		_tAway = _move.AddTerm("away", p => NVector2.Dot(_planner.End(p) - _planner.Start.Position, _aimAway) / _planner.MaxTravel);
		_tLead = _move.AddTerm("lead", p => Progress(p, _leadAt));
		_tExplore = _move.AddTerm("explore", p => Progress(p, _exploreAt));
		_tTurn = _move.AddTerm("turn", TurnTerm);
		_tKeepMoving = _move.AddTerm("keep moving", p => Mathf.Min(1f, Travel(p) / Mathf.Max(0.01f, _keepMovingTravel)));
	}

	// ───────────── Terms: each a value for one path, higher better, about -1 to 1 ─────────────

	/// <summary>How far a path goes, as a share of the farthest it could.</summary>
	private float Travel(int p) => NVector2.Distance(_planner.End(p), _planner.Start.Position) / _planner.MaxTravel;

	/// <summary>How much closer a path ends to a point than it starts, as a share of the farthest it could go.</summary>
	private float Progress(int p, NVector2 target) =>
		(NVector2.Distance(_planner.Start.Position, target) - NVector2.Distance(_planner.End(p), target)) / _planner.MaxTravel;

	/// <summary>Getting within holding distance of the harvest target: how much a path closes the gap that's left,
	/// so a path that overshoots past the rock scores worse than one that stops beside it.</summary>
	private float HarvestTerm(int p)
	{
		float Gap(NVector2 at) => Mathf.Max(0f, NVector2.Distance(at, _harvestAt) - _harvestReach);
		return (Gap(_planner.Start.Position) - Gap(_planner.End(p))) / _planner.MaxTravel;
	}

	/// <summary>Keeping its fighting distance from the enemy it fights, by where that enemy will be: how much a path
	/// closes the gap from that distance. Being too far counts only by _fightFarShare (ApproachEnemies, less while
	/// holding still); being too close always counts.</summary>
	private float FightTerm(int p)
	{
		float Gap(NVector2 at, NVector2 enemy)
		{
			float distance = NVector2.Distance(at, enemy) - _fightRadius;
			if (distance < _fightPreferred)
				return _fightPreferred - distance;
			// Too far. While holding still, it only closes in on an enemy well beyond its distance.
			float far = (distance - _fightPreferred) * _fightFarShare;
			return far + Mathf.Max(0f, distance - _fightPreferred * 1.5f) * _fightStillShare;
		}
		NVector2 enemyEnd = _fightAt + _fightVelocity * _planner.Horizon;
		return (Gap(_planner.Start.Position, _fightAt) - Gap(_planner.End(p), enemyEnd)) / _planner.MaxTravel;
	}

	/// <summary>Going round the enemy it fights, either way.</summary>
	private float OrbitTerm(int p)
	{
		NVector2 fromEnemy = _planner.Start.Position - _fightAt;
		float length = fromEnemy.Length();
		if (length < 0.01f)
			return 0f;
		NVector2 moved = _planner.End(p) - _planner.Start.Position;
		NVector2 radial = fromEnemy / length;
		return Mathf.Abs(radial.X * moved.Y - radial.Y * moved.X) / _planner.MaxTravel;
	}

	/// <summary>A cost for heading off at an angle to the way it is going: 0 straight on, -1 straight back.</summary>
	private float TurnTerm(int p)
	{
		NVector2 velocity = _planner.Start.Velocity;
		NVector2 moved = _planner.End(p) - _planner.Start.Position;
		if (velocity.LengthSquared() < 4f || moved.LengthSquared() < 0.01f)
			return 0f;
		return -(1f - NVector2.Dot(NVector2.Normalize(velocity), NVector2.Normalize(moved))) * 0.5f;
	}

	// ───────────── Deciding ─────────────

	/// <summary>One decision: the judgements, then the moving, by path scoring.</summary>
	private void Decide(float sinceLast)
	{
		if (_move == null)
			SetUpPlanning();
		Vector2 me = _vnp.PlanePosition;
		float awareness = Mathf.Min(Skill.AwarenessRadius, ScreenReach(me));
		EnemySwarm enemies = _fight.Enemies;
		int enemyCount = OnScreenOnly(enemies.CollectNear(me, awareness, _enemies), _enemies, i => enemies.PositionOf(i));
		// It keeps track of only so many enemies, the nearest: its attention.
		if (Skill.Attention > 0)
			enemyCount = KeepNearest(_enemies, enemyCount, Skill.Attention, i => enemies.PositionOf(i).DistanceTo(me) - enemies.RadiusOf(i));

		// Threat, and which way it comes from.
		float droneReliance = _drones.Cap > 0 ? Mathf.Clamp((float)_drones.Count / _drones.Cap, 0f, 1f) : 0f;
		float threat = 0f;
		Vector2 away = Vector2.Zero;
		int nearest = -1;
		float nearestDistance = float.MaxValue;
		float nearestAnyThreat = float.MaxValue;
		for (int c = 0; c < enemyCount; c++)
		{
			int e = _enemies[c];
			Vector2 offset = me - enemies.PositionOf(e);
			float distance = Mathf.Max(0.5f, offset.Length() - enemies.RadiusOf(e));
			float closeness = Mathf.Clamp(1f - distance / Mathf.Max(1f, Skill.ThreatRange), 0f, 1f);
			nearestAnyThreat = Mathf.Min(nearestAnyThreat, distance);
			float weight = enemies.AbsorbsDronesOf(e) ? Mathf.Lerp(1f, Skill.DroneEaterThreat, droneReliance) : 1f;
			// An idle enemy, which hasn't noticed it, counts for less than one coming for it.
			if (!enemies.IsChasingOf(e))
				weight *= Skill.IdleThreatShare;
			threat += weight * closeness * closeness;
			away += offset / distance * weight * closeness;
			// It picks fights only with enemies coming for it: an idle one is left alone, as the designer does, since
			// going for it wakes it and, through group aggro, its neighbours.
			bool worthApproaching = (!enemies.AbsorbsDronesOf(e) || droneReliance < 0.25f) && enemies.IsChasingOf(e);
			if (worthApproaching && distance < nearestDistance)
			{
				nearestDistance = distance;
				nearest = e;
			}
		}

		float pressure = threat / Mathf.Max(0.01f, Affordable());
		if (Skill.PressureFall > 0f)
			pressure = Mathf.Max(pressure, _heldPressure - Skill.PressureFall * sinceLast);
		_heldPressure = pressure;
		BlendWeaponProfiles(out float preferredDistance, out float keepMoving, out float leadChasers);
		float backOff = Mathf.Clamp((pressure - 0.75f) / 0.5f, 0f, 1f);
		if (!_regrouping && backOff > 0.5f && droneReliance < Skill.RegroupBelow)
			_regrouping = true;
		else if (_regrouping && droneReliance >= Skill.RegroupUntil)
			_regrouping = false;
		if (_regrouping && Skill.RegroupClearRange > 0f)
			backOff = nearestAnyThreat < Skill.RegroupClearRange ? 1f : backOff;
		if (Skill.CommitOrWithdraw && !_regrouping && droneReliance >= Skill.RegroupBelow)
		{
			float dronesLetGo = Tuning.Drones.EngageRange * DroneSwarm.KeepTargetFactor + Skill.WithdrawMargin;
			if (!_withdrawing && backOff >= 0.5f)
			{
				_withdrawing = true;
				_withdrawingFor = 0f;
			}
			else if (_withdrawing)
			{
				_withdrawingFor += sinceLast;
				if (nearestAnyThreat > dronesLetGo || _withdrawingFor > Skill.WithdrawMaxSeconds)
					_withdrawing = false;
			}
			backOff = _withdrawing ? 1f : 0f;
		}
		else
			_withdrawing = false;
		float engage = _regrouping ? 0f : 1f - backOff;

		bool fighting = nearest >= 0 && engage > 0f && nearestDistance < preferredDistance * 2f;
		float fightWeight = Skill.FightWeight * Mathf.Lerp(Skill.FightWithoutDrones, 1f, droneReliance);
		float still = Skill.HoldStillPressure > 0f
			? Mathf.Clamp((Skill.HoldStillPressure * 1.5f - pressure) / (Skill.HoldStillPressure * 0.5f), 0f, 1f)
			: 0f;

		Vector2 harvest = HarvestMove(me, awareness, enemyCount, droneReliance, out bool holding, out float targetWorth,
			out float targetEdge, out float targetDanger, Skill.Attention, Skill.Attention * 2);
		float harvestWeight = Skill.HarvestWeight * (_drones.Count < _drones.Cap ? 1f : Skill.HarvestAtCap);
		float rateMultiplier = _drones.HarvestRate / Mathf.Max(0.001f, Tuning.Harvesting.DroneRate);
		harvestWeight *= Mathf.Max(0f, 1f + Skill.HarvestRateInfluence * (rateMultiplier - 1f));
		harvestWeight *= 1f + Skill.ConcentrationPull * Mathf.Clamp(targetWorth / Mathf.Max(1f, Skill.ConcentrationMass), 0f, 1f);
		harvestWeight /= 1f + backOff * targetDanger * Skill.HarvestDangerWhenBackingOff;
		float tooClose = Mathf.Clamp(nearestAnyThreat / (preferredDistance * 0.5f), 0f, 1f);
		harvestWeight *= tooClose;
		bool hasHarvest = harvest != Vector2.Zero || holding;
		bool committing = harvest != Vector2.Zero && backOff <= 0f && tooClose >= 1f
			&& targetEdge <= _fight.Harvestables.BeamRange + Skill.HarvestCommitDistance
			&& !EnemyAhead(me, harvest.Normalized(), enemyCount, preferredDistance * 1.5f);

		// Perception: what enemies, shots and its weapons declare, on screen and within its awareness, with
		// fleeting hazards noticed by its dodge chance.
		_situation.Clear();
		enemies.DeclareThreats(_situation, me, awareness, OnScreen);
		_fight.Projectiles.DeclareThreats(_situation, me, awareness, OnScreen);
		_fight.FragileShots.DeclareThreats(_situation, me, awareness, OnScreen);
		for (int w = 0; w < _fight.Weapons.Count; w++)
			_fight.Weapons[w].DeclareZones(_situation, w);
		_situation.Overlook(Skill.DodgeChance, Random);

		// Leading chasers across its traps, such as mines, that lie ahead as it moves away from them.
		bool leading = leadChasers > 0.01f && away != Vector2.Zero && nearestDistance < Skill.ThreatRange
			&& PickLureSpot(me, away.Normalized(), out _leadAt);

		bool idle = !holding && !fighting && !hasHarvest && backOff <= 0f && !leading;
		if (idle)
		{
			Vector2 heading = WhenIdle(me, awareness);
			_exploreAt = (me + heading * _vnp.MoveSpeed * Skill.LookAheadSeconds * 2f).ToArbiter();
		}
		else
			_returnTo = null;
		_wasIdle = idle;

		// The paths.
		_planner.Horizon = Mathf.Max(0.2f, Skill.LookAheadSeconds);
		_planner.Steps = Mathf.Max(4, Mathf.RoundToInt(_planner.Horizon / 0.1f));
		_planner.Directions = Mathf.Max(4, Skill.PathDirections);
		_planner.Attention = Skill.Attention;
		_planner.Build(new Arbiter.Motion { Position = me.ToArbiter(), Velocity = _vnp.Velocity.ToArbiter(), Control = Move.ToArbiter() },
			new Arbiter.Mover { MaxSpeed = _vnp.MoveSpeed, Acceleration = Tuning.Vnp.Acceleration, Deceleration = Tuning.Vnp.Deceleration, ControlRate = StickSpeed });
		float keepClear = Skill.TouchAvoidDistance * (1f + Mathf.Max(0f, Skill.LowHealthTouchScale) * LowHealth());
		_planner.Assess(_situation, Tuning.Vnp.HitRadius, keepClear, Tuning.Drones.FlockRadius * 1.5f);

		// What each term aims at, and how much it counts now, from the judgements above.
		_aimAway = away != Vector2.Zero ? away.Normalized().ToArbiter() : NVector2.Zero;
		if (fighting)
		{
			_fightAt = enemies.PositionOf(nearest).ToArbiter();
			_fightVelocity = enemies.VelocityOf(nearest).ToArbiter();
			_fightRadius = enemies.RadiusOf(nearest);
			_fightPreferred = preferredDistance;
			_fightFarShare = Skill.ApproachEnemies * (1f - still);
			_fightStillShare = Skill.ApproachEnemies * still;
		}
		if (hasHarvest && _harvestTargetId != 0)
			AimAtHarvestTarget(me);
		_keepMovingTravel = CircleSpeed;

		_move.SetWeight(_tHarm, Skill.HarmWeight);
		_move.SetWeight(_tClose, Skill.TouchAvoidWeight);
		_move.SetWeight(_tDrones, Skill.DroneDangerWeight * droneReliance);
		_move.SetWeight(_tAway, backOff > 0f && away != Vector2.Zero ? Skill.BackOffWeight * backOff : 0f);
		_move.SetWeight(_tFight, fighting ? fightWeight * engage : 0f);
		_move.SetWeight(_tOrbit, fighting ? Skill.OrbitShare * fightWeight * engage * (1f - still) : 0f);
		_move.SetWeight(_tStill, fighting ? fightWeight * engage * still : 0f);
		_move.SetWeight(_tHarvest, hasHarvest ? harvestWeight : 0f);
		_move.SetWeight(_tLead, leading ? Skill.LeadWeight * leadChasers : 0f);
		_move.SetWeight(_tExplore, idle ? 1f : 0f);
		_move.SetWeight(_tTurn, Skill.TurnCost);
		_move.SetWeight(_tKeepMoving, 0f);
		string goal = idle ? (_returnTo != null ? "returning to remembered material" : "exploring")
			: committing ? "committing to harvest" : holding ? "holding to harvest"
			: fighting && hasHarvest ? "fighting and harvesting" : fighting ? "fighting"
			: hasHarvest ? "heading for material" : backOff > 0f ? "backing off" : "leading chasers";
		if (committing)
		{
			// Close the last few units into beam reach, whatever else it wants, but still clear of hazards.
			foreach (int t in new[] { _tAway, _tFight, _tOrbit, _tStill, _tLead })
				_move.SetWeight(t, 0f);
			_move.SetWeight(_tHarvest, Skill.CommitWeight);
		}
		else if (holding && still >= 1f)
		{
			// Little danger: hold at the rock, leaving the fight to the drones and weapons, but still keep back from
			// an enemy closer than its distance.
			foreach (int t in new[] { _tAway, _tOrbit, _tStill, _tLead })
				_move.SetWeight(t, 0f);
			_fightFarShare = 0f;
			_fightStillShare = 0f;
			_move.SetWeight(_tHarvest, Mathf.Max(harvestWeight, 1f));
		}
		// Weapons that only work while moving, such as Mine Layer, want it circling the rock it harvests, slowly
		// enough that the beam stays on it.
		if (holding && keepMoving > 0.01f)
			_move.SetWeight(_tKeepMoving, keepMoving);

		int best = _move.Choose(_planner.Count);
		Vector2 control = best >= 0 ? _planner.Control(best).ToGodot() : Vector2.Zero;
		if (control.LengthSquared() > 0.0001f)
		{
			float wander = Mathf.DegToRad(Skill.WanderDegrees);
			control = control.Rotated(Rng.Range(-wander, wander));
		}
		_wanted = control.LimitLength(1f);

		_lastTargetDanger = targetDanger;
		Decisions++;
#if TOOLS
		if (Explaining)
			Reasoning = $"PLANNER {goal} | " + Explain(threat, pressure, backOff, droneReliance, fighting ? nearest : -1, nearestDistance,
				harvest, targetWorth, targetEdge, holding, committing, idle, _wanted, enemyCount, me) +
				$" | hazards {_situation.Hazards.Length} | paths: {_move.Explain(3, _planner.Describe)}";
#endif
	}

	/// <summary>Where the harvest target is, and how near it must get: within beam reach, where it holds.</summary>
	private void AimAtHarvestTarget(Vector2 me)
	{
		Harvestables harvestables = _fight.Harvestables;
		int count = harvestables.CollectNear(me, Skill.AwarenessRadius, _material);
		for (int c = 0; c < count; c++)
		{
			int m = _material[c];
			if (harvestables.IdOf(m) != _harvestTargetId)
				continue;
			_harvestAt = harvestables.PositionOf(m).ToArbiter();
			_harvestReach = harvestables.RadiusOf(m) + harvestables.BeamRange * 0.6f;
			return;
		}
	}

	/// <summary>
	/// A trap its weapons declared, such as a mine, that lies ahead as it moves away from the enemies chasing it.
	/// Nearer spots, and ones more squarely ahead, are better.
	/// </summary>
	private bool PickLureSpot(Vector2 me, Vector2 awayFromEnemies, out NVector2 spot)
	{
		spot = NVector2.Zero;
		float bestScore = 0f;
		foreach (Arbiter.Zone zone in _situation.Zones)
		{
			if (zone.Kind != Arbiter.ZoneKind.Trap)
				continue;
			Vector2 toSpot = zone.Area.Position.ToGodot() - me;
			float distance = toSpot.Length();
			if (distance < 2f || distance > 30f)
				continue;
			float ahead = (toSpot / distance).Dot(awayFromEnemies);
			if (ahead < 0.2f)
				continue;
			float score = ahead / (1f + distance / 15f);
			if (score > bestScore)
			{
				bestScore = score;
				spot = zone.Area.Position;
			}
		}
		return bestScore > 0f;
	}
}
