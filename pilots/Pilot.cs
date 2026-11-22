using Godot;
using System.Collections.Generic;

/// <summary>
/// Flies a VNP in place of a player: it chooses a direction to move each moment, and picks from level-up
/// offers. How skilled it is comes from a PilotDefinition, and what it picks from a BuildDefinition.
///
/// It sees only what a player could: enemies, enemy shots and material within its awareness, which is never
/// farther than the screen shows, plus its own health, drones and level. It never reads hidden information,
/// such as when the next wave comes.
///
/// Each decision weighs four wants:
/// - Back off: pull away from a fight it can't afford. Threat comes from each enemy near it, closer ones more,
///   and an enemy that eats drones counts for more while it relies on drones. What it can afford grows with
///   its health, drones and level, and shrinks with caution.
/// - Dodge: move aside from a shot it noticed coming at it.
/// - Fight: while the fight is affordable, keep its preferred distance from the nearest enemy, circling it.
///   It wants this less the fewer drones it has: without them, it would rather rebuild than fight alone.
/// - Harvest: head for the most worthwhile material that is affordable to reach, preferring clusters, and hold
///   still beside it while the beam works. It wants this most while its drones are below their cap, since
///   harvesting builds them, and less at the cap.
/// With nothing to harvest or fight, it either returns to material it remembers seeing, choosing where by the
/// mass there, or explores for more: the more mass it remembers, the likelier it returns.
/// It never retreats through the gateway.
///
/// It flies any VNP it is given, and doesn't assume it is the player's or the only one. For now it only flies
/// the player's VNP in the scenario harness and batches; nothing in a normal fight uses it. Rival VNPs and
/// escort VNPs in the full game are meant to be flown by the same logic.
/// </summary>
public partial class Pilot : Node, IVnpController
{
	public PilotDefinition Skill { get; private set; }
	public BuildDefinition Build { get; private set; }

	public Vector2 Move { get; private set; }

	/// <summary>
	/// Why it moved as it did at its latest decision, in a line a person can read: the threat it felt against what
	/// it could afford, and which wants won. For checking its behavior, such as with the scenario harness's
	/// explain option. Nothing in the game reads it.
	/// </summary>
	public string Reasoning { get; private set; } = "";

	/// <summary>How many decisions it has made, so a reader can tell when there is a new one.</summary>
	public int Decisions { get; private set; }

	private Fight _fight;
	private Vnp _vnp;
	private DroneSwarm _drones;
	private Vector2 _wanted;
	private float _decisionTimer;
	private int _harvestTargetId;
	private Vector2 _exploreHeading = Vector2.Up;
	private float _orbitSign = 1f;
	private bool _regrouping;
	private bool _withdrawing;
	private float _withdrawingFor;
	private float _sinceDecision;
	private float _heldPressure;
	private float _lastTargetDanger;

	// Material it has seen, by ID: where and how much. Forgotten once it is used up, or found gone on return.
	private readonly Dictionary<int, (Vector2 Position, float Mass)> _known = new();
	private readonly HashSet<int> _seen = new();
	// While it has nothing to do: where it is returning to, if anywhere, and how long it has roamed.
	private Vector2? _returnTo;
	private float _roamTime;
	private bool _wasIdle;

	private readonly int[] _enemies = new int[512];
	private readonly int[] _material = new int[512];
	private readonly List<(Vector2 Position, Vector2 Velocity)> _shots = new();

	// How quickly the pilot's "stick" moves to a new direction, in full turns of the stick a second, so its
	// movement is smooth rather than snapping between decisions.
	private const float StickSpeed = 6f;

	/// <summary>Has a new pilot fly a VNP: it steers the VNP and is added to the fight to run each frame.</summary>
	public static Pilot Fly(Fight fight, Vnp vnp, DroneSwarm drones, PilotDefinition skill, BuildDefinition build)
	{
		var pilot = new Pilot
		{
			Name = $"Pilot_{skill.Id}_{build.Id}",
			Skill = skill,
			Build = build,
			_fight = fight,
			_vnp = vnp,
			_drones = drones,
			ProcessPriority = UpdateOrder.Pilots,
		};
		pilot._exploreHeading = Rng.Direction();
		pilot._orbitSign = Rng.Sign();
		vnp.Controller = pilot;
		fight.AddChild(pilot);
		return pilot;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (_vnp.IsDestroyed)
		{
			Move = Vector2.Zero;
			return;
		}

		_decisionTimer -= dt;
		_sinceDecision += dt;
		if (_decisionTimer <= 0f)
		{
			Decide(_sinceDecision);
			_sinceDecision = 0f;
			_decisionTimer = Skill.ReactionSeconds * Rng.Range(0.8f, 1.2f);
		}
		Move = Move.MoveToward(_wanted, StickSpeed * dt);
	}

	// ───────────── Moving ─────────────

