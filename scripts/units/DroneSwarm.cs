using Godot;
using System;

/// <summary>
/// The VNP's drones. Like the enemies, they are plain data in one array, drawn in one batch.
///
/// Each drone does the first of these that applies:
/// 1. Attack. When an enemy is within engage range of the VNP, the drone heads for its own spot
///    around it, so the drones surround it, and fires. While attacking it still harvests anything
///    already within reach of its harvest beam, but does not move for it.
/// 2. Harvest. When an asteroid or piece of debris is within harvest range of the VNP, the drone
///    surrounds it and draws mass out with its harvest beam.
/// 3. Flock. Otherwise it flocks around the VNP and keeps pace with it.
/// A drone drops its target once that target is no longer within range of the VNP, so when the
/// player moves away, the drones break off and follow.
///
/// Distances to an enemy, for surrounding it and for weapon range, are measured from its edge, so drones
/// surround a large enemy at its edge rather than inside it. Some enemies, such as the Goo, ask drones to
/// stay farther back. Drones also steer clear of the enemies they aren't attacking, gently, so a drone fighting
/// one enemy doesn't drift into the next, and don't go in to harvest material beside a drone eater. An enemy
/// that absorbs drones, such as the Goo, drains the health of any drone touching
/// it; a drone drained to nothing is pulled into it and lost, while one that gets away keeps the damage.
/// </summary>
public partial class DroneSwarm : Node3D
{
	/// <summary>One drone. Public so Drone Weapon attacks in res://drone_weapons/ can work on it.</summary>
	public struct Drone
	{
		public int Id;            // Stays the same for its whole life, unlike its place in the array.
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
		public int AttackState;   // For the Drone Weapon's attack to use, such as where it is in the Ram cycle. 0 to start.
		public float AttackTime;  // For the Drone Weapon's attack to use, such as seconds in the current Ram state.
	}

	/// <summary>A destroyed drone, shown briefly while it disintegrates. It cannot be harvested.</summary>
	private struct Wreck
	{
		public Vector2 Position;
		public float Yaw;
		public float Age;
	}

	/// <summary>A drone being pulled into the enemy that absorbed it. It is already gone from the fight.</summary>
	private struct Absorbing
	{
		public Vector2 Start;
		public Vector2 Into;      // Where the enemy is, followed while it lives.
		public int EnemyId;
		public float Yaw;
		public float Age;
	}

	// A drone already busy with a target keeps it a little past the range it would pick it up at,
	// so it does not dither when a target sits right on the edge.
	/// <summary>Drones keep a target until it is this many times their engage range from the VNP. Public so a
	/// pilot can tell how far it must pull away for its drones to break off.</summary>
	public const float KeepTargetFactor = 1.2f;

	// Seconds a newly built drone takes to grow to full size.
	private const float BuildTime = 0.35f;

	// Attack beams run just above the drones. Harvest beams run just below them, so a beam seems to come
	// from under the drone rather than out of its middle.
	private const float BeamHeight = 0.2f;
	private const float HarvestBeamHeight = -0.3f;

	public Fight Fight;

	/// <summary>Drones currently alive.</summary>
	public int Count { get; private set; }

	/// <summary>Drones alive plus those built this frame and not yet added.</summary>
	public int CountWithBuilds => Count + _pendingBuilds;

	/// <summary>Distance from the VNP to its farthest drone.</summary>
	public float Reach { get; private set; }

	/// <summary>Most drones the VNP can have. Harvesting builds drones up to this.</summary>
	public int Cap = Tuning.Drones.Cap;

	/// <summary>The Drone Weapon every drone uses.</summary>
	public DroneWeaponDefinition Weapon { get; private set; } = Tuning.Drones.StartingWeapon;

	/// <summary>How the drones attack with that weapon. Swapped when they switch.</summary>
	private DroneAttack _attack;

	/// <summary>Drone damage upgrades, as a multiple starting at 1. Applies to whichever Drone Weapon the
	/// drones use, and carries over when they switch.</summary>
	public float DamageMultiplier = 1f;

	/// <summary>Drone attack rate upgrades, as a multiple starting at 1: 1.2 attacks 20% more often.
	/// Applies to whichever Drone Weapon the drones use, and carries over when they switch.</summary>
	public float AttackRateMultiplier = 1f;

