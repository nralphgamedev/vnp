using Arbiter;
using Godot;
using System.Collections.Generic;

/// <summary>
/// The Spiker, a Grey Goo type. Everything shared by Grey Goo (GreyGoo.cs) applies; it drains no drones by touch,
/// but grows with each drone it absorbs, and never splits. Its own part is its thrust:
/// - drifting: a caltrop-shaped blob drifting after the VNP, which it hurts by touch;
/// - forming: once a drone or the VNP is within its reach and its cooldown is done, it stops, swells and glows
///   paler, and picks its targets (MinSpikes to MaxSpikes): the nearest drones in reach that no other spike is
///   after, or the VNP if too few drones are. A spike at the VNP has its line set now, so moving off it dodges;
/// - thrusting: the spikes shoot out, those at drones aimed again as they go and swinging toward their drone as
///   they extend (TrackDegrees). Every spike leads its target and strays by the Spiker's accuracy, which it rolls as
///   it spawns and the difficulty ramp raises, as the Lancer does. The first drone a spike's tip meets is impaled
///   on it: it stops fighting and is drawn back in at PullSpeed, then absorbed. A spike that crosses the VNP damages it once and carries on,
///   since the VNP can't be impaled. An empty spike comes back at once.
/// It forms again once every spike is back in. If it is destroyed first, its impaled drones are freed.
/// While forming and thrusting it declares each spike's line to pilots. Its numbers are in spiker.tres.
/// </summary>
public sealed class Spiker : GreyGoo
{
	private const int Drifting = 0, Forming = 1, Thrusting = 2;

	private struct Spike
	{
		public Vector2 Direction;
		public float Length;     // From its centre to the tip.
		public float MaxLength;
		public bool Pulling;     // On its way back in.
		public bool HitVnp;
		public int Held;         // The drone impaled on it, by ID, or 0.
		public int Target;       // The drone it is aimed at, by ID, or 0 for the VNP.
	}

	private readonly SpikerDefinition _type;
	// Each Spiker's spikes, by its ID, while forming or thrusting.
	private readonly Dictionary<int, List<Spike>> _spikes = new();
	private readonly int[] _drones = new int[64];
	private readonly List<(float Distance, Vector2 Position, int Id)> _nearest = new();
	// Drones a spike on its way out is aimed at, by ID, so two Spikers don't both go for the same drone.
	private readonly HashSet<int> _claimed = new();
	private InstanceBatch _spikeBatch;
	private int _spikeCount;
	private int _spikesDrawn;

	public Spiker(EnemySwarm swarm, SpikerDefinition definition) : base(swarm, definition)
	{
		_type = definition;
	}

	public override Mesh Mesh => Placeholders.Mesh("spiker");

	public override void Spawned(ref EnemySwarm.Enemy enemy)
	{
		base.Spawned(ref enemy);
		enemy.AttackDamage = _type.SpikeDamage * Fight.GreyGooDamageScale;
		enemy.AttackPhase = Drifting;
		enemy.AttackTimer = _type.ThrustCooldown * Rng.Range(0.3f, 1f); // So a group doesn't thrust at once.
		float roll = Mathf.Pow(Rng.Value(), Mathf.Max(0.01f, _type.SharpshooterRarity));
		enemy.Accuracy = Mathf.Clamp(roll + Fight.EnemyAccuracyBonus, 0f, 1f);
	}

	/// <summary>It drifts straight after its target, and holds still while forming and thrusting.</summary>
	public override Vector2 Chase(ref EnemySwarm.Enemy enemy, Vector2 track, float distance) =>
		enemy.AttackPhase == Drifting ? track : Vector2.Zero;

