using Godot;
using System;

/// <summary>
/// The VNP's basic probes. Like the enemies, they are plain data in one array, drawn in one batch.
///
/// Each probe does the first of these that applies:
/// 1. Attack. When an enemy is within engage range of the VNP, the probe heads for its own spot
///    around it, so the probes surround it, and fires. While attacking it still harvests anything
///    already within reach of its harvest beam, but does not move for it.
/// 2. Harvest. When an asteroid or piece of debris is within harvest range of the VNP, the probe
///    surrounds it and draws mass out with its harvest beam.
/// 3. Flock. Otherwise it flocks around the VNP and keeps pace with it.
/// A probe drops its target once that target is no longer within range of the VNP, so when the
/// player moves away, the probes break off and follow.
/// </summary>
public partial class ProbeSwarm : Node3D
{
	private struct Probe
	{
		public Vector2 Position;
		public Vector2 Velocity;
		public float Yaw;
		public float Health;
		public float FireTimer;   // Seconds until it can fire again.
		public float SlotAngle;   // Its side of whatever it is surrounding.
		public float OrbitSign;   // 1 or -1: which way it circles.
		public float Flash;       // Seconds left of the hit flash.
		public float Age;         // Seconds since it was built.
		public int TargetId;      // The enemy it is attacking, or 0.
		public int HarvestId;     // The asteroid or debris it is harvesting, or 0.
	}

	/// <summary>A destroyed probe, shown briefly while it disintegrates. It cannot be harvested.</summary>
	private struct Wreck
	{
		public Vector2 Position;
		public float Yaw;
		public float Age;
	}

	// Placeholder silhouette: a small kite. Forward is -y.
	private static readonly Vector2[] Outline =
	{
		new(0f, -0.75f), new(0.45f, 0.05f), new(0f, 0.55f), new(-0.45f, 0.05f),
	};

	// A probe already busy with a target keeps it a little past the range it would pick it up at,
	// so it does not dither when a target sits right on the edge.
	private const float KeepTargetFactor = 1.2f;

	// Seconds a newly built probe takes to grow to full size.
	private const float BuildTime = 0.35f;

	private const float BeamHeight = 0.2f;

	public Fight Fight;

	/// <summary>Probes currently alive.</summary>
	public int Count { get; private set; }

	/// <summary>Probes alive plus those built this frame and not yet added.</summary>
	public int CountWithBuilds => Count + _pendingBuilds;

	/// <summary>Distance from the VNP to its farthest probe.</summary>
	public float Reach { get; private set; }

	/// <summary>Most probes the VNP can have. Harvesting builds probes up to this.</summary>
	public int Cap = Tuning.ProbeCap;

	// The Basic Probe Weapon. One set of numbers shared by every probe,
	// so an upgrade changes them all at once.
	public float WeaponDamage = Tuning.ProbeProjectileDamage;
	public float WeaponCooldown = Tuning.ProbeProjectileCooldown;
	public float WeaponRange = Tuning.ProbeProjectileRange;

	/// <summary>Mass each probe's harvest beam draws out per second.</summary>
	public float HarvestRate = Tuning.ProbeHarvestRate;

