using Arbiter;
using Godot;
using System.Collections.Generic;

/// <summary>
/// The Lancer, a Grey Goo type. Everything shared by Grey Goo (GreyGoo.cs) applies, but it doesn't drain drones,
/// and it faces the VNP rather than turning slowly. Its own part is its launch:
/// - drifting: a teardrop-shaped blob drifting after the VNP, its point turned toward it once it has noticed the
///   VNP, and facing where it wanders before that;
/// - forming: once the VNP is within launch range and its cooldown is done, it stops and forms itself into a
///   spike, glowing paler, with its aim set as it starts, leading the VNP by how accurate it is;
/// - launching: it shoots along that line, past the point it aimed at by its launch beyond, carrying on through
///   what it meets, damaging the VNP and each drone in its path once;
/// - recovering: it turns back into a blob, then drifts again until its cooldown is done.
/// While forming and launching it declares the line to pilots as a hazard that lands when the launch does, so a
/// pilot can step off it. Its numbers are in lancer.tres.
/// </summary>
public sealed class Lancer : GreyGoo
{
	private const int Drifting = 0, Forming = 1, Launching = 2, Recovering = 3;

	private readonly LancerDefinition _type;
	// The drones each Lancer's current launch has hit, by its ID, so each is hit once.
	private readonly Dictionary<int, HashSet<int>> _dronesHit = new();
	// Each forming Lancer's wind-up sound, by its ID, stopped when it launches or dies.
	private readonly Dictionary<int, SoundHandle> _formSounds = new();

	public Lancer(EnemySwarm swarm, LancerDefinition definition) : base(swarm, definition)
	{
		_type = definition;
	}

	public override Mesh Mesh => Placeholders.Mesh("lancer");

	protected override bool Spins => false;

	/// <summary>While launching, the launch is what hits, so its touch doesn't hurt as well.</summary>
	protected override bool TouchHurts(in EnemySwarm.Enemy enemy) => enemy.AttackPhase != Launching;

	protected override ulong OwnTags => ThreatTags.Launches;

	/// <summary>Seconds a launch of this length takes.</summary>
	private float SecondsFor(float length) => length / Mathf.Max(0.01f, _type.LaunchSpeed);

	public override void Spawned(ref EnemySwarm.Enemy enemy)
	{
		base.Spawned(ref enemy);
		enemy.AttackDamage = _type.LaunchDamage * Fight.GreyGooDamageScale;
		float roll = Mathf.Pow(Rng.Value(), Mathf.Max(0.01f, _type.SharpshooterRarity));
		enemy.Accuracy = Mathf.Clamp(roll + Fight.EnemyAccuracyBonus, 0f, 1f);
		enemy.AttackPhase = Drifting;
		enemy.AttackTimer = _type.LaunchCooldown * Rng.Range(0.3f, 1f); // So a group doesn't launch at once.
	}

	/// <summary>It drifts straight after the VNP between launches, and holds still otherwise: its launch moves it.</summary>
	public override Vector2 Chase(ref EnemySwarm.Enemy enemy, Vector2 track, float distance)
	{
		if (enemy.AttackPhase != Drifting)
			return Vector2.Zero;
		Vector2 toVnp = Fight.Vnp.PlanePosition - enemy.Position;
		return toVnp.LengthSquared() > 0.01f ? toVnp.Normalized() : track;
	}

	protected override bool ActOwn(ref EnemySwarm.Enemy enemy, int index, in EnemySwarm.Target target, float dt)
	{
		Vnp vnp = Fight.Vnp;
		enemy.AttackTimer -= dt;
		switch (enemy.AttackPhase)
		{
			case Drifting:
				// Idle, it faces where it wanders, as other enemies do; only once it has noticed the VNP does it
				// turn its point toward it.
				if (!enemy.Aggro)
					return false;
				Face(ref enemy, vnp.PlanePosition - enemy.Position, dt);
				if (enemy.AttackTimer <= 0f && !vnp.IsDestroyed
					&& enemy.Position.DistanceTo(vnp.PlanePosition) <= _type.LaunchRange)
					StartForming(ref enemy, vnp);
				break;
			case Forming:
				Face(ref enemy, enemy.AttackDirection, dt);
				if (enemy.AttackTimer <= 0f)
					StartLaunch(ref enemy);
				break;
			case Launching:
				Launch(ref enemy, vnp, dt);
				break;
			case Recovering:
				if (enemy.AttackTimer <= 0f)
				{
					enemy.AttackPhase = Drifting;
					enemy.AttackTimer = _type.LaunchCooldown * Rng.Range(0.85f, 1.15f);
				}
				break;
		}
		return true;
	}