	public float WeaponDamage => Weapon.Damage * DamageMultiplier;
	public float WeaponCooldown => Weapon.Cooldown / Mathf.Max(0.01f, AttackRateMultiplier);
	public float WeaponRange = Tuning.Drones.StartingWeapon.Range;

	/// <summary>Damage compared with the weapon's own, so damage upgrades show as bigger shots, beams and impacts.</summary>
	public float Power => Mathf.Sqrt(Mathf.Max(0.01f, DamageMultiplier));

	/// <summary>How fast a drone normally flies. Rises with the VNP's movement speed upgrades, so drones keep up.</summary>
	public float Speed => _speed;

	/// <summary>How far from its target's edge a drone sits while surrounding it. Depends on the weapon.</summary>
	public float AttackDistance = Tuning.Drones.StartingWeapon.AttackDistance;

	/// <summary>Mass each drone's harvest beam draws out per second.</summary>
	public float HarvestRate = Tuning.Harvesting.DroneRate;

	/// <summary>The Drone Weapon in use, as the source of the drones' hits, for fight statistics. Each Drone
	/// Weapon has its own, so damage done before a switch stays counted under the weapon that did it.</summary>
	public DamageSource Source => _source ??= SourceFor(Weapon);

	private DamageSource _source;

	private static DamageSource SourceFor(DroneWeaponDefinition weapon) =>
		new(weapon.Id, weapon.Name, DamageSourceKind.DroneWeapon, weapon.Color);

	/// <summary>Switches every drone to another Drone Weapon. Damage and attack rate upgrades carry over.</summary>
	public void SwitchWeapon(DroneWeaponDefinition weapon)
	{
		if (weapon == null)
			return;
		Weapon = weapon;
		_source = SourceFor(weapon);
		_attack = DroneAttack.Create(this, weapon);
		WeaponRange = weapon.Range;
		AttackDistance = weapon.AttackDistance;
		for (int i = 0; i < Count; i++)
		{
			_drones[i].AttackState = 0;
			_drones[i].AttackTime = 0f;
		}
	}

	private Drone[] _drones = new Drone[64];