	private void Decide(float sinceLast)
	{
		Vector2 me = _vnp.PlanePosition;
		float awareness = Mathf.Min(Skill.AwarenessRadius, ScreenReach(me));
		EnemySwarm enemies = _fight.Enemies;
		int enemyCount = OnScreenOnly(enemies.CollectNear(me, awareness, _enemies), _enemies, i => enemies.PositionOf(i));

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
			threat += weight * closeness * closeness;
			away += offset / distance * weight * closeness;
			// Fight the nearest enemy, but leave drone eaters to the weapons while the drones matter.
			bool worthApproaching = !enemies.AbsorbsDronesOf(e) || droneReliance < 0.25f;
			if (worthApproaching && distance < nearestDistance)
			{
				nearestDistance = distance;
				nearest = e;
			}
		}

		float pressure = threat / Mathf.Max(0.01f, Affordable());
		// Pressure rises at once but falls only at PressureFall a second, so it follows through on backing off.
		if (Skill.PressureFall > 0f)
			pressure = Mathf.Max(pressure, _heldPressure - Skill.PressureFall * sinceLast);
		_heldPressure = pressure;
		BlendWeaponProfiles(out float preferredDistance, out float keepMoving, out float leadChasers);
		// Back off more and more as the fight stops being affordable, fully once it is well past.
		float backOff = Mathf.Clamp((pressure - 0.75f) / 0.5f, 0f, 1f);
		// Regrouping: with few drones, once it backs off it gets well clear and rebuilds drones from a cluster,
		// rather than staying at the edge of the fight flicking between backing off and fighting.
		if (!_regrouping && backOff > 0.5f && droneReliance < Skill.RegroupBelow)
			_regrouping = true;
		else if (_regrouping && droneReliance >= Skill.RegroupUntil)
			_regrouping = false;
		if (_regrouping && Skill.RegroupClearRange > 0f)
			backOff = nearestAnyThreat < Skill.RegroupClearRange ? 1f : backOff;
		// With drones that matter, all or nothing: stay with them, or withdraw far enough that they break off.
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

		Vector2 want = Vector2.Zero;
		if (backOff > 0f && away != Vector2.Zero)
			want += away.Normalized() * Skill.BackOffWeight * backOff;

		Vector2 dodge = Dodge(me, awareness);
		want += dodge * Skill.DodgeWeight;
		// Keep clear of enemies that damage by touch, such as Goos, whatever else it wants: even a fight it can
		// afford is no reason to bump into one.
		Vector2 keepClear = KeepClearOfTouch(me, enemyCount) * Skill.TouchAvoidWeight;
		want += keepClear;

		// An enemy beyond about its weapons' reach (twice its preferred distance) isn't a fight yet: it doesn't go
		// after enemies, so it would only creep toward one on a weak want. Without this, with no material in sight
		// it crept at about 60% speed toward a distant enemy instead of exploring at full speed, which the designer
		// saw as it slowing down. Backing off still answers the threat of enemies farther out.
		bool fighting = nearest >= 0 && engage > 0f && nearestDistance < preferredDistance * 2f;
		float fightWeight = Skill.FightWeight * Mathf.Lerp(Skill.FightWithoutDrones, 1f, droneReliance);
		// With little danger it holds still, as the designer does, rather than circling: fully below
		// HoldStillPressure, blending back to circling by half as much again.
		float still = Skill.HoldStillPressure > 0f
			? Mathf.Clamp((Skill.HoldStillPressure * 1.5f - pressure) / (Skill.HoldStillPressure * 0.5f), 0f, 1f)
			: 0f;
		if (fighting)
			want += FightMove(me, enemies.PositionOf(nearest), nearestDistance, preferredDistance, still) * fightWeight * engage;

		// Weapons that leave spots behind, such as mines, want the enemies chasing it led across them.
		if (leadChasers > 0.01f && away != Vector2.Zero && nearestDistance < Skill.ThreatRange)
			want += LeadAcrossSpots(me, away.Normalized()) * Skill.LeadWeight * leadChasers;

		// Harvesting: toward the best material, and holding still beside it once the beam reaches. It carries on
		// while backing off, since material near enemies is already scored down: a hurt pilot keeps harvesting
		// rocks away from the fight, as a player would, rather than only fleeing.
		Vector2 harvest = HarvestMove(me, awareness, enemyCount, droneReliance, out bool holding, out float targetWorth,
			out float targetEdge, out float targetDanger);
		float harvestWeight = Skill.HarvestWeight * (_drones.Count < _drones.Cap ? 1f : Skill.HarvestAtCap);
		// Harvest rate upgrades make harvesting worth more of its time.
		float rateMultiplier = _drones.HarvestRate / Mathf.Max(0.001f, Tuning.Harvesting.DroneRate);
		harvestWeight *= Mathf.Max(0f, 1f + Skill.HarvestRateInfluence * (rateMultiplier - 1f));
		// A rich concentration pulls harder than a lone rock.
		harvestWeight *= 1f + Skill.ConcentrationPull * Mathf.Clamp(targetWorth / Mathf.Max(1f, Skill.ConcentrationMass), 0f, 1f);
		// While backing off, material with enemies around it stops pulling: it keeps harvesting rocks away from the
		// fight, but doesn't head into the danger it is backing away from for the rich debris inside it. Before
		// this, it flew into a crowd of Goos for the debris among them, and was destroyed in half a second.
		harvestWeight /= 1f + backOff * targetDanger * Skill.HarvestDangerWhenBackingOff;
		// An enemy inside half its preferred distance: harvesting pulls less the closer it is, so keeping back
		// wins. Before, a pull toward material past an enemy carried it right up to the enemy, which the designer
		// saw as it moving closer to enemies for no reason.
		// Every enemy counts here, drone eaters too: it doesn't fight those while its drones matter, but it mustn't
		// harvest beside one either. Before, a Goo 2.6 away didn't count and it committed to a rock next to it.
		float tooClose = Mathf.Clamp(nearestAnyThreat / (preferredDistance * 0.5f), 0f, 1f);
		harvestWeight *= tooClose;
		want += harvest * harvestWeight;