	/// <summary>Turns toward a direction at its turn rate.</summary>
	private void Face(ref EnemySwarm.Enemy enemy, Vector2 direction, float dt)
	{
		if (direction.LengthSquared() > 0.0001f)
			enemy.Yaw = Mathf.RotateToward(enemy.Yaw, PlaneMath.YawFacing(direction), Mathf.DegToRad(_type.TurnSpeed) * dt);
	}

	/// <summary>
	/// Starts forming its spike, with its aim set now: ahead of the VNP by its accuracy, where 1 leads to where the
	/// VNP will be when the spike arrives if it keeps going and 0 aims where it is, then straying a little, less
	/// for an accurate Lancer.
	/// </summary>
	private void StartForming(ref EnemySwarm.Enemy enemy, Vnp vnp)
	{
		Vector2 vnpLater = vnp.PlanePosition + vnp.Velocity * _type.FormSeconds;
		Vector2 meeting = PlaneMath.AimAhead(enemy.Position, vnpLater, vnp.Velocity, _type.LaunchSpeed,
			SecondsFor(_type.LaunchRange + _type.LaunchBeyond));
		float lead = Mathf.Lerp(_type.AimLeadMin, _type.AimLeadMax, enemy.Accuracy);
		Vector2 aimAt = vnp.PlanePosition.Lerp(meeting, lead);
		Vector2 aim = aimAt.DistanceSquaredTo(enemy.Position) > 0.0001f
			? (aimAt - enemy.Position).Normalized()
			: (vnp.PlanePosition - enemy.Position).Normalized();
		float spread = Mathf.DegToRad(Mathf.Lerp(_type.AimSpreadMax, _type.AimSpreadMin, enemy.Accuracy));
		enemy.AttackDirection = aim.Rotated(Rng.Range(-spread, spread));
		// It carries on past the point it aimed at, by LaunchBeyond.
		enemy.AttackLength = enemy.Position.DistanceTo(aimAt) + _type.LaunchBeyond;
		enemy.AttackPhase = Forming;
		enemy.AttackTimer = _type.FormSeconds;
		_formSounds[enemy.Id] = Fight.Sounds.Play(Sound.LancerForm);
	}

	/// <summary>Stops a Lancer's wind-up sound, if it is still playing: when it launches, or dies forming.</summary>
	private void StopFormSound(int id)
	{
		if (_formSounds.Remove(id, out SoundHandle sound))
			Fight.Sounds.Stop(sound);
	}

	private void StartLaunch(ref EnemySwarm.Enemy enemy)
	{
		StopFormSound(enemy.Id);
		enemy.AttackPhase = Launching;
		enemy.AttackTimer = SecondsFor(enemy.AttackLength);
		enemy.AttackFrom = enemy.Position;
		enemy.AttackHitVnp = false;
		if (!_dronesHit.TryGetValue(enemy.Id, out HashSet<int> hit))
			_dronesHit[enemy.Id] = hit = new HashSet<int>();
		hit.Clear();
		Fight.Sounds.Play(Sound.LancerLaunch);
		Fight.Effects.Impact(enemy.Position, _type.Color, 0.8f);
	}

