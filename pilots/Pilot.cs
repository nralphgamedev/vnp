using Godot;
using System.Collections.Generic;

/// <summary>
/// Flies a VNP in place of a player: it chooses a direction to move each moment, and picks from level-up
/// offers. How skilled it is comes from a PilotDefinition, and what it picks from a BuildDefinition.
///
/// It sees only what a player could: what enemies, shots and its weapons declare within its awareness and on
/// screen, plus its own health, drones and level. It never reads hidden information, such as when the next wave
/// comes.
///
/// Each decision makes its judgements here (whether it can afford the fight, whether to regroup or withdraw, what
/// to harvest, whether to commit or hold still, where to explore), then scores short candidate paths by them and
/// follows the best (Pilot.Planning.cs, docs/dev/PILOT_SPEC.md). It never retreats through the gateway.
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

#if TOOLS
	/// <summary>
	/// Why it moved as it did at its latest decision, in a line a person can read: the threat it felt against what
	/// it could afford, and which wants won. A testing aid for the scenario harness's explain option, so it is
	/// editor-only, and written only while <see cref="Explaining"/> is on: building it at every decision was the
	/// pilot's largest garbage. Nothing in the game reads it.
	/// </summary>
	public string Reasoning { get; private set; } = "";

	/// <summary>Whether to write <see cref="Reasoning"/> at each decision. The harness turns it on for explain=.</summary>
	public bool Explaining { get; set; }
#endif

	/// <summary>How many decisions it has made, so a reader can tell when there is a new one.</summary>
	public int Decisions { get; private set; }

	private Fight _fight;
	private Vnp _vnp;
	private DroneSwarm _drones;
	private Vector2 _wanted;
	private float _decisionTimer;
	private int _harvestTargetId;
	private Vector2 _exploreHeading = Vector2.Up;
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

#if TOOLS
	private string Explain(float threat, float pressure, float backOff, float droneReliance, int fightTarget,
		float fightDistance, Vector2 harvest, float targetWorth, float targetEdge, bool holding, bool committing,
		bool idle, Vector2 want, int enemyCount, Vector2 me)
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
			$" | {fight} | {doing}" +
			$" | drones {_drones.Count} (reliance {droneReliance:0.00}) | drone eaters on screen {drainers}" +
			(drainers > 0 ? $", nearest edge {nearestDrainer:0.0}" : "") +
			$" | health {_vnp.Health:0} | stick {want.Length():0.00}" +
			(enemyCount > 0 && want != Vector2.Zero
				? $" | nearest enemy {nearestAny:0.0} away, heading toward it {want.Normalized().Dot(towardNearest):+0.00;-0.00}"
				: "") +
			$" | weapons: distance {_lastProfile.Distance:0}, keep moving {_lastProfile.KeepMoving:0.00}, lead {_lastProfile.Lead:0.00}";
	}
#endif

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
#if TOOLS
		_lastProfile = (preferredDistance, keepMoving, leadChasers);
#endif
	}

#if TOOLS
	private (float Distance, float KeepMoving, float Lead) _lastProfile;