		// Close to its target and not backing off: commit, and close the last few units into beam reach. Blended
		// with the other wants, the pull toward a nearby rock could balance against keeping fighting distance,
		// leaving it hovering just out of reach in the middle of a concentration.
		bool committing = harvest != Vector2.Zero && backOff <= 0f && tooClose >= 1f
			&& targetEdge <= _fight.Harvestables.BeamRange + Skill.HarvestCommitDistance
			&& !EnemyAhead(me, harvest.Normalized(), enemyCount, preferredDistance * 1.5f);
		if (committing)
			want = harvest + dodge * Skill.DodgeWeight + keepClear;

		// Harvesting with little danger about: keep holding at the rock, leaving the fight to the drones and weapons.
		// Weapons that need movement then circle the rock, below.
		if (holding && still >= 1f)
		{
			want = dodge * Skill.DodgeWeight + keepClear;
			// Still keep back from an enemy closer than its preferred distance. Without this, holding still dropped
			// the fight's keeping back, and the designer saw the pilot let enemies come right up to it.
			if (nearest >= 0 && nearestDistance < preferredDistance)
			{
				Vector2 fromEnemy = me - enemies.PositionOf(nearest);
				if (fromEnemy.LengthSquared() > 0.0001f)
					want += fromEnemy.Normalized() * (1f - nearestDistance / preferredDistance) * fightWeight;
			}
		}

		// Weapons that only work while moving, such as Mine Layer, want it circling the rock it harvests rather
		// than holding still beside it.
		bool circling = false;
		if (holding && keepMoving > 0.01f && want.LengthSquared() < 0.01f)
		{
			want = CircleTarget(me, nearest >= 0 ? enemies.PositionOf(nearest) : (Vector2?)null);
			circling = want != Vector2.Zero;
		}

		bool idle = want.LengthSquared() < 0.01f && !holding && !fighting;
		if (idle)
			want = WhenIdle(me, awareness);
		else
			_returnTo = null;
		_wasIdle = idle;

		_lastTargetDanger = targetDanger;
		Decisions++;
		Reasoning = Explain(threat, pressure, backOff, droneReliance, fighting ? nearest : -1, nearestDistance,
			harvest, targetWorth, targetEdge, holding, committing, idle, dodge, want, enemyCount, me);