	protected override bool ActOwn(ref EnemySwarm.Enemy enemy, int index, in EnemySwarm.Target target, float dt)
	{
		switch (enemy.AttackPhase)
		{
			case Drifting:
				enemy.AttackTimer -= dt;
				if (enemy.AttackTimer <= 0f && enemy.Aggro && !Fight.Vnp.IsDestroyed)
					StartForming(ref enemy);
				break;
			case Forming:
				enemy.AttackTimer -= dt;
				if (enemy.AttackTimer <= 0f)
				{
					AimAtDrones(enemy);
					enemy.AttackPhase = Thrusting;
					Fight.Sounds.Play(Sound.SpikerThrust);
				}
				break;
			case Thrusting:
				Thrust(ref enemy, index, dt);
				break;
		}
		return false;
	}

	/// <summary>Picks its targets, drones first, and sets each spike's line, or does nothing if no drone or the VNP
	/// is in reach.</summary>
	private void StartForming(ref EnemySwarm.Enemy enemy)
	{
		float reach = enemy.Radius + _type.Reach;
		_nearest.Clear();
		int found = Fight.Drones.CollectNear(enemy.Position, reach, _drones);
		for (int c = 0; c < found; c++)
		{
			if (Fight.Drones.IsHeld(_drones[c]) || _claimed.Contains(Fight.Drones.IdOf(_drones[c])))
				continue;
			Vector2 p = Fight.Drones.PositionOf(_drones[c]);
			_nearest.Add((p.DistanceTo(enemy.Position), p, Fight.Drones.IdOf(_drones[c])));
		}
		_nearest.Sort((a, b) => a.Distance.CompareTo(b.Distance));
		Vector2 vnp = Fight.Vnp.PlanePosition;
		bool vnpInReach = vnp.DistanceTo(enemy.Position) <= reach + Tuning.Vnp.HitRadius;
		if (_nearest.Count == 0 && !vnpInReach)
			return;

		if (!_spikes.TryGetValue(enemy.Id, out List<Spike> spikes))
			_spikes[enemy.Id] = spikes = new List<Spike>();
		_spikeCount -= spikes.Count;
		spikes.Clear();
		int count = Rng.Int(Mathf.Max(1, _type.MinSpikes), Mathf.Max(1, Mathf.Max(_type.MinSpikes, _type.MaxSpikes)));
		for (int n = 0; n < _nearest.Count && spikes.Count < count; n++)
		{
			// Aimed properly as the thrust starts (AimAtDrones); for now, at where the drone is.
			var spike = new Spike { Target = _nearest[n].Id };
			_claimed.Add(spike.Target);
			Aim(ref spike, enemy, _nearest[n].Position);
			spikes.Add(spike);
		}
		if (spikes.Count < count && vnpInReach)
			AimAtVnp(spikes, enemy);
		_spikeCount += spikes.Count;
		enemy.AttackPhase = Forming;
		enemy.AttackTimer = _type.FormSeconds;
	}

	/// <summary>
	/// A spike at the VNP, its line set now, so moving off it dodges: it leads the VNP by the Spiker's accuracy,
	/// toward where the VNP will be when the spike gets there (it moves on while the Spiker forms, then while the tip
	/// shoots out), and strays by it too.
	/// </summary>
	private void AimAtVnp(List<Spike> spikes, in EnemySwarm.Enemy enemy)
	{
		Vnp vnp = Fight.Vnp;
		float speed = Mathf.Max(0.01f, _type.SpikeSpeed);
		Vector2 vnpLater = vnp.PlanePosition + vnp.Velocity * _type.FormSeconds;
		Vector2 meeting = PlaneMath.AimAhead(enemy.Position, vnpLater, vnp.Velocity, speed, Farthest(enemy) / speed);
		Vector2 aimAt = vnp.PlanePosition.Lerp(meeting, Lead(enemy));
		var spike = new Spike();
		Aim(ref spike, enemy, enemy.Position + (aimAt - enemy.Position).Rotated(Stray(enemy)));
		spikes.Add(spike);
	}

	/// <summary>How far it leads a moving target, and a random stray, both by its accuracy.</summary>
	private float Lead(in EnemySwarm.Enemy enemy) => Mathf.Lerp(_type.AimLeadMin, _type.AimLeadMax, enemy.Accuracy);