#endif

	// How far a path goes while circling a rock, as a share of top speed: about 4 units a second at the VNP's base
	// speed, just above what Mine Layer needs to drop mines, so the beam stays on the rock.
	private const float CircleSpeed = 0.2f;

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

	/// <summary>
	/// Toward the best material it can afford to reach: more mass is better, farther is worse, and material
	/// with enemies around it is discounted by caution. Once the beam reaches it, it holds still.
	/// </summary>
	// HarvestMove's working space: each candidate's worth, and its score before danger, by which it is sorted.
	private readonly float[] _harvestWorth = new float[512];
	private readonly float[] _harvestBound = new float[512];
	private readonly int[] _harvestOrder = new int[512];

	private Vector2 HarvestMove(Vector2 me, float awareness, int enemyCount, float droneReliance, out bool holding,
		out float targetWorth, out float targetEdge, out float targetDanger, int weighAtMost = 0, int considerAtMost = 0)
	{
		holding = false;
		targetWorth = 0f;
		targetEdge = float.MaxValue;
		targetDanger = 0f;
		Harvestables harvestables = _fight.Harvestables;
		int count = OnScreenOnly(harvestables.CollectNear(me, awareness, _material), _material, i => harvestables.PositionOf(i));
		Remember(harvestables, me, awareness, count);
		// It considers only the nearest material (considerAtMost), after remembering all it sees.
		if (considerAtMost > 0 && count > considerAtMost)
			count = KeepNearest(_material, count, considerAtMost, i => harvestables.EdgeDistance(i, me));
		int best = -1;
		float bestScore = 0f;
		float bestWorth = 0f;
		float bestDanger = 0f;
		EnemySwarm enemies = _fight.Enemies;
		float clusterRadiusSquared = Skill.ClusterRadius * Skill.ClusterRadius;
		// First each material's score before danger, which danger can only lower. Then danger is weighed from the best
		// of those down, stopping once none left could beat the best found: the same choice as weighing them all, at a
		// fraction of the cost beside a crowd, where weighing every rock against every enemy took over 10 ms.
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
			_harvestWorth[c] = worth;
			_harvestOrder[c] = c;
			_harvestBound[c] = -score; // Sorted lowest first, so the best comes first.
		}
		System.Array.Sort(_harvestBound, _harvestOrder, 0, count);
		for (int i = 0; i < count; i++)
		{
			float score = -_harvestBound[i];
			// It weighs the danger of only its attention's worth of the most promising (weighAtMost; 0 weighs them all).
			if (score <= bestScore || (weighAtMost > 0 && i >= weighAtMost))
				break;
			int c = _harvestOrder[i];
			int m = _material[c];
			Vector2 position = harvestables.PositionOf(m);
			float worth = _harvestWorth[c];

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

	private float[] _nearestKeys = new float[512];

	/// <summary>Keeps the <paramref name="keep"/> items nearest by <paramref name="distanceOf"/>, nearest first, and
	/// returns how many are left. For its attention.</summary>
	private int KeepNearest(int[] items, int count, int keep, System.Func<int, float> distanceOf)
	{
		if (count <= keep)
			return count;
		if (_nearestKeys.Length < count)
			_nearestKeys = new float[count * 2];
		for (int c = 0; c < count; c++)
			_nearestKeys[c] = distanceOf(items[c]);
		System.Array.Sort(_nearestKeys, items, 0, count);
		return keep;
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

	// Upgrade picks are a choice on Arbiter's decision core, its second user after moving (docs/dev/PILOT_SPEC.md):
	// the offered upgrades are the options, and the build's weights are three terms on them.
	private Arbiter.Decision _pick;
	private int _tLiking, _tOwned, _tNewWeapon;
	private IReadOnlyList<UpgradeDefinition> _offer;

	private void SetUpPicking()
	{
		_pick = new Arbiter.Decision("upgrade");
		// How much the build wants this upgrade: its own weight, or its category's. Weighted 1, since the build's
		// weights are already on the scale the other terms and the randomness use.
		_tLiking = _pick.AddTerm("liking", o => Liking(_offer[o]));
		// It improves something the VNP already has: an equipped weapon, or its drones' attack.
		_tOwned = _pick.AddTerm("owned", o => ImprovesOwned(_offer[o]) ? 1f : 0f);
		// It gives a new VNP Weapon or Drone Weapon.
		_tNewWeapon = _pick.AddTerm("new weapon",
			o => _offer[o].Effect is UpgradeEffect.EquipWeapon or UpgradeEffect.SwitchDroneWeapon ? 1f : 0f);
	}

	/// <summary>
	/// Picks from a level-up offer by the build's weights, with some randomness that shrinks with skill. Returns the
	/// index of the option to take, or -1 to reroll: when nothing scores well enough, a reroll is left, and it hasn't
	/// rerolled this offer yet.
	/// </summary>
	public int ChooseUpgrade(IReadOnlyList<UpgradeDefinition> options, int rerollsLeft, bool alreadyRerolled)
	{
		if (_pick == null)
			SetUpPicking();
		_offer = options;
		_pick.SetWeight(_tLiking, 1f);
		_pick.SetWeight(_tOwned, Build.FavourOwned);
		_pick.SetWeight(_tNewWeapon, Build.NewWeaponWeight);
		int best = _pick.Choose(options.Count, Build.Randomness + Skill.PickNoise, Random);
#if TOOLS
		if (Explaining)
			UpgradeReasoning = _pick.Explain(options.Count, o => options[o].Id);
#endif
		_offer = null;
		if (best >= 0 && rerollsLeft > 0 && !alreadyRerolled && Build.RerollBelow > 0f && _pick.TotalOf(best) < Build.RerollBelow)
			return -1;
		return best;
	}

#if TOOLS
	/// <summary>Why it picked as it did at its latest level-up offer, each option with the terms that scored it.
	/// Written only while <see cref="Explaining"/> is on; the harness prints it with explain=.</summary>
	public string UpgradeReasoning { get; private set; } = "";
#endif

	/// <summary>The build's weight for an upgrade: its own, if the build names it, or its category's.</summary>
	private float Liking(UpgradeDefinition upgrade) =>
		Build.UpgradeWeights.TryGetValue(upgrade.Id, out float own)
			? own
			: upgrade.Category switch
			{
				UpgradeCategory.VnpWeapon => Build.VnpWeaponWeight,
				UpgradeCategory.DroneWeapon => Build.DroneWeaponWeight,
				UpgradeCategory.Stats => Build.StatsWeight,
				_ => Build.DronesWeight,
			};

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