	private Probe[] _probes = new Probe[64];
	private Wreck[] _wrecks = new Wreck[16];
	private int _wreckCount;
	private int _spawned;
	private int _pendingBuilds;
	private readonly SpatialGrid _grid = new(4f);
	private readonly int[] _nearby = new int[32];
	private readonly int[] _enemyTargets = new int[1024];
	private readonly int[] _harvestTargets = new int[512];
	private Vector2[] _beamFrom = new Vector2[64];
	private Vector2[] _beamTo = new Vector2[64];
	private int _beamCount;
	private InstanceBatch _batch;
	private InstanceBatch _beamBatch;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Probes;
		_batch = new InstanceBatch(this, "ProbeShapes", Placeholder.ExtrudedShape(Outline, 0.45f), Placeholder.InstanceMaterial());
		_beamBatch = new InstanceBatch(this, "HarvestBeams", new BoxMesh { Size = Vector3.One }, Placeholder.InstanceMaterial(unshaded: true));
	}

	/// <summary>Adds probes in a ring around a point. Built probes start small and grow in.</summary>
	public void Add(int count, Vector2 around, bool built = false)
	{
		float spread = built ? 1f : Tuning.ProbeFlockRadius;
		for (int i = 0; i < count; i++)
		{
			if (Count == _probes.Length)
				Array.Resize(ref _probes, _probes.Length * 2);

			// The golden angle spreads any number of probes evenly around a circle.
			float angle = _spawned++ * 2.39996f;
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			_probes[Count++] = new Probe
			{
				Position = around + offset * spread,
				Yaw = PlaneMath.YawFacing(offset),
				Health = Tuning.ProbeHealth,
				FireTimer = Rng.Range(0f, WeaponCooldown), // So the probes do not fire in unison.
				SlotAngle = angle,
				OrbitSign = Rng.Sign(),
				Age = built ? 0f : BuildTime,
			};
		}
	}

	/// <summary>Builds one probe at the VNP. It appears at the start of the next probe update.</summary>
	public void Build() => _pendingBuilds++;

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		float dt = (float)delta;
		RemoveDestroyed();
		if (_pendingBuilds > 0)
		{
			Add(_pendingBuilds, Fight.Vnp.PlanePosition, built: true);
			_pendingBuilds = 0;
		}
		Simulate(dt);
		AgeWrecks(dt);
		Draw();
	}

	// ───────────── Used by enemies and projectiles ─────────────

	/// <summary>Finds the living probe closest to a point. False if none is within range.</summary>
	public bool FindNearest(Vector2 from, float range, out Vector2 position, out Vector2 velocity, out float distance)
	{
		int nearest = -1;
		float nearestSquared = range * range;
		for (int i = 0; i < Count; i++)
		{
			if (_probes[i].Health <= 0f)
				continue;
			float distanceSquared = from.DistanceSquaredTo(_probes[i].Position);
			if (distanceSquared <= nearestSquared)
			{
				nearestSquared = distanceSquared;
				nearest = i;
			}
		}

		position = nearest >= 0 ? _probes[nearest].Position : default;
		velocity = nearest >= 0 ? _probes[nearest].Velocity : default;
		distance = Mathf.Sqrt(nearestSquared);
		return nearest >= 0;
	}

	/// <summary>The first living probe a shot travelling between two points would hit, or -1.</summary>
	public int FindHit(Vector2 from, Vector2 to)
	{
		// The grid is filled at the start of the probe update, so search a little wide to
		// allow for how far probes have moved since.
		float reach = from.DistanceTo(to) * 0.5f + Tuning.ProbeHitRadius + 1f;
		int candidates = _grid.Query((from + to) * 0.5f, reach, _nearby);

		int hit = -1;
		float hitDistanceSquared = float.MaxValue;
		for (int c = 0; c < candidates; c++)
		{
			int i = _nearby[c];
			if (_probes[i].Health <= 0f)
				continue;
			if (PlaneMath.DistanceToSegment(_probes[i].Position, from, to) > Tuning.ProbeHitRadius)
				continue;

			float distanceSquared = from.DistanceSquaredTo(_probes[i].Position);
			if (distanceSquared < hitDistanceSquared)
			{
				hitDistanceSquared = distanceSquared;
				hit = i;
			}
		}
		return hit;
	}

	/// <summary>Damages a probe. A destroyed probe leaves wreckage that disintegrates.</summary>
	public void Damage(int index, float amount)
	{
		ref Probe probe = ref _probes[index];
		if (probe.Health <= 0f)
			return;

		probe.Health -= amount;
		probe.Flash = Tuning.HitFlashTime;
		if (probe.Health <= 0f)
		{
			if (_wreckCount == _wrecks.Length)
				Array.Resize(ref _wrecks, _wrecks.Length * 2);
			_wrecks[_wreckCount++] = new Wreck { Position = probe.Position, Yaw = probe.Yaw };
			// The probe itself is cleared away at the start of the next update.
		}
	}

	// ───────────── Each frame ─────────────

	private void RemoveDestroyed()
	{
		for (int i = Count - 1; i >= 0; i--)
		{
			if (_probes[i].Health <= 0f)
				_probes[i] = _probes[--Count]; // Fill the gap with the last probe.
		}
	}

	private void Simulate(float dt)
	{
		Vnp vnp = Fight.Vnp;
		Vector2 vnpPosition = vnp.PlanePosition;
		Vector2 vnpVelocity = vnp.Velocity;
		EnemySwarm enemies = Fight.Enemies;
		Harvestables harvestables = Fight.Harvestables;

		_grid.Clear();
		for (int i = 0; i < Count; i++)
			_grid.Insert(i, _probes[i].Position);

		// Everything the probes could go after this frame: what is near enough to the VNP.
		int enemyCount = enemies.CollectNear(vnpPosition, Tuning.ProbeEngageRange * KeepTargetFactor, _enemyTargets);
		int harvestCount = harvestables.CollectNear(vnpPosition, Tuning.ProbeHarvestRange * KeepTargetFactor, _harvestTargets);

		if (_beamFrom.Length < Count)
		{
			Array.Resize(ref _beamFrom, Count * 2);
			Array.Resize(ref _beamTo, Count * 2);
		}
		_beamCount = 0;

		float turnStep = Mathf.DegToRad(Tuning.ProbeTurnSpeed) * dt;
		float reach = 0f;

		for (int i = 0; i < Count; i++)
		{
			ref Probe probe = ref _probes[i];
			probe.FireTimer = Mathf.Max(0f, probe.FireTimer - dt);
			probe.Flash = Mathf.Max(0f, probe.Flash - dt);
			probe.Age += dt;

			Vector2 separation = Separation(i);
			Vector2 wantedVelocity;

			int enemy = PickEnemyTarget(ref probe, enemyCount, vnpPosition);
			if (enemy >= 0)
			{
				// 1. Attack, and harvest at the same time.
				probe.TargetId = enemies.IdOf(enemy);
				probe.HarvestId = 0;

				Vector2 enemyPosition = enemies.PositionOf(enemy);
				wantedVelocity = SteerToSlot(ref probe, enemyPosition, enemies.VelocityOf(enemy),
					Tuning.ProbeAttackDistance, separation, dt);
				Face(ref probe, enemyPosition, turnStep);
				if (probe.Position.DistanceTo(enemyPosition) <= WeaponRange)
					TryFire(ref probe, enemy);

				// Beam whatever is already within reach, without moving for it.
				int nearby = NearestHarvestInBeamRange(probe.Position, harvestCount);
				if (nearby >= 0)
				{
					harvestables.Harvest(nearby, HarvestRate * dt);
					AddBeam(probe.Position, harvestables.PositionOf(nearby), harvestables.RadiusOf(nearby));
				}
			}
			else
			{
				probe.TargetId = 0;
				int harvest = PickHarvestTarget(ref probe, harvestCount, vnpPosition);
				if (harvest >= 0)
				{
					// 2. Harvest.
					probe.HarvestId = harvestables.IdOf(harvest);

					Vector2 itemPosition = harvestables.PositionOf(harvest);
					float itemRadius = harvestables.RadiusOf(harvest);
					wantedVelocity = SteerToSlot(ref probe, itemPosition, harvestables.VelocityOf(harvest),
						itemRadius + Tuning.ProbeHarvestStandoff, separation, dt);
					Face(ref probe, itemPosition, turnStep);

					if (probe.Position.DistanceTo(itemPosition) - itemRadius <= harvestables.BeamRange)
					{
						harvestables.Harvest(harvest, HarvestRate * dt);
						AddBeam(probe.Position, itemPosition, itemRadius);
					}
				}
				else
				{
					// 3. Flock.
					probe.HarvestId = 0;
					wantedVelocity = Flock(ref probe, vnpPosition, vnpVelocity, separation);
					if (probe.Velocity.LengthSquared() > 0.25f)
						probe.Yaw = Mathf.RotateToward(probe.Yaw, PlaneMath.YawFacing(probe.Velocity), turnStep);
				}
			}

			wantedVelocity = wantedVelocity.LimitLength(Tuning.ProbeSpeed);
			probe.Velocity = probe.Velocity.MoveToward(wantedVelocity, Tuning.ProbeAcceleration * dt);
			probe.Position += probe.Velocity * dt;

			reach = Mathf.Max(reach, probe.Position.DistanceTo(vnpPosition));
		}

		Reach = reach;
	}

	/// <summary>Push away from probes that are too close.</summary>
	private Vector2 Separation(int index)
	{
		Vector2 position = _probes[index].Position;
		float radius = Tuning.ProbeSeparationRadius;
		Vector2 separation = Vector2.Zero;

		int nearbyCount = _grid.Query(position, radius, _nearby);
		for (int n = 0; n < nearbyCount; n++)
		{
			int other = _nearby[n];
			if (other == index)
				continue;
			Vector2 away = position - _probes[other].Position;
			float gap = away.Length();
			if (gap < radius)
				separation += (gap > 0.001f ? away / gap : Rng.Direction()) * (1f - gap / radius);
		}
		return separation;
	}

	/// <summary>
	/// What this probe should harvest, or -1. It keeps its current target while that lasts and stays
	/// within range of the VNP. Otherwise it takes the closest to itself, out of those within range.
	/// </summary>
	private int PickHarvestTarget(ref Probe probe, int candidateCount, Vector2 vnpPosition)
	{
		Harvestables harvestables = Fight.Harvestables;

		// The candidates were collected out to the wider keep range.
		if (probe.HarvestId != 0)
		{
			for (int c = 0; c < candidateCount; c++)
			{
				if (harvestables.IdOf(_harvestTargets[c]) == probe.HarvestId)
					return _harvestTargets[c];
			}
		}

		int best = -1;
		float bestSquared = float.MaxValue;
		for (int c = 0; c < candidateCount; c++)
		{
			int i = _harvestTargets[c];
			if (harvestables.EdgeDistance(i, vnpPosition) > Tuning.ProbeHarvestRange)
				continue;
			float distanceSquared = harvestables.PositionOf(i).DistanceSquaredTo(probe.Position);
			if (distanceSquared < bestSquared)
			{
				bestSquared = distanceSquared;
				best = i;
			}
		}
		return best;
	}

	/// <summary>
	/// Which enemy this probe should attack, or -1. It stays on its current enemy while that enemy is
	/// alive and within range of the VNP. Without this, probes in a crowd keep switching target and
	/// never finish anything off. Otherwise it takes the closest to itself, out of those within range.
	/// </summary>
	private int PickEnemyTarget(ref Probe probe, int candidateCount, Vector2 vnpPosition)
	{
		EnemySwarm enemies = Fight.Enemies;

		if (probe.TargetId != 0)
		{
			float keepRange = Tuning.ProbeEngageRange * KeepTargetFactor;
			for (int c = 0; c < candidateCount; c++)
			{
				int i = _enemyTargets[c];
				if (enemies.IdOf(i) != probe.TargetId)
					continue;
				if (enemies.PositionOf(i).DistanceSquaredTo(vnpPosition) <= keepRange * keepRange)
					return i;
				break;
			}
		}

		float range = Tuning.ProbeEngageRange;
		int best = -1;
		float bestSquared = float.MaxValue;
		for (int c = 0; c < candidateCount; c++)
		{
			int i = _enemyTargets[c];
			Vector2 enemyPosition = enemies.PositionOf(i);
			if (enemyPosition.DistanceSquaredTo(vnpPosition) > range * range)
				continue;
			float distanceSquared = enemyPosition.DistanceSquaredTo(probe.Position);
			if (distanceSquared < bestSquared)
			{
				bestSquared = distanceSquared;
				best = i;
			}
		}
		return best;
	}

	/// <summary>The closest asteroid or debris within this probe's harvest beam reach, or -1.</summary>
	private int NearestHarvestInBeamRange(Vector2 position, int candidateCount)
	{
		Harvestables harvestables = Fight.Harvestables;
		int best = -1;
		float bestEdge = harvestables.BeamRange;
		for (int c = 0; c < candidateCount; c++)
		{
			int i = _harvestTargets[c];
			float edge = harvestables.EdgeDistance(i, position);
			if (edge <= bestEdge)
			{
				bestEdge = edge;
				best = i;
			}
		}
		return best;
	}

	/// <summary>
	/// Heads for this probe's own spot on a ring around a point, so a group of probes surrounds it.
	/// The spots slowly turn, so the ring circles.
	/// </summary>
	private static Vector2 SteerToSlot(ref Probe probe, Vector2 center, Vector2 centerVelocity, float distance,
		Vector2 separation, float dt)
	{
		float circleSpeed = Mathf.DegToRad(Tuning.ProbeCircleSpeed) * probe.OrbitSign;
		probe.SlotAngle += circleSpeed * dt;
		Vector2 around = new Vector2(Mathf.Cos(probe.SlotAngle), Mathf.Sin(probe.SlotAngle));
		Vector2 slot = center + around * distance;

		// Move with the slot: carried along by the target, and turning around it.
		Vector2 velocity = centerVelocity + new Vector2(-around.Y, around.X) * (circleSpeed * distance);

		// On top of that, close in only as fast as it can still stop at the slot, so it arrives
		// without overshooting and bouncing back. The last couple of units ease out smoothly.
		Vector2 toSlot = slot - probe.Position;
		float slotDistance = toSlot.Length();
		if (slotDistance > 0.001f)
		{
			float braking = Tuning.ProbeAcceleration * Tuning.ProbeArriveBraking;
			float approach = Mathf.Min(Tuning.ProbeSpeed,
				Mathf.Min(Mathf.Sqrt(2f * braking * slotDistance), Mathf.Sqrt(braking) * slotDistance));
			velocity += toSlot / slotDistance * approach;
		}

		return velocity + separation * Tuning.ProbeSeparation * Tuning.ProbeFlockSpeed;
	}

	/// <summary>Stay in a band around the VNP, circle it, keep clear of other probes, and keep pace.</summary>
	private static Vector2 Flock(ref Probe probe, Vector2 vnpPosition, Vector2 vnpVelocity, Vector2 separation)
	{
		Vector2 toVnp = vnpPosition - probe.Position;
		float vnpDistance = toVnp.Length();
		Vector2 directionToVnp = vnpDistance > 0.001f ? toVnp / vnpDistance : Vector2.Up;

		// Far behind: sprint straight back.
		if (vnpDistance > Tuning.ProbeSprintDistance)
			return directionToVnp * Tuning.ProbeSpeed;

		float band = Tuning.ProbeFlockRadius;
		float tooClose = band * 0.45f;
		Vector2 steer = new Vector2(-directionToVnp.Y, directionToVnp.X) * probe.OrbitSign * 0.6f;
		if (vnpDistance > band)
			steer += directionToVnp * Mathf.Min((vnpDistance - band) / band, 1.5f);
		else if (vnpDistance < tooClose)
			steer -= directionToVnp * (1f - vnpDistance / tooClose);
		steer += separation * Tuning.ProbeSeparation;

		// Matching the VNP's velocity is what keeps the flock with it instead of trailing.
		return vnpVelocity + steer.LimitLength(1f) * Tuning.ProbeFlockSpeed;
	}

	private static void Face(ref Probe probe, Vector2 point, float turnStep)
	{
		Vector2 toward = point - probe.Position;
		if (toward.LengthSquared() > 0.0001f)
			probe.Yaw = Mathf.RotateToward(probe.Yaw, PlaneMath.YawFacing(toward), turnStep);
	}

	/// <summary>Fires the Basic Probe Weapon at an enemy if it is off cooldown.</summary>
	private void TryFire(ref Probe probe, int enemy)
	{
		if (probe.FireTimer > 0f)
			return;

		EnemySwarm enemies = Fight.Enemies;

		// Let the shot fly a little past the weapon's range before it fizzles out.
		float lifetime = WeaponRange * 1.5f / Tuning.ProbeProjectileSpeed;

		// Aim ahead of a moving enemy, at where it will be when the shot arrives.
		Vector2 aimAt = PlaneMath.AimAhead(probe.Position, enemies.PositionOf(enemy), enemies.VelocityOf(enemy),
			Tuning.ProbeProjectileSpeed, lifetime);
		Vector2 toAim = aimAt - probe.Position;
		if (toAim.LengthSquared() < 0.0001f)
			return;

		Fight.Projectiles.Fire(probe.Position, toAim.Normalized(), WeaponDamage,
			Tuning.ProbeProjectileSpeed, lifetime, fromEnemy: false);
		probe.FireTimer = WeaponCooldown * Rng.Range(0.9f, 1.1f);
	}

	/// <summary>Records a harvest beam to draw, ending just inside the edge of what it harvests.</summary>
	private void AddBeam(Vector2 from, Vector2 to, float targetRadius)
	{
		Vector2 along = to - from;
		float distance = along.Length();
		if (distance < 0.01f)
			return;

		_beamFrom[_beamCount] = from;
		_beamTo[_beamCount] = from + along / distance * Mathf.Max(0f, distance - targetRadius * 0.5f);
		_beamCount++;
	}

	private void AgeWrecks(float dt)
	{
		for (int i = _wreckCount - 1; i >= 0; i--)
		{
			_wrecks[i].Age += dt;
			if (_wrecks[i].Age >= Tuning.ProbeWreckTime)
				_wrecks[i] = _wrecks[--_wreckCount];
		}
	}

	private void Draw()
	{
		_batch.Begin(Count + _wreckCount);

		for (int i = 0; i < Count; i++)
		{
			ref Probe probe = ref _probes[i];
			Color color = Tuning.ProbeColor;
			float scale = Tuning.ProbeSize;

			// A newly built probe grows in from a white spark.
			if (probe.Age < BuildTime)
			{
				float grown = probe.Age / BuildTime;
				color = Colors.White.Lerp(color, grown);
				scale *= grown;
			}
			if (probe.Flash > 0f)
				color = color.Lerp(new Color(1f, 0.2f, 0.15f), probe.Flash / Tuning.HitFlashTime);

			_batch.Set(i, probe.Position, 0f, probe.Yaw, scale, color);
		}

		// Wreckage: flares white, then spins and shrinks away to nothing.
		for (int w = 0; w < _wreckCount; w++)
		{
			float gone = _wrecks[w].Age / Tuning.ProbeWreckTime;
			Color color = Colors.White.Lerp(Tuning.ProbeColor.Darkened(0.6f), gone);
			_batch.Set(Count + w, _wrecks[w].Position, 0f, _wrecks[w].Yaw + gone * 6f, Tuning.ProbeSize * (1f - gone), color);
		}

		_batch.Commit(Count + _wreckCount);

		_beamBatch.Begin(_beamCount);
		for (int b = 0; b < _beamCount; b++)
			_beamBatch.SetBeam(b, _beamFrom[b], _beamTo[b], BeamHeight, 0.12f, 0.06f, Tuning.ExperienceColor);
		_beamBatch.Commit(_beamCount);
	}
}