	private float Stray(in EnemySwarm.Enemy enemy)
	{
		float spread = Mathf.DegToRad(Mathf.Lerp(_type.AimSpreadMax, _type.AimSpreadMin, enemy.Accuracy));
		return Rng.Range(-spread, spread);
	}

	/// <summary>The farthest a spike can reach from its centre.</summary>
	private float Farthest(in EnemySwarm.Enemy enemy) => enemy.Radius + _type.Reach + _type.SpikeBeyond;

	/// <summary>
	/// As the spikes go out, each one at a drone is aimed again, ahead of where the drone is heading by the Spiker's
	/// lead of the way it will move before the tip gets there, and strayed a little. One whose drone is gone, or
	/// already caught, goes for the nearest other drone in reach that no spike is after, or keeps its line if there
	/// is none.
	/// </summary>
	private void AimAtDrones(in EnemySwarm.Enemy enemy)
	{
		if (!_spikes.TryGetValue(enemy.Id, out List<Spike> spikes))
			return;
		DroneSwarm drones = Fight.Drones;
		float speed = Mathf.Max(0.01f, _type.SpikeSpeed);
		for (int s = 0; s < spikes.Count; s++)
		{
			Spike spike = spikes[s];
			if (spike.Target == 0)
				continue;
			int drone = drones.IndexOfId(spike.Target);
			if (drone < 0 || drones.IsHeld(drone))
			{
				_claimed.Remove(spike.Target);
				drone = NearestFree(enemy);
				spike.Target = drone >= 0 ? drones.IdOf(drone) : 0;
				spikes[s] = spike;
				if (drone < 0)
					continue;
				_claimed.Add(spike.Target);
			}
			Vector2 at = drones.PositionOf(drone);
			Vector2 velocity = drones.VelocityOf(drone);
			// Where it will be when the tip arrives, found by stepping the arrival time twice.
			float time = (at.DistanceTo(enemy.Position) - enemy.Radius) / speed;
			for (int pass = 0; pass < 2; pass++)
				time = Mathf.Max(0f, ((at + velocity * time).DistanceTo(enemy.Position) - enemy.Radius) / speed);
			Vector2 led = at + velocity * time * Lead(enemy);
			Aim(ref spike, enemy, enemy.Position + (led - enemy.Position).Rotated(Stray(enemy)));
			spikes[s] = spike;
		}
	}

