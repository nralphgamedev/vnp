using Arbiter;
using Godot;
using System.Collections.Generic;

/// <summary>
/// The Quiller, a Grey Goo type. Everything shared by Grey Goo (GreyGoo.cs) applies, but it drains no drones and
/// doesn't grow. Its own part is its volley:
/// - drifting: an urchin-like blob drifting after the VNP;
/// - forming: once the VNP or a drone is within its volley range and its cooldown is done, it stops, swells and
///   glows paler, with each spike's target point set as it starts: shared out among the VNP, if in range, and the
///   nearest drones, each straying a little;
/// - firing: the spikes leave from every side as fragile shots (FragileShots.cs), curl in on their target points,
///   fly on a little past, turn back (or on a hit), and arc home, where it takes them back in. Each broken first
///   costs it health.
/// It forms its next volley only once all of the last are back or broken. While forming it declares each spike's
/// line to pilots, landing when the volley fires; the spikes in flight declare themselves.
/// Its numbers are in quiller.tres.
/// </summary>
public sealed class Quiller : GreyGoo
{
	private const int Drifting = 0, Forming = 1;

	private readonly QuillerDefinition _type;
	// Each forming Quiller's spikes, by its ID: the point each curls in on.
	private readonly Dictionary<int, List<Vector2>> _aims = new();
	private readonly int[] _drones = new int[64];

	public Quiller(EnemySwarm swarm, QuillerDefinition definition) : base(swarm, definition)
	{
		_type = definition;
	}

	public override Mesh Mesh => Placeholders.Mesh("quiller");

	public override void Spawned(ref EnemySwarm.Enemy enemy)
	{
		base.Spawned(ref enemy);
		enemy.AttackDamage = _type.SpikeDamage * Fight.GreyGooDamageScale;
		enemy.AttackPhase = Drifting;
		enemy.AttackTimer = _type.VolleyCooldown * Rng.Range(0.3f, 1f); // So a group doesn't fire at once.
	}

	/// <summary>It drifts straight after the VNP, and holds still while forming.</summary>
	public override Vector2 Chase(ref EnemySwarm.Enemy enemy, Vector2 track, float distance)
	{
		if (enemy.AttackPhase != Drifting)
			return Vector2.Zero;
		Vector2 toVnp = Fight.Vnp.PlanePosition - enemy.Position;
		return toVnp.LengthSquared() > 0.01f ? toVnp.Normalized() : track;
	}

	protected override bool ActOwn(ref EnemySwarm.Enemy enemy, int index, in EnemySwarm.Target target, float dt)
	{
		switch (enemy.AttackPhase)
		{
			case Drifting:
				// The cooldown counts once all of its last volley is back or broken.
				if (Fight.FragileShots.CountOwnedBy(enemy.Id) == 0)
					enemy.AttackTimer -= dt;
				if (enemy.AttackTimer <= 0f && enemy.Aggro && !Fight.Vnp.IsDestroyed)
					StartForming(ref enemy);
				break;
			case Forming:
				enemy.AttackTimer -= dt;
				if (enemy.AttackTimer <= 0f)
					Fire(ref enemy);
				break;
		}
		return false;
	}

	/// <summary>Picks its targets and sets each spike's aim, or does nothing if no target is in range.</summary>
	private void StartForming(ref EnemySwarm.Enemy enemy)
	{
		var targets = new List<Vector2>();
		Vnp vnp = Fight.Vnp;
		if (vnp.PlanePosition.DistanceTo(enemy.Position) <= _type.VolleyRange)
			targets.Add(vnp.PlanePosition);
		int found = Fight.Drones.CollectNear(enemy.Position, _type.VolleyRange, _drones);
		// The nearest drones, after the VNP.
		var drones = new List<(float Distance, Vector2 Position)>();
		for (int c = 0; c < found; c++)
		{
			Vector2 p = Fight.Drones.PositionOf(_drones[c]);
			drones.Add((p.DistanceTo(enemy.Position), p));
		}
		drones.Sort((a, b) => a.Distance.CompareTo(b.Distance));
		foreach ((float _, Vector2 p) in drones)
		{
			if (targets.Count >= Mathf.Max(1, _type.MaxTargets))
				break;
			targets.Add(p);
		}
		if (targets.Count == 0)
			return;

		if (!_aims.TryGetValue(enemy.Id, out List<Vector2> aims))
			_aims[enemy.Id] = aims = new List<Vector2>();
		aims.Clear();
		float spread = Mathf.DegToRad(_type.SpreadDegrees);
		for (int s = 0; s < _type.SpikeCount; s++)
		{
			// Where its target is now, strayed a little round the Quiller.
			Vector2 toTarget = targets[s % targets.Count] - enemy.Position;
			aims.Add(enemy.Position + toTarget.Rotated(Rng.Range(-spread, spread)));
		}
		enemy.AttackPhase = Forming;
		enemy.AttackTimer = _type.FormSeconds;
	}

