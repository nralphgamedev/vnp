using Godot;
using System;

/// <summary>
/// The VNP's basic probes. Like the enemies, they are plain data in one array, drawn in one batch.
///
/// Behaviour: with nothing to attack, a probe flocks around the VNP and keeps pace with it.
/// When an enemy comes within engage range of the VNP, the probe heads for its own spot around
/// that enemy, so the probes surround it, and fires. When the enemy is no longer within range of
/// the VNP, because it died, fled, or the VNP moved away, the probe breaks off and returns.
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
		public float SlotAngle;   // Its side of a surrounded enemy.
		public float OrbitSign;   // 1 or -1: which way it circles.
		public float Flash;       // Seconds left of the hit flash.
		public bool Engaged;      // Attacking an enemy, as opposed to flocking.
		public int TargetId;      // The enemy it is attacking, or 0.
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

	// A probe already attacking keeps its target a little past the engage range,
	// so it does not dither when an enemy sits right on the edge.
	private const float KeepEngagedFactor = 1.2f;

	// How fast a probe circles the enemy it is surrounding, in radians per second.
	private const float CircleSpeed = 0.8f;

	public Fight Fight;

	/// <summary>Probes currently alive.</summary>
	public int Count { get; private set; }

	/// <summary>Distance from the VNP to its farthest probe.</summary>
	public float Reach { get; private set; }

	// The Basic Probe Weapon. One set of numbers shared by every probe,
	// so an upgrade changes them all at once.
	public float WeaponDamage = Tuning.ProbeProjectileDamage;
	public float WeaponCooldown = Tuning.ProbeProjectileCooldown;
	public float WeaponRange = Tuning.ProbeProjectileRange;

	private Probe[] _probes = new Probe[64];
	private Wreck[] _wrecks = new Wreck[16];
	private int _wreckCount;
	private int _spawned;
	private readonly SpatialGrid _grid = new(4f);
	private readonly int[] _nearby = new int[32];
	private readonly int[] _targets = new int[1024];
	private InstanceBatch _batch;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Probes;
		_batch = new InstanceBatch(this, "ProbeShapes", Placeholder.ExtrudedShape(Outline, 0.45f), Placeholder.InstanceMaterial());
	}

	/// <summary>Adds probes in a ring around a point.</summary>
	public void Add(int count, Vector2 around)
	{
		for (int i = 0; i < count; i++)
		{
			if (Count == _probes.Length)
				Array.Resize(ref _probes, _probes.Length * 2);

			// The golden angle spreads any number of probes evenly around a circle.
			float angle = _spawned++ * 2.39996f;
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			_probes[Count++] = new Probe
			{
				Position = around + offset * Tuning.ProbeFlockRadius,
				Yaw = PlaneMath.YawFacing(offset),
				Health = Tuning.ProbeHealth,
				FireTimer = Rng.Range(0f, WeaponCooldown), // So the probes do not fire in unison.
				SlotAngle = angle,
				OrbitSign = Rng.Sign(),
			};
		}
	}

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		float dt = (float)delta;
		RemoveDestroyed();
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

		_grid.Clear();
		for (int i = 0; i < Count; i++)
			_grid.Insert(i, _probes[i].Position);

		// Every enemy the probes could attack this frame: those near enough to the VNP.
		int targetCount = enemies.CollectNear(vnpPosition, Tuning.ProbeEngageRange * KeepEngagedFactor, _targets);

		float separationRadius = Tuning.ProbeSeparationRadius;
		float turnStep = Mathf.DegToRad(Tuning.ProbeTurnSpeed) * dt;
		float reach = 0f;

		for (int i = 0; i < Count; i++)
		{
			ref Probe probe = ref _probes[i];

			// Push away from probes that are too close.
			Vector2 separation = Vector2.Zero;
			int nearbyCount = _grid.Query(probe.Position, separationRadius, _nearby);
			for (int n = 0; n < nearbyCount; n++)
			{
				int other = _nearby[n];
				if (other == i)
					continue;
				Vector2 away = probe.Position - _probes[other].Position;
				float gap = away.Length();
				if (gap < separationRadius)
					separation += (gap > 0.001f ? away / gap : Rng.Direction()) * (1f - gap / separationRadius);
			}

			float engageRange = Tuning.ProbeEngageRange * (probe.Engaged ? KeepEngagedFactor : 1f);
			float engageRangeSquared = engageRange * engageRange;
			int target = -1;
			Vector2 targetPosition = default;
			float targetDistanceSquared = float.MaxValue;

			// Stay on the enemy it was already attacking while that enemy is alive and in range.
			// Without this, probes in a crowd keep switching target and never finish anything off.
			if (probe.TargetId != 0)
			{
				for (int t = 0; t < targetCount; t++)
				{
					if (enemies.IdOf(_targets[t]) != probe.TargetId)
						continue;
					Vector2 enemyPosition = enemies.PositionOf(_targets[t]);
					if (enemyPosition.DistanceSquaredTo(vnpPosition) <= engageRangeSquared)
					{
						target = _targets[t];
						targetPosition = enemyPosition;
						targetDistanceSquared = enemyPosition.DistanceSquaredTo(probe.Position);
					}
					break;
				}
			}

			// Otherwise pick the closest enemy to this probe, out of those in range of the VNP.
			if (target < 0)
			{
				for (int t = 0; t < targetCount; t++)
				{
					Vector2 enemyPosition = enemies.PositionOf(_targets[t]);
					if (enemyPosition.DistanceSquaredTo(vnpPosition) > engageRangeSquared)
						continue;
					float distanceSquared = enemyPosition.DistanceSquaredTo(probe.Position);
					if (distanceSquared < targetDistanceSquared)
					{
						targetDistanceSquared = distanceSquared;
						targetPosition = enemyPosition;
						target = _targets[t];
					}
				}
			}

			bool hasTarget = target >= 0;
			probe.Engaged = hasTarget;
			probe.TargetId = hasTarget ? enemies.IdOf(target) : 0;

			Vector2 wantedVelocity;
			probe.FireTimer = Mathf.Max(0f, probe.FireTimer - dt);

			if (hasTarget)
			{
				// Surround: each probe heads for its own spot on a ring around the enemy,
				// and the spots slowly turn so the ring circles.
				probe.SlotAngle += CircleSpeed * probe.OrbitSign * dt;
				Vector2 slot = targetPosition
					+ new Vector2(Mathf.Cos(probe.SlotAngle), Mathf.Sin(probe.SlotAngle)) * Tuning.ProbeAttackDistance;
				Vector2 toSlot = slot - probe.Position;
				float slotDistance = toSlot.Length();

				// Full speed until close, then ease in so it does not overshoot.
				wantedVelocity = slotDistance > 0.001f
					? toSlot / slotDistance * Tuning.ProbeSpeed * Mathf.Min(1f, slotDistance / 3f)
					: Vector2.Zero;
				wantedVelocity += separation * Tuning.ProbeSeparation * Tuning.ProbeFlockSpeed;

				Vector2 toTarget = targetPosition - probe.Position;
				float targetDistance = Mathf.Sqrt(targetDistanceSquared);
				Vector2 directionToTarget = targetDistance > 0.001f ? toTarget / targetDistance : Vector2.Up;
				probe.Yaw = Mathf.RotateToward(probe.Yaw, PlaneMath.YawFacing(directionToTarget), turnStep);

				if (targetDistance <= WeaponRange && probe.FireTimer <= 0f)
				{
					// Let the shot fly a little past the weapon's range before it fizzles out.
					float lifetime = WeaponRange * 1.5f / Tuning.ProbeProjectileSpeed;

					// Aim ahead of a moving enemy, at where it will be when the shot arrives.
					Vector2 aimAt = PlaneMath.AimAhead(probe.Position, targetPosition, enemies.VelocityOf(target),
						Tuning.ProbeProjectileSpeed, lifetime);
					Vector2 aim = (aimAt - probe.Position).Normalized();
					Fight.Projectiles.Fire(probe.Position, aim, WeaponDamage,
						Tuning.ProbeProjectileSpeed, lifetime, fromEnemy: false);
					probe.FireTimer = WeaponCooldown * Rng.Range(0.9f, 1.1f);
				}
			}
			else
			{
				Vector2 toVnp = vnpPosition - probe.Position;
				float vnpDistance = toVnp.Length();
				Vector2 directionToVnp = vnpDistance > 0.001f ? toVnp / vnpDistance : Vector2.Up;

				if (vnpDistance > Tuning.ProbeSprintDistance)
				{
					// Far behind: sprint straight back.
					wantedVelocity = directionToVnp * Tuning.ProbeSpeed;
				}
				else
				{
					// Flock: stay in a band around the VNP, circle it, and keep clear of other probes.
					float band = Tuning.ProbeFlockRadius;
					float tooClose = band * 0.45f;
					Vector2 steer = new Vector2(-directionToVnp.Y, directionToVnp.X) * probe.OrbitSign * 0.6f;
					if (vnpDistance > band)
						steer += directionToVnp * Mathf.Min((vnpDistance - band) / band, 1.5f);
					else if (vnpDistance < tooClose)
						steer -= directionToVnp * (1f - vnpDistance / tooClose);
					steer += separation * Tuning.ProbeSeparation;

					// Matching the VNP's velocity is what keeps the flock with it instead of trailing.
					wantedVelocity = vnpVelocity + steer.LimitLength(1f) * Tuning.ProbeFlockSpeed;
				}

				if (probe.Velocity.LengthSquared() > 0.25f)
					probe.Yaw = Mathf.RotateToward(probe.Yaw, PlaneMath.YawFacing(probe.Velocity), turnStep);
			}

			wantedVelocity = wantedVelocity.LimitLength(Tuning.ProbeSpeed);
			probe.Velocity = probe.Velocity.MoveToward(wantedVelocity, Tuning.ProbeAcceleration * dt);
			probe.Position += probe.Velocity * dt;
			probe.Flash = Mathf.Max(0f, probe.Flash - dt);

			reach = Mathf.Max(reach, probe.Position.DistanceTo(vnpPosition));
		}

		Reach = reach;
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
			if (probe.Flash > 0f)
				color = color.Lerp(new Color(1f, 0.2f, 0.15f), probe.Flash / Tuning.HitFlashTime);
			_batch.Set(i, probe.Position, 0f, probe.Yaw, Tuning.ProbeSize, color);
		}

		// Wreckage: flares white, then spins and shrinks away to nothing.
		for (int w = 0; w < _wreckCount; w++)
		{
			float gone = _wrecks[w].Age / Tuning.ProbeWreckTime;
			Color color = Colors.White.Lerp(Tuning.ProbeColor.Darkened(0.6f), gone);
			_batch.Set(Count + w, _wrecks[w].Position, 0f, _wrecks[w].Yaw + gone * 6f, Tuning.ProbeSize * (1f - gone), color);
		}

		_batch.Commit(Count + _wreckCount);
	}
}