	/// <summary>The nearest drone within its reach that isn't held and no spike is after, or -1.</summary>
	private int NearestFree(in EnemySwarm.Enemy enemy)
	{
		DroneSwarm drones = Fight.Drones;
		int found = drones.CollectNear(enemy.Position, enemy.Radius + _type.Reach, _drones);
		int best = -1;
		float bestDistance = float.MaxValue;
		for (int c = 0; c < found; c++)
		{
			int d = _drones[c];
			if (drones.IsHeld(d) || _claimed.Contains(drones.IdOf(d)))
				continue;
			float distance = drones.PositionOf(d).DistanceSquaredTo(enemy.Position);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = d;
			}
		}
		return best;
	}

	/// <summary>A spike at a drone swings toward it, at up to TrackDegrees a second, and reaches past it.</summary>
	private void Track(ref Spike spike, in EnemySwarm.Enemy enemy, float dt)
	{
		if (spike.Target == 0 || _type.TrackDegrees <= 0f)
			return;
		DroneSwarm drones = Fight.Drones;
		int drone = drones.IndexOfId(spike.Target);
		if (drone < 0 || drones.IsHeld(drone))
			return;
		Vector2 toward = drones.PositionOf(drone) - enemy.Position;
		if (toward.LengthSquared() < 0.0001f)
			return;
		float angle = Mathf.RotateToward(spike.Direction.Angle(), toward.Angle(), Mathf.DegToRad(_type.TrackDegrees) * dt);
		spike.Direction = Vector2.FromAngle(angle);
		spike.MaxLength = Mathf.Min(Farthest(enemy), Mathf.Max(spike.MaxLength, toward.Length() + _type.SpikeBeyond));
	}

	/// <summary>Points a spike at a point, reaching a little past it, but no farther than its reach allows.</summary>
	private void Aim(ref Spike spike, in EnemySwarm.Enemy enemy, Vector2 at)
	{
		Vector2 toward = at - enemy.Position;
		spike.Direction = toward.LengthSquared() > 0.0001f ? toward.Normalized() : Vector2.Up;
		spike.Length = enemy.Radius;
		spike.MaxLength = Mathf.Min(Mathf.Max(enemy.Radius, toward.Length()) + _type.SpikeBeyond, Farthest(enemy));
	}

	/// <summary>Moves each spike out or back, impaling, hitting the VNP and absorbing as it goes.</summary>
	private void Thrust(ref EnemySwarm.Enemy enemy, int index, float dt)
	{
		if (!_spikes.TryGetValue(enemy.Id, out List<Spike> spikes))
		{
			EndThrust(ref enemy);
			return;
		}
		DroneSwarm drones = Fight.Drones;
		Vnp vnp = Fight.Vnp;
		for (int s = spikes.Count - 1; s >= 0; s--)
		{
			Spike spike = spikes[s];
			if (!spike.Pulling)
			{
				Track(ref spike, enemy, dt);
				Vector2 from = enemy.Position + spike.Direction * spike.Length;
				spike.Length = Mathf.Min(spike.MaxLength, spike.Length + _type.SpikeSpeed * dt);
				Vector2 tip = enemy.Position + spike.Direction * spike.Length;

				// The VNP is hit once and the spike carries on; the first drone met is impaled.
				if (!spike.HitVnp && !vnp.IsDestroyed
					&& PlaneMath.DistanceToSegment(vnp.PlanePosition, from, tip) <= _type.SpikeWidth + Tuning.Vnp.HitRadius)
				{
					spike.HitVnp = true;
					vnp.TakeDamage(enemy.AttackDamage, Source);
					Fight.Effects.Impact(vnp.PlanePosition, _type.Color, 0.8f);
				}
				int drone = drones.FindImpalable(from, tip, _type.SpikeWidth);
				if (drone >= 0)
				{
					spike.Held = drones.IdOf(drone);
					spike.Length = Mathf.Max(enemy.Radius, drones.PositionOf(drone).DistanceTo(enemy.Position));
					drones.Hold(drone, enemy.Id, enemy.Position + spike.Direction * spike.Length);
					Fight.Effects.Impact(drones.PositionOf(drone), _type.Color, 0.6f);
					Fight.Sounds.Play(Sound.Hit, 0.6f);
					spike.Pulling = true;
					_claimed.Remove(spike.Target);
				}
				else if (spike.Length >= spike.MaxLength)
				{
					spike.Pulling = true;
					_claimed.Remove(spike.Target);
				}
			}
			else
			{
				int drone = spike.Held != 0 ? drones.IndexOfId(spike.Held) : -1;
				if (spike.Held != 0 && drone < 0)
					spike.Held = 0; // Destroyed on the spike, by something else.
				spike.Length -= (spike.Held != 0 ? _type.PullSpeed : _type.SpikeSpeed) * dt;
				if (spike.Length <= enemy.Radius)
				{
					if (drone >= 0)
						drones.AbsorbHeld(drone, index);
					spikes.RemoveAt(s);
					_spikeCount--;
					continue;
				}
				if (drone >= 0)
					drones.Hold(drone, enemy.Id, enemy.Position + spike.Direction * spike.Length);
			}
			spikes[s] = spike;
		}
		if (spikes.Count == 0)
			EndThrust(ref enemy);
	}

	private void EndThrust(ref EnemySwarm.Enemy enemy)
	{
		enemy.AttackPhase = Drifting;
		enemy.AttackTimer = _type.ThrustCooldown * Rng.Range(0.85f, 1.15f);
	}

	/// <summary>
	/// While forming, each spike's whole line, landing as the thrust starts (a spike at a drone toward where that
	/// drone is then, since it is aimed again as it goes); while thrusting, what is left of each spike's way out,
	/// from its tip. Both hurt the VNP and drones. A spike on its way back hurts nothing.
	/// </summary>
	public override void DeclareThreats(in EnemySwarm.Enemy enemy, Situation situation)
	{
		base.DeclareThreats(enemy, situation);
		if (enemy.AttackPhase == Drifting || !_spikes.TryGetValue(enemy.Id, out List<Spike> spikes))
			return;
		float speed = Mathf.Max(0.01f, _type.SpikeSpeed);
		float landsIn = enemy.AttackPhase == Forming ? Mathf.Max(0f, enemy.AttackTimer) : 0f;
		foreach (Spike spike in spikes)
		{
			if (spike.Pulling)
				continue;
			Vector2 from = enemy.Position + spike.Direction * spike.Length;
			Vector2 to = enemy.Position + spike.Direction * spike.MaxLength;
			situation.Add(Hazard.Timed(Area.Capsule(from.ToArbiter(), to.ToArbiter(), _type.SpikeWidth),
				landsIn, (spike.MaxLength - spike.Length) / speed, enemy.AttackDamage, Endangers.Both, enemy.Id));
		}
	}

	public override void Died(in EnemySwarm.Enemy enemy)
	{
		// Its impaled drones are freed.
		if (_spikes.TryGetValue(enemy.Id, out List<Spike> spikes))
		{
			foreach (Spike spike in spikes)
			{
				if (spike.Held != 0)
					Fight.Drones.Release(spike.Held);
				if (!spike.Pulling)
					_claimed.Remove(spike.Target);
			}
			_spikeCount -= spikes.Count;
			_spikes.Remove(enemy.Id);
		}
		base.Died(enemy);
	}

	/// <summary>How far it has formed its thrust: 0 not forming, 1 about to thrust.</summary>
	private float Formed(in EnemySwarm.Enemy enemy) => enemy.AttackPhase == Forming
		? 1f - Mathf.Clamp(enemy.AttackTimer / Mathf.Max(0.01f, _type.FormSeconds), 0f, 1f)
		: 0f;

	/// <summary>Forming, it swells.</summary>
	protected override Basis Reshape(in EnemySwarm.Enemy enemy, Basis shape, float time)
	{
		float formed = Formed(enemy);
		return formed > 0f ? shape.Scaled(Vector3.One * (1f + _type.FormSwell * formed)) : shape;
	}

	public override void BeginDraw()
	{
		_spikeBatch ??= new InstanceBatch(Swarm, "spiker_spikes", Placeholders.Mesh("unit_box"), Placeholders.Material("goo"));
		_spikeBatch.Begin(_spikeCount);
		_spikesDrawn = 0;
	}

	/// <summary>Forming, it glows paler; thrusting, its spikes stand out from it.</summary>
	public override void Draw(int slot, in EnemySwarm.Enemy enemy, Color color)
	{
		float glow = Formed(enemy) * _type.FormGlow;
		base.Draw(slot, enemy, glow > 0f ? color.Lerp(Colors.White, glow) : color);
		if (enemy.AttackPhase != Thrusting || !_spikes.TryGetValue(enemy.Id, out List<Spike> spikes))
			return;
		foreach (Spike spike in spikes)
		{
			if (_spikesDrawn >= _spikeCount)
				break;
			Vector2 from = enemy.Position + spike.Direction * enemy.Radius * 0.5f;
			Vector2 tip = enemy.Position + spike.Direction * spike.Length;
			_spikeBatch.SetBeam(_spikesDrawn++, from, tip, 0f, _type.SpikeWidth * 2f, _type.SpikeWidth * 2f, color);
		}
	}

	public override void EndDraw() => _spikeBatch?.Commit(_spikesDrawn);
}
