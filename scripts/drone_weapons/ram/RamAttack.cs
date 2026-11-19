using Godot;

/// <summary>
/// Ram: drones charge through their target, damaging it on impact, then arc back round and charge again.
///
/// Each drone goes through these states, kept in its AttackState:
/// - Circling: it closes in like the other Drone Weapons, steered by the swarm, until the target is within
///   range.
/// - Arcing: it never stops or turns on the spot. It eases back to normal speed and turns at a limited
///   rate, circling the target while on cooldown, and swinging round to face it when ready.
/// - Charging: once it faces the target, it flies at it at charge speed, homing a little, and damages it on
///   contact, then carries on through and arcs round again. Ramming does not hurt the drone.
/// - Finishing: its target is gone with no other enemy in reach, so it finishes its arc before the swarm
///   steers it normally again.
/// While arcing, charging or finishing, Ram sets the drone's velocity itself.
/// </summary>
public sealed class RamAttack : DroneAttack
{
	private enum RamState { Circling, Arcing, Charging, Finishing }

	// A charge that has not reached its target in this long gives up and arcs round.
	private const float MaxChargeSeconds = 1f;
	// After a charge, a drone arcs for at least this long before charging again, so it carries on through.
	private const float MinArcSeconds = 0.25f;
	// A drone charges only once it is heading within this many degrees of its target.
	private const float ChargeAlignmentDegrees = 15f;
	// While on cooldown, a ramming drone circles its target at this many of its own turning radii, so the
	// target stays outside the circle it turns on and it can turn straight in. Above 2 is needed.
	private const float RamOrbitTurns = 2.2f;
	// Once lined up, a ramming drone charges from up to this many times its weapon range.
	private const float MaxChargeRangeScale = 1.5f;

	public RamAttack(DroneSwarm swarm) : base(swarm)
	{
	}

	/// <summary>Ram's turn rate while arcing back round, in degrees a second. Attack rate upgrades raise it,
	/// so the loop gets tighter and quicker along with the cooldown: otherwise the loop, not the cooldown,
	/// sets how often a drone rams, and attack rate would barely help.</summary>
	private float ArcTurnRate => Weapon.ArcTurnRate * Swarm.AttackRateMultiplier;

	/// <summary>How quickly a ramming drone slows back to normal speed after a charge. Raised by attack rate
	/// upgrades with the turn rate, so the tighter loop keeps its shape.</summary>
	private float ArcSlowdown => Weapon.ArcSlowdown * Swarm.AttackRateMultiplier;

	private static RamState StateOf(in DroneSwarm.Drone drone) => (RamState)drone.AttackState;

	private static void SetState(ref DroneSwarm.Drone drone, RamState state, float time = 0f)
	{
		drone.AttackState = (int)state;
		drone.AttackTime = time;
	}

	/// <summary>A new target ends a charge, but the drone keeps its momentum and arcs round toward the new one.</summary>
	public override void TargetChanged(ref DroneSwarm.Drone drone)
	{
		if (StateOf(drone) == RamState.Charging)
			drone.AttackState = (int)RamState.Arcing;
	}