		if (want.LengthSquared() > 0.0001f)
		{
			// The weights above decide which way to go. How fast: full speed, as a player holds the stick right
			// over, with two exceptions. It eases off as it arrives at the material it is about to harvest, so it
			// doesn't overshoot. And when its wants balance out, with no clear direction, it slows to match.
			// Speed used to follow the strength of its wants, and wants pulling different ways, such as rocks
			// ahead and enemies behind, left it crawling: below 80% of top speed for two-thirds of a fight.
			bool arriving = harvest != Vector2.Zero && (committing || (!fighting && backOff <= 0f && dodge == Vector2.Zero));
			const float ClearDirection = 0.25f;
			// Circling a rock goes slowly, just fast enough for weapons that need movement (Mine Layer drops mines
			// above 3 units a second), so the beam stays on the rock. Faster circling, at up to 45% of top speed,
			// cost about a tenth of the fight experience by 2:00 in a batch.
			float speed = circling ? CircleSpeed
				: arriving ? harvest.Length()
				: Mathf.Min(1f, want.Length() / ClearDirection);
			float wander = Mathf.DegToRad(Skill.WanderDegrees);
			want = want.Normalized().Rotated(Rng.Range(-wander, wander)) * speed;
		}
		_wanted = want.LimitLength(1f);
	}

	private string Explain(float threat, float pressure, float backOff, float droneReliance, int fightTarget,
		float fightDistance, Vector2 harvest, float targetWorth, float targetEdge, bool holding, bool committing,
		bool idle, Vector2 dodge, Vector2 want, int enemyCount, Vector2 me)
	{
		EnemySwarm enemies = _fight.Enemies;
		int drainers = 0;
		float nearestDrainer = float.MaxValue;
		float nearestAny = float.MaxValue;
		Vector2 towardNearest = Vector2.Zero;
		for (int c = 0; c < enemyCount; c++)
		{
			int e = _enemies[c];
			float edge = me.DistanceTo(enemies.PositionOf(e)) - enemies.RadiusOf(e);
			if (edge < nearestAny)
			{
				nearestAny = edge;
				towardNearest = (enemies.PositionOf(e) - me).Normalized();
			}
			if (!enemies.AbsorbsDronesOf(e))
				continue;
			drainers++;
			nearestDrainer = Mathf.Min(nearestDrainer, me.DistanceTo(enemies.PositionOf(e)) - enemies.RadiusOf(e));
		}
		string doing = committing ? $"committing to harvest (edge {targetEdge:0.0})"
			: holding ? "holding to harvest"
			: idle ? (_returnTo != null ? "returning to remembered material" : "exploring")
			: harvest != Vector2.Zero ? $"heading for material (worth {targetWorth:0}, edge {targetEdge:0}, danger {_lastTargetDanger:0.0})"
			: "no material in sight";
		string fight = fightTarget >= 0 ? $"fighting {enemies.TypeIdOf(fightTarget)} at {fightDistance:0.0}" : "not fighting";
		return $"threat {threat:0.00} / affordable {Affordable():0.00} = pressure {pressure:0.00}, back off {backOff:0.00}" +
			(_regrouping ? " | regrouping" : "") + (_withdrawing ? " | withdrawing" : "") +
			$" | {fight} | {doing}" + (dodge != Vector2.Zero ? " | dodging" : "") +
			$" | drones {_drones.Count} (reliance {droneReliance:0.00}) | drone eaters on screen {drainers}" +
			(drainers > 0 ? $", nearest edge {nearestDrainer:0.0}" : "") +
			$" | health {_vnp.Health:0} | stick {want.Length():0.00}" +
			(enemyCount > 0 && want != Vector2.Zero
				? $" | nearest enemy {nearestAny:0.0} away, heading toward it {want.Normalized().Dot(towardNearest):+0.00;-0.00}"
				: "") +
			$" | weapons: distance {_lastProfile.Distance:0}, keep moving {_lastProfile.KeepMoving:0.00}, lead {_lastProfile.Lead:0.00}";
	}

	/// <summary>
	/// Blends the pilot profiles of the VNP Weapons it has equipped, each weighted by how invested it is in that
	/// weapon: 1 for having it, plus 1 for each upgrade taken for it. Then scales the blend by TacticSkill: the
	/// preferred distance moves from its own toward the weapons', and the other behaviors grow from nothing.
	/// </summary>
	private void BlendWeaponProfiles(out float preferredDistance, out float keepMoving, out float leadChasers)
	{
		float distanceSum = 0f, lowDistanceSum = 0f, distanceWeight = 0f, keepSum = 0f, leadSum = 0f, total = 0f;
		foreach (VnpWeapon weapon in _fight.Weapons)
		{
			WeaponPilotProfile profile = weapon.Definition.PilotProfile;
			float investment = 1f;
			foreach (UpgradeDefinition upgrade in _fight.TakenUpgrades)
			{
				if (upgrade.Weapon != null && upgrade.Weapon.Id == weapon.Definition.Id
					&& upgrade.Effect != UpgradeEffect.EquipWeapon)
					investment += 1f;
			}
			total += investment;
			if (profile == null)
				continue;
			// Its distance follows the weapon's reach now, with upgrades, where the profile says so.
			float reach = weapon.Reach;
			bool byReach = reach > 0f && profile.RangeFraction > 0f;
			float distance = byReach ? reach * profile.RangeFraction : profile.PreferredDistance;
			if (distance > 0f)
			{
				float low = byReach && profile.LowHealthRangeFraction > 0f ? reach * profile.LowHealthRangeFraction
					: !byReach && profile.LowHealthDistance > 0f ? profile.LowHealthDistance : distance;
				distanceSum += distance * investment;
				lowDistanceSum += low * investment;
				distanceWeight += investment;
			}
			keepSum += profile.KeepMoving * investment;
			leadSum += profile.LeadChasers * investment;
		}
		float tactic = Mathf.Clamp(Skill.TacticSkill, 0f, 1f);
		float weaponsDistance = distanceWeight > 0f ? distanceSum / distanceWeight : Skill.PreferredEnemyDistance;
		// At low health, the weapons' wider low-health distances blend in.
		if (distanceWeight > 0f)
			weaponsDistance = Mathf.Lerp(weaponsDistance, lowDistanceSum / distanceWeight, LowHealth());
		preferredDistance = Mathf.Lerp(Skill.PreferredEnemyDistance, weaponsDistance, tactic);
		keepMoving = total > 0f ? keepSum / total * tactic : 0f;
		leadChasers = total > 0f ? leadSum / total * tactic : 0f;
		_lastProfile = (preferredDistance, keepMoving, leadChasers);
	}

	private (float Distance, float KeepMoving, float Lead) _lastProfile;

	// Stick strength while circling a rock: 0.2 of top speed, about 4 units a second at the VNP's base speed.
	private const float CircleSpeed = 0.2f;
	private readonly List<Vector2> _lureSpots = new();

	/// <summary>
	/// Toward one of its weapons' lure spots, such as a mine, that lies ahead as it moves away from the enemies
	/// chasing it, so they follow it across. Nearer spots, and ones more squarely ahead, are better. Nothing if
	/// no spot lies ahead.
	/// </summary>
	private Vector2 LeadAcrossSpots(Vector2 me, Vector2 awayFromEnemies)
	{
		_lureSpots.Clear();
		foreach (VnpWeapon weapon in _fight.Weapons)
		{
			if (weapon is IPilotLure lure)
				lure.CollectLureSpots(_lureSpots);
		}
		Vector2 best = Vector2.Zero;
		float bestScore = 0f;
		foreach (Vector2 spot in _lureSpots)
		{
			Vector2 toSpot = spot - me;
			float distance = toSpot.Length();
			if (distance < 2f || distance > 30f)
				continue; // On top of it already, or too far to lead anything across.
			float ahead = (toSpot / distance).Dot(awayFromEnemies);
			if (ahead < 0.2f)
				continue;
			float score = ahead / (1f + distance / 15f);
			if (score > bestScore)
			{
				bestScore = score;
				best = toSpot / distance;
			}
		}
		return best;
	}

	/// <summary>Around the rock it is harvesting, staying within beam reach, instead of holding still.</summary>
	private Vector2 CircleTarget(Vector2 me, Vector2? enemy)
	{
		Harvestables harvestables = _fight.Harvestables;
		int count = harvestables.CollectNear(me, harvestables.BeamRange + 20f, _material);
		for (int c = 0; c < count; c++)
		{
			int m = _material[c];
			if (harvestables.IdOf(m) != _harvestTargetId)
				continue;
			Vector2 fromRock = me - harvestables.PositionOf(m);
			float distance = fromRock.Length();
			if (distance < 0.01f)
				return Vector2.Right;
			Vector2 outward = fromRock / distance;
			// Keep about half the beam's reach from its edge: drift in or out toward that while going round.
			float wanted = harvestables.RadiusOf(m) + harvestables.BeamRange * 0.5f;
			float radial = Mathf.Clamp((wanted - distance) / 3f, -1f, 1f);
			// Go round whichever way leads away from the nearest enemy, rather than always the same way, which
			// could carry it round the rock straight toward an enemy.
			Vector2 around = new Vector2(-outward.Y, outward.X) * _orbitSign;
			if (enemy is Vector2 threat && around.Dot(threat - me) > 0f)
			{
				_orbitSign = -_orbitSign;
				around = -around;
			}
			return (around + outward * radial).Normalized();
		}
		return Vector2.Zero;
	}

	/// <summary>The threat it can afford right now: more with health, drones and level, less with caution.</summary>
	private float Affordable()
	{
		float health = _vnp.MaxHealth > 0f ? _vnp.Health / _vnp.MaxHealth : 0f;
		float strength = 1f + Skill.StrengthPerDrone * _drones.Count + Skill.StrengthPerLevel * (_fight.Level - 1);
		return Skill.AffordableThreat * health * strength * (1f - 0.5f * Skill.Caution);
	}

	/// <summary>How low its health is, from 0 at LowHealthBelow of its most (or above) to 1 at none.</summary>
	private float LowHealth()
	{
		if (Skill.LowHealthBelow <= 0f || _vnp.MaxHealth <= 0f)
			return 0f;
		float health = _vnp.Health / _vnp.MaxHealth;
		return Mathf.Clamp((Skill.LowHealthBelow - health) / Skill.LowHealthBelow, 0f, 1f);
	}

	/// <summary>
	/// Away from the enemies it sees that damage by touch, such as Goos, from each within its keep-clear distance
	/// of its edge, harder the closer it is, fully once touching. The distance is TouchAvoidDistance, wider at low
	/// health (LowHealthTouchScale). Each is judged where it is and where it will be TouchLookAhead seconds on,
	/// with the VNP where it is heading then too, by whichever is closer, so an enemy closing in head-on is
	/// avoided in time. At most length 1.
	/// </summary>
	private Vector2 KeepClearOfTouch(Vector2 me, int enemyCount)
	{
		if (Skill.TouchAvoidDistance <= 0f || Skill.TouchAvoidWeight <= 0f)
			return Vector2.Zero;
		float keepClear = Skill.TouchAvoidDistance * (1f + Mathf.Max(0f, Skill.LowHealthTouchScale) * LowHealth());
		float ahead = Mathf.Max(0f, Skill.TouchLookAhead);
		Vector2 meAhead = me + _vnp.Velocity * ahead;
		EnemySwarm enemies = _fight.Enemies;
		Vector2 push = Vector2.Zero;
		for (int c = 0; c < enemyCount; c++)
		{
			int e = _enemies[c];
			if (!enemies.DamagesByTouchOf(e))
				continue;
			Vector2 away = me - enemies.PositionOf(e);
			float distance = away.Length();
			if (ahead > 0f)
			{
				Vector2 awayAhead = meAhead - (enemies.PositionOf(e) + enemies.VelocityOf(e) * ahead);
				if (awayAhead.Length() < distance)
				{
					away = awayAhead;
					distance = awayAhead.Length();
				}
			}
			float edge = distance - enemies.RadiusOf(e);
			if (edge >= keepClear)
				continue;
			Vector2 direction = distance > 0.001f ? away / distance : Vector2.Right;
			push += direction * (1f - Mathf.Max(0f, edge) / keepClear);
		}
		return push.LimitLength(1f);
	}

	/// <summary>Whether an enemy lies close ahead in a direction, within a distance of its edge.</summary>
	private bool EnemyAhead(Vector2 me, Vector2 direction, int enemyCount, float within)
	{
		EnemySwarm enemies = _fight.Enemies;
		for (int c = 0; c < enemyCount; c++)
		{
			int e = _enemies[c];
			Vector2 toEnemy = enemies.PositionOf(e) - me;
			float distance = toEnemy.Length();
			if (distance - enemies.RadiusOf(e) < within && distance > 0.001f && (toEnemy / distance).Dot(direction) > 0.7f)
				return true;
		}
		return false;
	}

	/// <summary>How far a point is from the line segment between two others.</summary>
	private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
	{
		Vector2 along = to - from;
		float lengthSquared = along.LengthSquared();
		float t = lengthSquared > 0.0001f ? Mathf.Clamp((point - from).Dot(along) / lengthSquared, 0f, 1f) : 0f;
		return point.DistanceTo(from + along * t);
	}

	/// <summary>Sideways away from a shot it noticed that would hit it soon, or nothing.</summary>
	private Vector2 Dodge(Vector2 me, float awareness)
	{
		Vector2 dodge = Vector2.Zero;
		_fight.Projectiles.CollectEnemyShots(me, awareness, _shots);
		foreach ((Vector2 position, Vector2 velocity) in _shots)
		{
			if (!OnScreen(position))
				continue;
			float speedSquared = velocity.LengthSquared();
			if (speedSquared < 0.01f)
				continue;
			// When the shot passes closest, and how close.
			Vector2 toMe = me - position;
			float time = toMe.Dot(velocity) / speedSquared;
			if (time <= 0f || time > 1.2f)
				continue;
			Vector2 miss = toMe - velocity * time;
			if (miss.Length() > Tuning.Vnp.HitRadius + 1.5f || Rng.Value() > Skill.DodgeChance)
				continue;
			Vector2 side = miss.LengthSquared() > 0.01f ? miss.Normalized() : new Vector2(-velocity.Y, velocity.X).Normalized();
			dodge += side * (1f - time / 1.2f);
		}
		return dodge.LimitLength(1f);
	}

	/// <summary>Keep the preferred distance from an enemy: close in from farther, edge away from closer, circling it.</summary>
	private Vector2 FightMove(Vector2 me, Vector2 enemy, float distance, float preferred, float still)
	{
		Vector2 toEnemy = enemy - me;
		Vector2 direction = toEnemy.LengthSquared() > 0.0001f ? toEnemy.Normalized() : Vector2.Up;
		float radial = Mathf.Clamp((distance - preferred) / (preferred * 0.5f), -1f, 1f);
		// Closing in on an enemy farther than its preferred distance is scaled by ApproachEnemies; keeping back
		// from one that is too close is not.
		if (radial > 0f)
			radial *= Skill.ApproachEnemies;
		Vector2 circle = new Vector2(-direction.Y, direction.X) * _orbitSign * 0.6f;
		Vector2 moving = direction * radial + circle;
		// Holding still: only close in on an enemy well beyond its preferred distance, so the weapons reach it.
		Vector2 holdingStill = distance > preferred * 1.5f ? direction * radial : Vector2.Zero;
		return moving.Lerp(holdingStill, still);
	}

	/// <summary>
	/// Toward the best material it can afford to reach: more mass is better, farther is worse, and material
	/// with enemies around it is discounted by caution. Once the beam reaches it, it holds still.
	/// </summary>
	private Vector2 HarvestMove(Vector2 me, float awareness, int enemyCount, float droneReliance, out bool holding,
		out float targetWorth, out float targetEdge, out float targetDanger)
	{
		holding = false;
		targetWorth = 0f;
		targetEdge = float.MaxValue;
		targetDanger = 0f;
		Harvestables harvestables = _fight.Harvestables;
		int count = OnScreenOnly(harvestables.CollectNear(me, awareness, _material), _material, i => harvestables.PositionOf(i));
		Remember(harvestables, me, awareness, count);
		int best = -1;
		float bestScore = 0f;
		float bestWorth = 0f;
		float bestDanger = 0f;
		EnemySwarm enemies = _fight.Enemies;
		float clusterRadiusSquared = Skill.ClusterRadius * Skill.ClusterRadius;
		for (int c = 0; c < count; c++)
		{
			int m = _material[c];
			Vector2 position = harvestables.PositionOf(m);
			float distance = harvestables.EdgeDistance(m, me);

			// Its worth: its own mass, plus part of the material around it, so clusters beat lone rocks. Nearby
			// asteroids count more than nearby debris: debris piles up where enemies died, usually in the middle of a
			// fight, and counting it as fully as asteroids drew pilots into fights and cost them their drones.
			float worth = harvestables.MassOf(m) * ValueOf(harvestables, m);
			if (Skill.ClusterWeight > 0f || Skill.DebrisClusterWeight > 0f)
			{
				for (int o = 0; o < count; o++)
				{
					int other = _material[o];
					if (other == m || harvestables.PositionOf(other).DistanceSquaredTo(position) > clusterRadiusSquared)
						continue;
					float share = harvestables.IsAsteroidOf(other) ? Skill.ClusterWeight : Skill.DebrisClusterWeight;
					worth += share * harvestables.MassOf(other) * ValueOf(harvestables, other);
				}
			}
			// Regrouping wants a rich cluster to rebuild drones, worth travelling for, over a nearby small rock.
			float score = (_regrouping ? Mathf.Max(0.01f, worth) : Mathf.Sqrt(Mathf.Max(0.01f, worth))) / (distance + 10f);
			if (harvestables.IdOf(m) == _harvestTargetId)
				score *= 1.5f; // Finish what it started, rather than dithering between rocks.

			float danger = 0f;
			for (int e = 0; e < enemyCount; e++)
			{
				int enemy = _enemies[e];
				Vector2 enemyPosition = enemies.PositionOf(enemy);
				float near = Mathf.Clamp(1f - enemyPosition.DistanceTo(position) / 12f, 0f, 1f);
				// An enemy on the way there counts too, for PathDanger as much: reaching it means flying past it.
				if (Skill.PathDanger > 0f)
				{
					float offPath = DistanceToSegment(enemyPosition, me, position) - enemies.RadiusOf(enemy);
					near = Mathf.Max(near, Mathf.Clamp(1f - offPath / 8f, 0f, 1f) * Skill.PathDanger);
				}
				danger += near * (enemies.AbsorbsDronesOf(enemy) ? Mathf.Lerp(1f, Skill.DroneEaterThreat, droneReliance) : 1f);
			}
			score /= 1f + danger * Skill.Caution;

			if (score > bestScore)
			{
				bestScore = score;
				best = m;
				bestWorth = worth;
				bestDanger = danger;
			}
		}
		if (best < 0)
		{
			_harvestTargetId = 0;
			return Vector2.Zero;
		}

		_harvestTargetId = harvestables.IdOf(best);
		targetWorth = bestWorth;
		targetDanger = bestDanger;
		float edge = harvestables.EdgeDistance(best, me);
		targetEdge = edge;
		if (edge <= harvestables.BeamRange * 0.6f)
		{
			holding = true;
			return Vector2.Zero;
		}
		Vector2 toward = harvestables.PositionOf(best) - me;
		// Ease in as it arrives, so it stops beside the material rather than overshooting.
		float urgency = Mathf.Clamp((edge - harvestables.BeamRange * 0.6f) / 6f, 0.3f, 1f);
		return toward.Normalized() * urgency;
	}

	/// <summary>
	/// Keeps track of material it has seen: adds or updates what is in sight, and forgets what it should see but
	/// doesn't, because it was used up. Material moves with its field, so a remembered spot is approximate.
	/// </summary>
	private void Remember(Harvestables harvestables, Vector2 me, float awareness, int count)
	{
		_seen.Clear();
		for (int c = 0; c < count; c++)
		{
			int m = _material[c];
			int id = harvestables.IdOf(m);
			_seen.Add(id);
			_known[id] = (harvestables.PositionOf(m), harvestables.MassOf(m));
		}
		// Well inside its awareness and not seen: gone. Near the edge, it may just have drifted out of sight.
		float forgetWithin = awareness * 0.8f;
		List<int> gone = null;
		foreach ((int id, (Vector2 position, float _)) in _known)
		{
			if (!_seen.Contains(id) && position.DistanceTo(me) < forgetWithin && OnScreen(position))
				(gone ??= new List<int>()).Add(id);
		}
		if (gone != null)
		{
			foreach (int id in gone)
				_known.Remove(id);
		}
	}

	/// <summary>
	/// With nothing to fight or harvest: return to remembered material, or explore for more. It decides when it
	/// first has nothing to do, and again every so often while roaming. The chance of returning grows with the
	/// mass it remembers, and where it returns to is picked in proportion to the mass there.
	/// </summary>
	private Vector2 WhenIdle(Vector2 me, float awareness)
	{
		_roamTime += Skill.ReactionSeconds;
		if (!_wasIdle || (_returnTo == null && _roamTime >= Skill.ExploreRethinkSeconds))
		{
			_roamTime = 0f;
			_returnTo = PickReturn();
		}

		if (_returnTo is Vector2 spot)
		{
			// Arrived, and nothing here to harvest: forgotten already by Remember, so pick again next time.
			if (spot.DistanceTo(me) < awareness * 0.5f)
			{
				_returnTo = null;
				_roamTime = Skill.ExploreRethinkSeconds;
			}
			else
			{
				return (spot - me).Normalized();
			}
		}

		_exploreHeading = _exploreHeading.Rotated(Rng.Range(-0.35f, 0.35f));
		return _exploreHeading;
	}

	/// <summary>
	/// Where to return to, or null to explore instead. Each remembered piece of material counts by its mass,
	/// scaled down by distance: one ReturnHalfDistance away counts half as much as one beside the pilot. The
	/// chance of returning at all grows with what the remembered material counts for, and where it returns to
	/// is picked in proportion to it.
	/// </summary>
	private Vector2? PickReturn()
	{
		Vector2 me = _vnp.PlanePosition;
		float halfDistance = Mathf.Max(1f, Skill.ReturnHalfDistance);
		float Worth(Vector2 position, float mass) => mass / (1f + position.DistanceTo(me) / halfDistance);

		float total = 0f;
		foreach ((Vector2 position, float mass) in _known.Values)
			total += Worth(position, mass);
		if (total <= 0f || Rng.Value() >= total / (total + Mathf.Max(0.001f, Skill.ExploreUrge)))
			return null;

		float roll = Rng.Range(0f, total);
		foreach ((Vector2 position, float mass) in _known.Values)
		{
			roll -= Worth(position, mass);
			if (roll <= 0f)
				return position;
		}
		return null;
	}

	/// <summary>How far the farthest point on screen is from the VNP: the most it could see in any direction.
	/// What it actually sees is then narrowed to what is on screen.</summary>
	private float ScreenReach(Vector2 me)
	{
		CameraRig camera = _fight.Camera;
		return camera == null ? Skill.AwarenessRadius : Mathf.Max(5f, camera.FarthestOnScreen(me));
	}

	/// <summary>
	/// How much a unit of this material's mass is worth to it: 1 for asteroids, and for debris, more as Debris
	/// reward upgrades make debris give more fight experience.
	/// </summary>
	private float ValueOf(Harvestables harvestables, int item)
	{
		if (harvestables.IsAsteroidOf(item))
			return 1f;
		return Mathf.Max(0f, 1f + Skill.DebrisRewardInfluence * (_fight.DebrisExperience - 1f));
	}

	/// <summary>Whether a point is on screen, as a player would see it.</summary>
	private bool OnScreen(Vector2 point) => _fight.Camera == null || _fight.Camera.IsOnScreen(point);

	/// <summary>Keeps only the found items that are on screen, in their order, and returns how many are left.</summary>
	private int OnScreenOnly(int count, int[] items, System.Func<int, Vector2> positionOf)
	{
		int kept = 0;
		for (int c = 0; c < count; c++)
		{
			if (OnScreen(positionOf(items[c])))
				items[kept++] = items[c];
		}
		return kept;
	}

	// ───────────── Choosing upgrades ─────────────

	/// <summary>
	/// Picks from a level-up offer by the build's weights, with some randomness. Returns the index of the
	/// option to take, or -1 to reroll: when nothing scores well enough, a reroll is left, and it hasn't
	/// rerolled this offer yet.
	/// </summary>
	public int ChooseUpgrade(IReadOnlyList<UpgradeDefinition> options, int rerollsLeft, bool alreadyRerolled)
	{
		int best = -1;
		float bestScore = float.MinValue;
		for (int i = 0; i < options.Count; i++)
		{
			float score = Score(options[i]) + Rng.Range(0f, Build.Randomness + Skill.PickNoise);
			if (score > bestScore)
			{
				bestScore = score;
				best = i;
			}
		}
		if (rerollsLeft > 0 && !alreadyRerolled && Build.RerollBelow > 0f && bestScore < Build.RerollBelow)
			return -1;
		return best;
	}

	private float Score(UpgradeDefinition upgrade)
	{
		float score = Build.UpgradeWeights.TryGetValue(upgrade.Id, out float own)
			? own
			: upgrade.Category switch
			{
				UpgradeCategory.VnpWeapon => Build.VnpWeaponWeight,
				UpgradeCategory.DroneWeapon => Build.DroneWeaponWeight,
				UpgradeCategory.Stats => Build.StatsWeight,
				_ => Build.DronesWeight,
			};
		if (ImprovesOwned(upgrade))
			score += Build.FavourOwned;
		if (upgrade.Effect is UpgradeEffect.EquipWeapon or UpgradeEffect.SwitchDroneWeapon)
			score += Build.NewWeaponWeight;
		return score;
	}

	/// <summary>Whether an upgrade improves something the VNP already has: an equipped weapon, or its drones.</summary>
	private bool ImprovesOwned(UpgradeDefinition upgrade) => upgrade.Effect switch
	{
		UpgradeEffect.ScaleWeaponStat or UpgradeEffect.AddWeaponStat or UpgradeEffect.UnlockWeaponFeature
			=> _fight.FindWeapon(upgrade.Weapon) != null,
		UpgradeEffect.ScaleStat or UpgradeEffect.AddStat
			=> upgrade.Stat is PlayerStat.DroneDamage or PlayerStat.DroneAttackRate && _drones.Count > 0,
		_ => false,
	};
}