	/// <summary>Where a drone is, for testing tools such as the run review's maps.</summary>
	public Vector2 PositionOf(int index) => _drones[index].Position;
	private int _lastId;
	private Wreck[] _wrecks = new Wreck[16];
	private int _wreckCount;
	private Absorbing[] _absorbing = new Absorbing[16];
	private int _absorbingCount;
	private int _spawned;
	private int _pendingBuilds;
	private readonly SpatialGrid _grid = new(4f);
	private readonly int[] _nearby = new int[32];
	private readonly int[] _enemyTargets = new int[1024];
	private readonly int[] _harvestTargets = new int[512];
	private readonly bool[] _harvestUnsafe = new bool[512];
	private readonly int[] _avoidNearby = new int[256];
	private Vector2[] _beamFrom = new Vector2[64];
	private Vector2[] _beamTo = new Vector2[64];
	private bool[] _beamIsAttack = new bool[64];
	private float[] _beamEndHeight = new float[64];
	private int _beamCount;
	private float _speed = Tuning.Drones.Speed;
	private InstanceBatch _batch;
	private InstanceBatch _beamBatch;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Drones;
		_attack ??= DroneAttack.Create(this, Weapon);
		_batch = new InstanceBatch(this, "DroneShapes", Placeholders.Mesh("drone"), Placeholders.Material("instanced_lit"));
		_beamBatch = new InstanceBatch(this, "HarvestBeams", Placeholders.Mesh("unit_box"), Placeholders.Material("instanced_unshaded"));
	}

	/// <summary>Adds drones in a ring around a point. Built drones start small and grow in.</summary>
	public void Add(int count, Vector2 around, bool built = false)
	{
		float spread = built ? 1f : Tuning.Drones.FlockRadius;
		for (int i = 0; i < count; i++)
		{
			if (Count == _drones.Length)
				Array.Resize(ref _drones, _drones.Length * 2);

			// The golden angle spreads any number of drones evenly around a circle.
			float angle = _spawned++ * 2.39996f;
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			_drones[Count++] = new Drone
			{
				Id = ++_lastId,
				Position = around + offset * spread,
				Yaw = PlaneMath.YawFacing(offset),
				Health = Tuning.Drones.Health,
				FireTimer = Rng.Range(0f, WeaponCooldown), // So the drones do not fire in unison.
				SlotAngle = angle,
				OrbitSign = Rng.Sign(),
				Age = built ? 0f : BuildTime,
			};
			if (built)
			{
				Fight.Stats.DronesBuilt++;
				Fight.Effects.DroneBuilt(around + offset * spread, Tuning.Drones.Color);
				Fight.Sounds.Play(Sound.DroneBuilt);
			}
		}
	}

	/// <summary>Builds one drone at the VNP. It appears at the start of the next drone update.</summary>
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

	/// <summary>Finds a living drone by its ID. False if it has been destroyed.</summary>
	public bool TryGet(int id, out Vector2 position, out Vector2 velocity)
	{
		for (int i = 0; i < Count; i++)
		{
			if (_drones[i].Id == id && _drones[i].Health > 0f)
			{
				position = _drones[i].Position;
				velocity = _drones[i].Velocity;
				return true;
			}
		}
		position = default;
		velocity = default;
		return false;
	}

	/// <summary>Finds the living drone closest to a point. False if none is within range.</summary>
	public bool FindNearest(Vector2 from, float range, out Vector2 position, out Vector2 velocity, out float distance)
		=> FindNearest(from, range, out position, out velocity, out distance, out _);

	/// <summary>Finds the living drone closest to a point, and its ID. False if none is within range.</summary>
	public bool FindNearest(Vector2 from, float range, out Vector2 position, out Vector2 velocity, out float distance, out int id)
	{
		int nearest = -1;
		float nearestSquared = range * range;
		for (int i = 0; i < Count; i++)
		{
			if (_drones[i].Health <= 0f)
				continue;
			float distanceSquared = from.DistanceSquaredTo(_drones[i].Position);
			if (distanceSquared <= nearestSquared)
			{
				nearestSquared = distanceSquared;
				nearest = i;
			}
		}

		position = nearest >= 0 ? _drones[nearest].Position : default;
		velocity = nearest >= 0 ? _drones[nearest].Velocity : default;
		id = nearest >= 0 ? _drones[nearest].Id : 0;
		distance = Mathf.Sqrt(nearestSquared);
		return nearest >= 0;
	}

	/// <summary>The first living drone a shot travelling between two points would hit, or -1.</summary>
	public int FindHit(Vector2 from, Vector2 to)
	{
		// The grid is filled at the start of the drone update, so search a little wide to
		// allow for how far drones have moved since.
		float reach = from.DistanceTo(to) * 0.5f + Tuning.Drones.HitRadius + 1f;
		int candidates = _grid.Query((from + to) * 0.5f, reach, _nearby);

		int hit = -1;
		float hitDistanceSquared = float.MaxValue;
		for (int c = 0; c < candidates; c++)
		{
			int i = _nearby[c];
			if (_drones[i].Health <= 0f)
				continue;
			if (PlaneMath.DistanceToSegment(_drones[i].Position, from, to) > Tuning.Drones.HitRadius)
				continue;

			float distanceSquared = from.DistanceSquaredTo(_drones[i].Position);
			if (distanceSquared < hitDistanceSquared)
			{
				hitDistanceSquared = distanceSquared;
				hit = i;
			}
		}
		return hit;
	}

	/// <summary>Damages a drone. A destroyed drone leaves wreckage that disintegrates. <paramref name="source"/>
	/// is what dealt the damage, for fight statistics.</summary>
	public void Damage(int index, float amount, DamageSource source)
	{
		ref Drone drone = ref _drones[index];
		if (drone.Health <= 0f)
			return;

		drone.Health -= amount;
		drone.Flash = Tuning.Feedback.HitFlashTime;
		Fight.Sounds.Play(Sound.Hit, 0.6f);
		if (drone.Health <= 0f)
		{
			if (_wreckCount == _wrecks.Length)
				Array.Resize(ref _wrecks, _wrecks.Length * 2);
			_wrecks[_wreckCount++] = new Wreck { Position = drone.Position, Yaw = drone.Yaw };
			Fight.Stats.DroneDestroyed(source);
			Fight.Effects.DronePop(drone.Position, Tuning.Drones.Color);
			Fight.Sounds.Play(Sound.DroneDeath);
			// The drone itself is cleared away at the start of the next update.
		}
	}

	// ───────────── Each frame ─────────────

	private void RemoveDestroyed()
	{
		for (int i = Count - 1; i >= 0; i--)
		{
			if (_drones[i].Health <= 0f)
				_drones[i] = _drones[--Count]; // Fill the gap with the last drone.
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
			_grid.Insert(i, _drones[i].Position);

		// Everything the drones could go after this frame: what is near enough to the VNP.
		int enemyCount = enemies.CollectNear(vnpPosition, Tuning.Drones.EngageRange * KeepTargetFactor, _enemyTargets);
		int harvestCount = harvestables.CollectNear(vnpPosition, Tuning.Harvesting.DroneRange * KeepTargetFactor, _harvestTargets);
		// Material beside a drone eater, such as a Goo, isn't worth a drone flying in for.
		for (int c = 0; c < harvestCount; c++)
		{
			int m = _harvestTargets[c];
			_harvestUnsafe[c] = Tuning.Drones.AvoidHarvestNearDroneEaters
				&& enemies.NearDroneEater(harvestables.PositionOf(m), harvestables.RadiusOf(m));
		}

		// Up to two beams per drone: one attacking, one harvesting.
		if (_beamFrom.Length < Count * 2)
		{
			Array.Resize(ref _beamFrom, Count * 4);
			Array.Resize(ref _beamTo, Count * 4);
			Array.Resize(ref _beamIsAttack, Count * 4);
			Array.Resize(ref _beamEndHeight, Count * 4);
		}
		_beamCount = 0;

		// Drones speed up with the VNP's movement speed upgrades, so they keep up.
		_speed = Tuning.Drones.Speed * vnp.MoveSpeed / Tuning.Vnp.MoveSpeed;

		float turnStep = Mathf.DegToRad(Tuning.Drones.TurnSpeed) * dt;
		float reach = 0f;

		for (int i = 0; i < Count; i++)
		{
			ref Drone drone = ref _drones[i];
			drone.FireTimer = Mathf.Max(0f, drone.FireTimer - dt);
			drone.Flash = Mathf.Max(0f, drone.Flash - dt);
			drone.Age += dt;

			Vector2 separation = Separation(i);
			Vector2 wantedVelocity;
			bool attackSteers = false;

			int enemy = PickEnemyTarget(ref drone, enemyCount, vnpPosition);
			if (enemy >= 0)
			{
				// 1. Attack, and harvest at the same time.
				int targetId = enemies.IdOf(enemy);
				if (drone.TargetId != targetId)
					_attack.TargetChanged(ref drone);
				drone.TargetId = targetId;
				drone.HarvestId = 0;

				Vector2 enemyPosition = enemies.PositionOf(enemy);
				wantedVelocity = SteerToSlot(ref drone, enemyPosition, enemies.VelocityOf(enemy),
					enemies.RadiusOf(enemy) + SurroundDistance(enemy), separation, dt);
				Face(ref drone, enemyPosition, turnStep);
				attackSteers = _attack.Attack(ref drone, enemy, ref wantedVelocity, dt);

				// Beam whatever is already within reach, without moving for it.
				int nearby = NearestHarvestInBeamRange(drone.Position, harvestCount);
				if (nearby >= 0)
				{
					harvestables.Harvest(nearby, HarvestRate * dt);
					AddHarvestBeam(drone.Position, harvestables.PositionOf(nearby), harvestables.HeightOf(nearby));
					HarvestFlow(drone.Position, harvestables.PositionOf(nearby), harvestables.RadiusOf(nearby), dt);
				}
			}
			else
			{
				drone.TargetId = 0;
				int harvest = PickHarvestTarget(ref drone, harvestCount, vnpPosition);
				if (harvest >= 0)
				{
					// 2. Harvest.
					drone.HarvestId = harvestables.IdOf(harvest);

					Vector2 itemPosition = harvestables.PositionOf(harvest);
					float itemRadius = harvestables.RadiusOf(harvest);
					wantedVelocity = SteerToSlot(ref drone, itemPosition, harvestables.VelocityOf(harvest),
						itemRadius + Tuning.Harvesting.DroneStandoff, separation, dt);
					Face(ref drone, itemPosition, turnStep);

					if (drone.Position.DistanceTo(itemPosition) - itemRadius <= harvestables.BeamRange)
					{
						harvestables.Harvest(harvest, HarvestRate * dt);
						AddHarvestBeam(drone.Position, itemPosition, harvestables.HeightOf(harvest));
						HarvestFlow(drone.Position, itemPosition, itemRadius, dt);
					}
				}
				else
				{
					// 3. Flock.
					drone.HarvestId = 0;
					wantedVelocity = Flock(ref drone, vnpPosition, vnpVelocity, separation);
					if (drone.Velocity.LengthSquared() > 0.25f)
						drone.Yaw = Mathf.RotateToward(drone.Yaw, PlaneMath.YawFacing(drone.Velocity), turnStep);
				}

				// The attack can finish a move first, such as a ramming drone finishing its arc.
				attackSteers = _attack.WithoutTarget(ref drone, ref wantedVelocity, dt);
			}

			// Steer clear of the enemies it isn't attacking. A drone whose attack steers it, such as a ramming
			// drone, keeps its speed and is only turned, so a charge still carries through its own target.
			Vector2 avoid = Avoid(drone.Position, drone.TargetId);
			if (avoid != Vector2.Zero)
			{
				if (attackSteers)
				{
					float speed = wantedVelocity.Length();
					Vector2 turned = wantedVelocity + avoid * (speed * Tuning.Drones.AvoidStrength);
					if (turned.LengthSquared() > 0.0001f)
						wantedVelocity = turned.Normalized() * speed;
				}
				else
				{
					wantedVelocity += avoid * (_speed * Tuning.Drones.AvoidStrength);
				}
			}

			if (attackSteers)
			{
				// The attack steers the drone itself, such as Ram charging faster than a drone normally flies.
				drone.Velocity = wantedVelocity;
			}
			else
			{
				wantedVelocity = wantedVelocity.LimitLength(_speed);
				drone.Velocity = drone.Velocity.MoveToward(wantedVelocity, Tuning.Drones.Acceleration * dt);
			}
			drone.Position += drone.Velocity * dt;

			// Touching an enemy that absorbs drones, such as a Goo, drains this drone. It flashes, and motes
			// flow from it into the enemy, so the player can see it being eaten. Drained to nothing, it is absorbed.
			int absorber = enemies.FindAbsorber(drone.Position, Tuning.Drones.HitRadius);
			if (absorber >= 0)
			{
				drone.Health -= enemies.DroneDrainOf(absorber) * dt;
				drone.Flash = Tuning.Feedback.HitFlashTime;
				Fight.Effects.HarvestFlow(drone.Position, enemies.PositionOf(absorber), Tuning.Drones.Color,
					Tuning.Feedback.DroneHarvestParticles, dt, 0.18f);
				if (drone.Health <= 0f)
				{
					AbsorbInto(ref drone, absorber);
					continue;
				}
			}

			reach = Mathf.Max(reach, drone.Position.DistanceTo(vnpPosition));
		}

		Reach = reach;
	}

	/// <summary>
	/// How far from an enemy's edge a drone sits while surrounding it: the weapon's attack distance, plus
	/// however far back that enemy asks drones to stay. Kept inside the weapon's range, so the drone can
	/// still reach it. Ramming drones charge in regardless.
	/// </summary>
	private float SurroundDistance(int enemy)
	{
		if (!_attack.KeepsBack)
			return AttackDistance;
		float keepBack = Fight.Enemies.DroneKeepBackOf(enemy);
		return Mathf.Min(AttackDistance + keepBack, Mathf.Max(AttackDistance, WeaponRange - 0.5f));
	}

	/// <summary>The drone is absorbed: gone at once, with no wreckage, and pulled visibly into the enemy.</summary>
	private void AbsorbInto(ref Drone drone, int enemy)
	{
		EnemySwarm enemies = Fight.Enemies;
		drone.Health = 0f;
		if (_absorbingCount == _absorbing.Length)
			Array.Resize(ref _absorbing, _absorbing.Length * 2);
		_absorbing[_absorbingCount++] = new Absorbing
		{
			Start = drone.Position,
			Into = enemies.PositionOf(enemy),
			EnemyId = enemies.IdOf(enemy),
			Yaw = drone.Yaw,
		};
		Fight.Effects.Absorbed(drone.Position, enemies.PositionOf(enemy), Tuning.Drones.Color);
		Fight.Stats.DroneAbsorbed(enemies.TypeIdOf(enemy));
		enemies.Absorb(enemy);
		// The drone itself is cleared away at the start of the next update.
	}

	/// <summary>
	/// Away from the enemies near a drone that it isn't attacking: from each enemy within that enemy's
	/// DroneAvoidDistance of its edge, harder the closer it is, fully once touching. At most length 1.
	/// </summary>
	private Vector2 Avoid(Vector2 position, int targetId)
	{
		EnemySwarm enemies = Fight.Enemies;
		if (Tuning.Drones.AvoidStrength <= 0f || enemies.MaxDroneAvoid <= 0f)
			return Vector2.Zero;
		int count = enemies.CollectTouching(position, enemies.MaxDroneAvoid, _avoidNearby);
		Vector2 push = Vector2.Zero;
		for (int c = 0; c < count; c++)
		{
			int e = _avoidNearby[c];
			if (enemies.IdOf(e) == targetId)
				continue;
			float avoid = enemies.DroneAvoidOf(e);
			if (avoid <= 0f)
				continue;
			Vector2 away = position - enemies.PositionOf(e);
			float distance = away.Length();
			float edge = distance - enemies.RadiusOf(e);
			if (edge >= avoid)
				continue;
			Vector2 direction = distance > 0.001f ? away / distance : Vector2.Right;
			push += direction * (1f - Mathf.Max(0f, edge) / avoid);
		}
		return push.LimitLength(1f);
	}

	/// <summary>Push away from drones that are too close.</summary>
	private Vector2 Separation(int index)
	{
		Vector2 position = _drones[index].Position;
		float radius = Tuning.Drones.SeparationRadius;
		Vector2 separation = Vector2.Zero;

		int nearbyCount = _grid.Query(position, radius, _nearby);
		for (int n = 0; n < nearbyCount; n++)
		{
			int other = _nearby[n];
			if (other == index)
				continue;
			Vector2 away = position - _drones[other].Position;
			float gap = away.Length();
			if (gap < radius)
				separation += (gap > 0.001f ? away / gap : Rng.Direction()) * (1f - gap / radius);
		}
		return separation;
	}

	/// <summary>
	/// What this drone should harvest, or -1. It keeps its current target while that lasts and stays
	/// within range of the VNP. Otherwise it takes the closest to itself, out of those within range.
	/// </summary>
	private int PickHarvestTarget(ref Drone drone, int candidateCount, Vector2 vnpPosition)
	{
		Harvestables harvestables = Fight.Harvestables;

		// The candidates were collected out to the wider keep range.
		if (drone.HarvestId != 0)
		{
			for (int c = 0; c < candidateCount; c++)
			{
				if (harvestables.IdOf(_harvestTargets[c]) == drone.HarvestId)
				{
					if (_harvestUnsafe[c])
						break; // A drone eater has come up beside it: leave it.
					return _harvestTargets[c];
				}
			}
		}

		int best = -1;
		float bestSquared = float.MaxValue;
		for (int c = 0; c < candidateCount; c++)
		{
			int i = _harvestTargets[c];
			if (_harvestUnsafe[c] || harvestables.EdgeDistance(i, vnpPosition) > Tuning.Harvesting.DroneRange)
				continue;
			float distanceSquared = harvestables.PositionOf(i).DistanceSquaredTo(drone.Position);
			if (distanceSquared < bestSquared)
			{
				bestSquared = distanceSquared;
				best = i;
			}
		}
		return best;
	}

	/// <summary>
	/// Which enemy this drone should attack, or -1. It stays on its current enemy while that enemy is
	/// alive and within range of the VNP. Without this, drones in a crowd keep switching target and
	/// never finish anything off. Otherwise it takes the closest to itself, out of those within range.
	/// </summary>
	private int PickEnemyTarget(ref Drone drone, int candidateCount, Vector2 vnpPosition)
	{
		EnemySwarm enemies = Fight.Enemies;

		if (drone.TargetId != 0)
		{
			float keepRange = Tuning.Drones.EngageRange * KeepTargetFactor;
			for (int c = 0; c < candidateCount; c++)
			{
				int i = _enemyTargets[c];
				if (enemies.IdOf(i) != drone.TargetId)
					continue;
				if (enemies.PositionOf(i).DistanceSquaredTo(vnpPosition) <= keepRange * keepRange)
					return i;
				break;
			}
		}

		float range = Tuning.Drones.EngageRange;
		int best = -1;
		float bestSquared = float.MaxValue;
		for (int c = 0; c < candidateCount; c++)
		{
			int i = _enemyTargets[c];
			Vector2 enemyPosition = enemies.PositionOf(i);
			if (enemyPosition.DistanceSquaredTo(vnpPosition) > range * range)
				continue;
			float distanceSquared = enemyPosition.DistanceSquaredTo(drone.Position);
			if (distanceSquared < bestSquared)
			{
				bestSquared = distanceSquared;
				best = i;
			}
		}
		return best;
	}

	/// <summary>The closest asteroid or debris within this drone's harvest beam reach, or -1.</summary>
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
	/// Heads for this drone's own spot on a ring around a point, so a group of drones surrounds it.
	/// The spots slowly turn, so the ring circles.
	/// </summary>
	private Vector2 SteerToSlot(ref Drone drone, Vector2 center, Vector2 centerVelocity, float distance,
		Vector2 separation, float dt)
	{
		float circleSpeed = Mathf.DegToRad(Tuning.Drones.CircleSpeed) * drone.OrbitSign;
		drone.SlotAngle += circleSpeed * dt;
		Vector2 around = new Vector2(Mathf.Cos(drone.SlotAngle), Mathf.Sin(drone.SlotAngle));
		Vector2 slot = center + around * distance;

		// Move with the slot: carried along by the target, and turning around it.
		Vector2 velocity = centerVelocity + new Vector2(-around.Y, around.X) * (circleSpeed * distance);

		// On top of that, close in only as fast as it can still stop at the slot, so it arrives
		// without overshooting and bouncing back. The last couple of units ease out smoothly.
		Vector2 toSlot = slot - drone.Position;
		float slotDistance = toSlot.Length();
		if (slotDistance > 0.001f)
		{
			float braking = Tuning.Drones.Acceleration * Tuning.Drones.ArriveBraking;
			float approach = Mathf.Min(_speed,
				Mathf.Min(Mathf.Sqrt(2f * braking * slotDistance), Mathf.Sqrt(braking) * slotDistance));
			velocity += toSlot / slotDistance * approach;
		}

		return velocity + separation * Tuning.Drones.Separation * Tuning.Drones.FlockSpeed;
	}

	/// <summary>Stay in a band around the VNP, circle it, keep clear of other drones, and keep pace.</summary>
	private Vector2 Flock(ref Drone drone, Vector2 vnpPosition, Vector2 vnpVelocity, Vector2 separation)
	{
		Vector2 toVnp = vnpPosition - drone.Position;
		float vnpDistance = toVnp.Length();
		Vector2 directionToVnp = vnpDistance > 0.001f ? toVnp / vnpDistance : Vector2.Up;

		// Far behind: sprint straight back.
		if (vnpDistance > Tuning.Drones.SprintDistance)
			return directionToVnp * _speed;

		float band = Tuning.Drones.FlockRadius;
		float tooClose = band * 0.45f;
		Vector2 steer = new Vector2(-directionToVnp.Y, directionToVnp.X) * drone.OrbitSign * 0.6f;
		if (vnpDistance > band)
			steer += directionToVnp * Mathf.Min((vnpDistance - band) / band, 1.5f);
		else if (vnpDistance < tooClose)
			steer -= directionToVnp * (1f - vnpDistance / tooClose);
		steer += separation * Tuning.Drones.Separation;

		// Matching the VNP's velocity is what keeps the flock with it instead of trailing.
		return vnpVelocity + steer.LimitLength(1f) * Tuning.Drones.FlockSpeed;
	}

	private static void Face(ref Drone drone, Vector2 point, float turnStep)
	{
		Vector2 toward = point - drone.Position;
		if (toward.LengthSquared() > 0.0001f)
			drone.Yaw = Mathf.RotateToward(drone.Yaw, PlaneMath.YawFacing(toward), turnStep);
	}

	/// <summary>Sends harvest particles from the edge of the material to the drone, and keeps the harvesting sound going.</summary>
	private void HarvestFlow(Vector2 dronePosition, Vector2 itemPosition, float itemRadius, float dt)
	{
		Fight.Sounds.Hold(Sound.HarvestLoop);
		Vector2 toDrone = (dronePosition - itemPosition).Normalized();
		Fight.Effects.HarvestFlow(itemPosition + toDrone * itemRadius * 0.6f, dronePosition,
			Tuning.Look.ExperienceColor, Tuning.Feedback.DroneHarvestParticles, dt, 0.16f, HarvestBeamHeight);
	}

	/// <summary>Records a harvest beam to draw, from under the drone down into the middle of the material,
	/// so neither end shows.</summary>
	private void AddHarvestBeam(Vector2 from, Vector2 to, float endHeight)
	{
		_beamFrom[_beamCount] = from;
		_beamTo[_beamCount] = to;
		_beamIsAttack[_beamCount] = false;
		_beamEndHeight[_beamCount] = endHeight;
		_beamCount++;
	}

	/// <summary>Records an attack beam to draw this frame, ending just inside the edge of its target. For
	/// Drone Weapon attacks that use a beam.</summary>
	public void AddAttackBeam(Vector2 from, Vector2 to, float targetRadius)
	{
		Vector2 along = to - from;
		float distance = along.Length();
		if (distance < 0.01f)
			return;

		_beamFrom[_beamCount] = from;
		_beamTo[_beamCount] = from + along / distance * Mathf.Max(0f, distance - targetRadius * 0.5f);
		_beamIsAttack[_beamCount] = true;
		_beamCount++;
	}

	private void AgeWrecks(float dt)
	{
		for (int i = _absorbingCount - 1; i >= 0; i--)
		{
			ref Absorbing absorbing = ref _absorbing[i];
			absorbing.Age += dt;
			if (Fight.Enemies.TryGetPosition(absorbing.EnemyId, out Vector2 into))
				absorbing.Into = into;
			if (absorbing.Age >= Tuning.Drones.AbsorbTime)
				_absorbing[i] = _absorbing[--_absorbingCount];
		}

		for (int i = _wreckCount - 1; i >= 0; i--)
		{
			_wrecks[i].Age += dt;
			if (_wrecks[i].Age >= Tuning.Drones.WreckTime)
				_wrecks[i] = _wrecks[--_wreckCount];
		}
	}

	private void Draw()
	{
		_batch.Begin(Count + _wreckCount + _absorbingCount);

		for (int i = 0; i < Count; i++)
		{
			ref Drone drone = ref _drones[i];
			Color color = Tuning.Drones.Color;
			float scale = Tuning.Drones.Size;

			// A newly built drone grows in from a white spark.
			if (drone.Age < BuildTime)
			{
				float grown = drone.Age / BuildTime;
				color = Colors.White.Lerp(color, grown);
				scale *= grown;
			}
			if (drone.Flash > 0f)
				color = color.Lerp(new Color(1f, 0.2f, 0.15f), drone.Flash / Tuning.Feedback.HitFlashTime);

			_batch.Set(i, drone.Position, 0f, drone.Yaw, scale, color);
		}

		// Wreckage: flares white, then spins and shrinks away to nothing.
		for (int w = 0; w < _wreckCount; w++)
		{
			float gone = _wrecks[w].Age / Tuning.Drones.WreckTime;
			Color color = Colors.White.Lerp(Tuning.Drones.Color.Darkened(0.6f), gone);
			_batch.Set(Count + w, _wrecks[w].Position, 0f, _wrecks[w].Yaw + gone * 6f, Tuning.Drones.Size * (1f - gone), color);
		}

		// Absorbed drones: pulled into the enemy, speeding up as they go, turning grey and shrinking away.
		for (int a = 0; a < _absorbingCount; a++)
		{
			ref Absorbing absorbing = ref _absorbing[a];
			float t = Mathf.Clamp(absorbing.Age / Mathf.Max(0.01f, Tuning.Drones.AbsorbTime), 0f, 1f);
			float pulled = t * t;
			Vector2 at = absorbing.Start.Lerp(absorbing.Into, pulled);
			Vector2 toward = absorbing.Into - absorbing.Start;
			float yaw = toward.LengthSquared() > 0.0001f ? PlaneMath.YawFacing(toward) : absorbing.Yaw;
			Color color = Tuning.Drones.Color.Lerp(new Color(0.55f, 0.57f, 0.6f), t);
			_batch.Set(Count + _wreckCount + a, at, 0f, yaw, Tuning.Drones.Size * (1f - 0.8f * pulled), color);
		}

		_batch.Commit(Count + _wreckCount + _absorbingCount);

		_beamBatch.Begin(_beamCount);
		for (int b = 0; b < _beamCount; b++)
		{
			if (_beamIsAttack[b])
				_beamBatch.SetBeam(b, _beamFrom[b], _beamTo[b], BeamHeight + 0.1f, 0.2f * Power, 0.06f, Weapon.Color);
			else
				_beamBatch.SetTransform(b, PlaneMath.Bar(PlaneMath.ToWorld(_beamFrom[b], HarvestBeamHeight),
					PlaneMath.ToWorld(_beamTo[b], _beamEndHeight[b]), 0.12f, 0.06f), Tuning.Look.ExperienceColor);
		}
		_beamBatch.Commit(_beamCount);
	}
}