	/// <summary>One frame of the launch: it moves along its line at launch speed, whatever the swarm's steering
	/// did, and hits the VNP and each drone the stretch it covered passes, once per launch.</summary>
	private void Launch(ref EnemySwarm.Enemy enemy, Vnp vnp, float dt)
	{
		float seconds = SecondsFor(enemy.AttackLength);
		float done = Mathf.Clamp(seconds - Mathf.Max(0f, enemy.AttackTimer), 0f, seconds);
		float travelled = done * _type.LaunchSpeed;
		Vector2 from = enemy.AttackFrom + enemy.AttackDirection * Mathf.Max(0f, travelled - _type.LaunchSpeed * dt);
		Vector2 to = enemy.AttackFrom + enemy.AttackDirection * travelled;
		enemy.Position = to;
		enemy.Velocity = enemy.AttackDirection * _type.LaunchSpeed;
		enemy.Yaw = PlaneMath.YawFacing(enemy.AttackDirection);

		if (!enemy.AttackHitVnp && !vnp.IsDestroyed
			&& PlaneMath.DistanceToSegment(vnp.PlanePosition, from, to) <= enemy.Radius + Tuning.Vnp.HitRadius)
		{
			vnp.TakeDamage(enemy.AttackDamage, Source);
			enemy.AttackHitVnp = true;
			Fight.Effects.Impact(vnp.PlanePosition, _type.Color, 1.2f);
		}
		if (_dronesHit.TryGetValue(enemy.Id, out HashSet<int> hit))
			Fight.Drones.HitAlong(from, to, enemy.Radius, enemy.AttackDamage, Source, hit);

		if (enemy.AttackTimer <= 0f)
		{
			enemy.AttackPhase = Recovering;
			enemy.AttackTimer = _type.RecoverSeconds;
			enemy.Velocity = enemy.AttackDirection * _type.Speed; // It glides on a little as it slows.
		}
	}

	/// <summary>While forming and launching, the line it will fly along: landing when the launch does, from where
	/// it is to where the launch ends, as wide as it is. A pilot that sees it forming can step off the line.</summary>
	public override void DeclareThreats(in EnemySwarm.Enemy enemy, Situation situation)
	{
		base.DeclareThreats(enemy, situation);
		if (enemy.AttackPhase != Forming && enemy.AttackPhase != Launching)
			return;
		bool forming = enemy.AttackPhase == Forming;
		Vector2 end = (forming ? enemy.Position : enemy.AttackFrom) + enemy.AttackDirection * enemy.AttackLength;
		float landsIn = forming ? Mathf.Max(0f, enemy.AttackTimer) : 0f;
		float lasts = forming ? SecondsFor(enemy.AttackLength) : Mathf.Max(0f, enemy.AttackTimer);
		situation.Add(Hazard.Timed(Area.Capsule(enemy.Position.ToArbiter(), end.ToArbiter(), enemy.Radius),
			landsIn, lasts, enemy.AttackDamage, Endangers.Both, enemy.Id));
	}

	public override void Died(in EnemySwarm.Enemy enemy)
	{
		_dronesHit.Remove(enemy.Id);
		StopFormSound(enemy.Id);
		base.Died(enemy);
	}

	/// <summary>How far it is formed into a spike: 0 a blob, 1 fully formed.</summary>
	private float Spike(in EnemySwarm.Enemy enemy) => enemy.AttackPhase switch
	{
		Forming => 1f - Mathf.Clamp(enemy.AttackTimer / Mathf.Max(0.01f, _type.FormSeconds), 0f, 1f),
		Launching => 1f,
		Recovering => Mathf.Clamp(enemy.AttackTimer / Mathf.Max(0.01f, _type.RecoverSeconds), 0f, 1f),
		_ => 0f,
	};

	/// <summary>Formed into a spike, it stretches along its aim and narrows.</summary>
	protected override Basis Reshape(in EnemySwarm.Enemy enemy, Basis shape, float time)
	{
		float spike = Spike(enemy);
		if (spike <= 0f)
			return shape;
		return StretchAlong(enemy.AttackDirection, 1f + _type.SpikeStretch * spike, 1f - 0.45f * spike) * shape;
	}

	/// <summary>It glows paler as it forms and while it launches, so the warning shows.</summary>
	public override void Draw(int slot, in EnemySwarm.Enemy enemy, Color color)
	{
		float glow = enemy.AttackPhase is Forming or Launching ? Spike(enemy) * _type.FormGlow : 0f;
		base.Draw(slot, enemy, glow > 0f ? color.Lerp(Colors.White, glow) : color);
	}
}