	public override bool Attack(ref DroneSwarm.Drone drone, int enemy, ref Vector2 wantedVelocity, float dt)
	{
		EnemySwarm enemies = Fight.Enemies;
		Vector2 enemyPosition = enemies.PositionOf(enemy);
		Vector2 toEnemy = enemyPosition - drone.Position;
		float distance = toEnemy.Length();
		Vector2 direction = distance > 0.001f ? toEnemy / distance : Vector2.Up;
		drone.AttackTime += dt;

		float speed = drone.Velocity.Length();
		Vector2 heading = speed > 0.1f ? drone.Velocity / speed : direction;

		// A drone finishing its arc after losing a target picks the arc up again for a new one.
		if (StateOf(drone) == RamState.Finishing)
			drone.AttackState = (int)RamState.Arcing;

		switch (StateOf(drone))
		{
			case RamState.Circling:
				if (distance > Swarm.WeaponRange)
					return false;
				SetState(ref drone, RamState.Arcing, MinArcSeconds);
				goto case RamState.Arcing;

			case RamState.Arcing:
			{
				bool ready = drone.FireTimer <= 0f;
				float turnRate = Mathf.DegToRad(ArcTurnRate);
				// Start turning in a quarter turn before the cooldown ends, so the drone is lined up when it does.
				bool turningIn = drone.FireTimer <= Mathf.Pi * 0.5f / Mathf.Max(0.01f, turnRate);
				Vector2 desired = direction;
				if (turningIn)
				{
					// If the target is inside the circle the drone turns on, turning toward it only circles it.
					// Fly straight instead to open up room, then turn in once the turn can line up a charge.
					float turnRadius = speed / Mathf.Max(0.01f, turnRate);
					float side = heading.Cross(direction) >= 0f ? 1f : -1f;
					Vector2 turnCentre = drone.Position + new Vector2(-heading.Y, heading.X) * side * turnRadius;
					if (turnCentre.DistanceTo(enemyPosition) < turnRadius)
						desired = heading;
				}
				else
				{
					// On cooldown: circle the target. Wide enough that the target stays outside the circle the
					// drone turns on, so it can turn straight in when it is ready, rather than having to fly
					// out and back first.
					float orbit = Mathf.Max(Swarm.AttackDistance, RamOrbitTurns * Swarm.Speed / Mathf.Max(0.01f, turnRate));
					var tangent = new Vector2(-direction.Y, direction.X) * drone.OrbitSign;
					float radial = Mathf.Clamp((distance - orbit) / orbit, -1f, 1f);
					desired = (tangent + direction * radial).Normalized();
				}
				heading = TurnToward(heading, desired, turnRate * dt, drone.OrbitSign);
				speed = Mathf.MoveToward(speed, Swarm.Speed, ArcSlowdown * dt);

				// Range only decides when ramming starts. Once lined up, a drone charges from a little further.
				if (ready && distance <= Swarm.WeaponRange * MaxChargeRangeScale && drone.AttackTime >= MinArcSeconds
					&& Mathf.RadToDeg(Mathf.Abs(heading.AngleTo(direction))) <= ChargeAlignmentDegrees)
				{
					SetState(ref drone, RamState.Charging);
					speed = Weapon.ChargeSpeed;
				}
				break;
			}

			case RamState.Charging:
				if (distance <= Tuning.EnemySpawning.Enemy.HitRadius + Tuning.Drones.HitRadius)
				{
					// A hit: the drone keeps going, straight through the target, then arcs round.
					enemies.Damage(enemy, Swarm.WeaponDamage);
					Fight.Effects.Impact(enemyPosition - direction * 0.5f, Weapon.Color, 0.9f * Swarm.Power);
					Fight.Sounds.Play(Sound.RamHit);
					drone.FireTimer = Swarm.WeaponCooldown * Rng.Range(0.9f, 1.1f);
					SetState(ref drone, RamState.Arcing);
				}
				else if (drone.AttackTime > MaxChargeSeconds)
				{
					SetState(ref drone, RamState.Arcing);
				}
				else
				{
					heading = TurnToward(heading, direction, Mathf.DegToRad(Weapon.ChargeTurnRate) * dt, drone.OrbitSign);
					speed = Weapon.ChargeSpeed;
				}
				break;
		}

		wantedVelocity = heading * speed;
		drone.Yaw = PlaneMath.YawFacing(wantedVelocity);
		return true;
	}

	/// <summary>
	/// For a ramming drone whose target is gone with no other enemy in reach: rather than snapping round to
	/// wherever it now wants to go, it keeps arcing, turning and slowing at Ram's own rates, until it is
	/// heading the way it wants at normal speed, or for at most the time it takes to turn half a circle.
	/// It then steers normally again.
	/// </summary>
	public override bool WithoutTarget(ref DroneSwarm.Drone drone, ref Vector2 wantedVelocity, float dt)
	{
		if (StateOf(drone) == RamState.Circling)
			return false;

		float normalSpeed = Swarm.Speed;
		float speed = drone.Velocity.Length();
		Vector2 wanted = wantedVelocity.LimitLength(normalSpeed);
		if (speed < 0.1f)
		{
			SetState(ref drone, RamState.Circling);
			return false;
		}

		if (StateOf(drone) != RamState.Finishing)
			SetState(ref drone, RamState.Finishing);
		drone.AttackTime += dt;

		Vector2 heading = drone.Velocity / speed;
		Vector2 wantedHeading = wanted.LengthSquared() > 0.01f ? wanted.Normalized() : heading;
		heading = TurnToward(heading, wantedHeading, Mathf.DegToRad(ArcTurnRate) * dt, drone.OrbitSign);
		speed = Mathf.MoveToward(speed, normalSpeed, ArcSlowdown * dt);

		// Close enough to steer normally from here without a visible snap. Or it has had time to turn
		// half a circle: a nearby spot it wants, such as beside a rock it will harvest, can be inside the
		// circle it turns on, so it would loop round it forever without ever lining up.
		float halfTurnSeconds = 180f / Mathf.Max(1f, ArcTurnRate);
		if ((Mathf.RadToDeg(Mathf.Abs(heading.AngleTo(wantedHeading))) <= 20f && speed <= normalSpeed + 1f)
			|| drone.AttackTime > halfTurnSeconds)
		{
			SetState(ref drone, RamState.Circling);
			return false;
		}

		wantedVelocity = heading * speed;
		drone.Yaw = PlaneMath.YawFacing(wantedVelocity);
		return true;
	}

	/// <summary>
	/// Turns a heading toward another by at most <paramref name="maxStep"/> radians. When the wanted
	/// heading is nearly behind, it turns the drone's own way round, so drones split both ways rather than
	/// all choosing the same side.
	/// </summary>
	private static Vector2 TurnToward(Vector2 heading, Vector2 wanted, float maxStep, float sideSign)
	{
		float difference = heading.AngleTo(wanted);
		if (Mathf.Abs(difference) > Mathf.DegToRad(150f))
			difference = Mathf.Abs(difference) * sideSign;
		return heading.Rotated(Mathf.Clamp(difference, -maxStep, maxStep));
	}
}