	private void Fire(ref EnemySwarm.Enemy enemy)
	{
		if (_aims.TryGetValue(enemy.Id, out List<Vector2> aims))
		{
			// From every side, evenly round it from a random start, each curling in on its point.
			float start = Rng.Range(0f, Mathf.Tau);
			for (int s = 0; s < aims.Count; s++)
			{
				Vector2 out_ = Vector2.FromAngle(start + Mathf.Tau * s / aims.Count);
				Fight.FragileShots.FireAt(enemy.Id, enemy.Position + out_ * enemy.Radius, out_, aims[s], _type.SpikeSpeed,
					_type.OutTurnDegrees, _type.SpikeBeyond, _type.ReturnSpeed, _type.ReturnTurnDegrees, enemy.AttackDamage,
					_type.SpikeRadius, _type.SpikeLossHealth, Source, _type.Color);
			}
			aims.Clear();
		}
		Fight.Sounds.Play(Sound.QuillerVolley);
		enemy.AttackPhase = Drifting;
		enemy.AttackTimer = _type.VolleyCooldown * Rng.Range(0.85f, 1.15f);
	}

	/// <summary>
	/// While forming, each spike's target point: a small circle there, landing when its spike should arrive and lasting
	/// while it passes on through. Spikes curve, so where they will be on the way isn't a line; once they fly, each
	/// declares itself (FragileShots.DeclareThreats).
	/// </summary>
	public override void DeclareThreats(in EnemySwarm.Enemy enemy, Situation situation)
	{
		base.DeclareThreats(enemy, situation);
		if (enemy.AttackPhase != Forming || !_aims.TryGetValue(enemy.Id, out List<Vector2> aims))
			return;
		float speed = Mathf.Max(0.01f, _type.SpikeSpeed);
		// The longest way round: a spike leaving straight away from its point turns half a circle first.
		float turnAround = Mathf.Pi * speed / Mathf.Max(0.01f, Mathf.DegToRad(_type.OutTurnDegrees));
		foreach (Vector2 point in aims)
		{
			float arrives = Mathf.Max(0f, enemy.AttackTimer) + (point.DistanceTo(enemy.Position) + turnAround * 0.5f) / speed;
			situation.Add(Hazard.Timed(Area.Circle(point.ToArbiter(), _type.SpikeRadius + 1f), Mathf.Max(0f, arrives - 0.3f),
				0.6f + _type.SpikeBeyond / speed, enemy.AttackDamage, Endangers.Both, enemy.Id));
		}
	}

	protected override ulong OwnTags => ThreatTags.Shoots;

	public override void Died(in EnemySwarm.Enemy enemy)
	{
		_aims.Remove(enemy.Id);
		base.Died(enemy);
	}

	/// <summary>How far it has formed its volley: 0 not forming, 1 about to fire.</summary>
	private float Formed(in EnemySwarm.Enemy enemy) => enemy.AttackPhase == Forming
		? 1f - Mathf.Clamp(enemy.AttackTimer / Mathf.Max(0.01f, _type.FormSeconds), 0f, 1f)
		: 0f;

	/// <summary>Forming, it swells.</summary>
	protected override Basis Reshape(in EnemySwarm.Enemy enemy, Basis shape, float time)
	{
		float formed = Formed(enemy);
		return formed > 0f ? shape.Scaled(Vector3.One * (1f + _type.FormSwell * formed)) : shape;
	}

	/// <summary>Forming, it glows paler, so the warning shows.</summary>
	public override void Draw(int slot, in EnemySwarm.Enemy enemy, Color color)
	{
		float glow = Formed(enemy) * _type.FormGlow;
		base.Draw(slot, enemy, glow > 0f ? color.Lerp(Colors.White, glow) : color);
	}
}
