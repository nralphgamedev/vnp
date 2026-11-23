using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Enemy shots that the player's weapons can destroy in flight, such as a Quiller's spikes: unlike ordinary shots
/// (Projectiles.cs), any damage that touches one breaks it, though no weapon aims at them.
///
/// Each flies out, either along its line for its reach, or (with a target point) steering toward that point at its
/// own turn rate and on past it by a set distance, such as a Quiller's spikes, which leave from every side and curl
/// in on their targets. It turns back as soon as it hits the VNP or a drone, or once its flight out is done. Then it arcs back to the enemy that fired it, turning at a limited rate, and is taken back in when it
/// arrives; on the way back it can hit again. One that is broken before it gets back costs the enemy that fired
/// it some health, credited to whatever broke it. One whose enemy is gone fizzles out.
///
/// What breaks them is each weapon's to say, through DestroyWithin, DestroyInBand and DestroyAlong: Orbital Shards,
/// Shockwave rings, mine blasts, ramming drones, and the VNP's and drones' shots and missiles. Beams only hit what
/// they aim at, so they don't.
///
/// For a crowd: a spatial grid finds the ones near a weapon, the enemies' positions are noted once a frame for the
/// spikes to find their way home, and each enemy's count of shots still out is kept as they come and go. A broken
/// one is only marked, and cleared away at the start of the next update, so the grid stays right within a frame.
/// </summary>
public partial class FragileShots : Node3D
{
	private struct Shot
	{
		public int Owner;               // The enemy that fired it, by ID: it returns there.
		public Vector2 Position;
		public Vector2 Velocity;
		public bool Returning;
		public bool Gone;               // Broken, taken back or fizzled: cleared away at the next update.
		public float OutLeft;           // Seconds left flying out (once past its target point, if it has one).
		public bool Steering;           // Still steering toward its target point.
		public Vector2 TargetPoint;
		public float OutTurnRate;       // Radians a second it can turn while steering toward its target point.
		public float Beyond;            // How far it flies on past its target point.
		public float Life;              // Seconds left before it fizzles out whatever happens.
		public float Damage;
		public float Radius;
		public float ReturnSpeed;
		public float TurnRate;          // Radians a second it can turn while arcing back.
		public float LossHealth;        // Health its enemy loses if it is broken.
		public int LastHit;             // What it last hit (0 none, -1 the VNP, else a drone's ID), so it hits each once in passing.
		public DamageSource Source;
		public Color Color;
	}

	// The widest a shot is, plus how far one moves in a frame, for searching the grid.
	private const float SearchSlack = 2f;

	public Fight Fight;

	private Shot[] _shots = new Shot[64];
	public int Count { get; private set; }

	private readonly SpatialGrid _grid = new(6f);
	private int[] _nearby = new int[256];
	private readonly Dictionary<int, int> _outByOwner = new();
	private readonly Dictionary<int, Vector2> _homes = new();
	private InstanceBatch _batch;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Projectiles;
		_batch = new InstanceBatch(this, "FragileShotShapes", Placeholders.Mesh("quill"), Placeholders.Material("goo"));
	}

	/// <summary>
	/// Fires one from an enemy, out along <paramref name="direction"/> at <paramref name="speed"/> for
	/// <paramref name="reach"/>, then back to that enemy at <paramref name="returnSpeed"/>, turning at up to
	/// <paramref name="turnDegrees"/> a second. If broken first, the enemy loses <paramref name="lossHealth"/>.
	/// </summary>
	public void Fire(int owner, Vector2 from, Vector2 direction, float speed, float reach, float returnSpeed,
		float turnDegrees, float damage, float radius, float lossHealth, DamageSource source, Color color)
	{
		if (Count == _shots.Length)
			Array.Resize(ref _shots, _shots.Length * 2);
		float outSeconds = reach / Mathf.Max(0.01f, speed);
		_shots[Count++] = new Shot
		{
			Owner = owner,
			Position = from,
			Velocity = direction * speed,
			OutLeft = outSeconds,
			Life = outSeconds + 6f,
			Damage = damage,
			Radius = radius,
			ReturnSpeed = returnSpeed,
			TurnRate = Mathf.DegToRad(turnDegrees),
			LossHealth = lossHealth,
			Source = source,
			Color = color,
		};
		_outByOwner.TryGetValue(owner, out int out_);
		_outByOwner[owner] = out_ + 1;
	}

	/// <summary>
	/// Fires one from an enemy that steers, from <paramref name="direction"/>, toward <paramref name="target"/> at up
	/// to <paramref name="outTurnDegrees"/> a second, flies <paramref name="beyond"/> past it, then comes back as
	/// Fire's do. It gives up steering and turns back after a few seconds if it can't reach the point.
	/// </summary>
	public void FireAt(int owner, Vector2 from, Vector2 direction, Vector2 target, float speed, float outTurnDegrees,
		float beyond, float returnSpeed, float turnDegrees, float damage, float radius, float lossHealth,
		DamageSource source, Color color)
	{
		Fire(owner, from, direction, speed, 3f * speed, returnSpeed, turnDegrees, damage, radius, lossHealth, source, color);
		ref Shot shot = ref _shots[Count - 1];
		shot.Steering = true;
		shot.TargetPoint = target;
		shot.OutTurnRate = Mathf.DegToRad(outTurnDegrees);
		shot.Beyond = beyond;
	}

	/// <summary>How many of an enemy's shots are still out.</summary>
	public int CountOwnedBy(int owner) => _outByOwner.TryGetValue(owner, out int count) ? count : 0;

	/// <summary>Marks one gone, to be cleared away at the next update.</summary>
	private void Finish(ref Shot shot)
	{
		if (shot.Gone)
			return;
		shot.Gone = true;
		if (_outByOwner.TryGetValue(shot.Owner, out int count))
		{
			if (count <= 1)
				_outByOwner.Remove(shot.Owner);
			else
				_outByOwner[shot.Owner] = count - 1;
		}
	}

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		// Clear away last frame's gone ones.
		for (int i = Count - 1; i >= 0; i--)
		{
			if (_shots[i].Gone)
				_shots[i] = _shots[--Count];
		}

		// Where each enemy is, once, for the shots arcing home.
		_homes.Clear();
		if (Count > 0)
		{
			EnemySwarm enemies = Fight.Enemies;
			for (int e = 0; e < enemies.Count; e++)
			{
				if (enemies.IsAlive(e))
					_homes[enemies.IdOf(e)] = enemies.PositionOf(e);
			}
		}

		float dt = (float)delta;
		Vnp vnp = Fight.Vnp;
		for (int i = 0; i < Count; i++)
		{
			ref Shot shot = ref _shots[i];
			shot.Life -= dt;
			bool ownerAlive = _homes.TryGetValue(shot.Owner, out Vector2 home);
			if (shot.Life <= 0f || (shot.Returning && !ownerAlive))
			{
				Fight.Effects.Fizzle(shot.Position, shot.Color);
				Finish(ref shot);
				continue;
			}

			if (!shot.Returning)
			{
				if (shot.Steering)
				{
					// Curl in on the target point; once there, fly on past it by Beyond, then turn back.
					Vector2 toTarget = shot.TargetPoint - shot.Position;
					float speed = shot.Velocity.Length();
					if (toTarget.Length() <= speed * dt + 0.5f)
					{
						shot.Steering = false;
						shot.OutLeft = shot.Beyond / Mathf.Max(0.01f, speed);
					}
					else
					{
						float heading = Mathf.RotateToward(shot.Velocity.Angle(), toTarget.Angle(), shot.OutTurnRate * dt);
						shot.Velocity = Vector2.FromAngle(heading) * speed;
					}
				}
				shot.OutLeft -= dt;
				if (shot.OutLeft <= 0f)
					shot.Returning = true;
			}
			else
			{
				// Arc back: turn toward the enemy at the turn rate, at the return speed.
				Vector2 toHome = home - shot.Position;
				float heading = Mathf.RotateToward(shot.Velocity.Angle(), toHome.Angle(), shot.TurnRate * dt);
				shot.Velocity = Vector2.FromAngle(heading) * shot.ReturnSpeed;
				if (toHome.Length() <= shot.ReturnSpeed * dt + 1f)
				{
					Finish(ref shot); // Taken back in.
					continue;
				}
			}

			Vector2 before = shot.Position;
			shot.Position += shot.Velocity * dt;

			// Hits on the VNP or a drone, each once in passing. Going out, a hit turns it back.
			if (!vnp.IsDestroyed && shot.LastHit != -1
				&& PlaneMath.DistanceToSegment(vnp.PlanePosition, before, shot.Position) <= shot.Radius + Tuning.Vnp.HitRadius)
			{
				vnp.TakeDamage(shot.Damage, shot.Source);
				Fight.Effects.Impact(shot.Position, shot.Color, 0.6f);
				shot.LastHit = -1;
				shot.Returning = true;
				continue;
			}
			int drone = Fight.Drones.FindHit(before, shot.Position);
			if (drone >= 0 && Fight.Drones.IdOf(drone) != shot.LastHit)
			{
				shot.LastHit = Fight.Drones.IdOf(drone);
				Fight.Drones.Damage(drone, shot.Damage, shot.Source);
				Fight.Effects.Impact(shot.Position, shot.Color, 0.5f);
				shot.Returning = true;
			}
		}

		_grid.Clear();
		int drawn = 0;
		for (int i = 0; i < Count; i++)
		{
			if (!_shots[i].Gone)
			{
				_grid.Insert(i, _shots[i].Position);
				drawn++;
			}
		}
		if (_nearby.Length < Count)
			_nearby = new int[Count * 2];

		_batch.Begin(drawn);
		int slot = 0;
		for (int i = 0; i < Count; i++)
		{
			ref Shot shot = ref _shots[i];
			if (!shot.Gone)
				_batch.Set(slot++, shot.Position, 0f, PlaneMath.YawFacing(shot.Velocity), shot.Radius * 2.5f, shot.Color);
		}
		_batch.Commit(drawn);
	}

	/// <summary>The ones the grid files near a circle, living ones only, into _nearby; returns how many.</summary>
	private int Near(Vector2 center, float radius)
	{
		if (Count == 0)
			return 0;
		int found = _grid.Query(center, radius + SearchSlack, _nearby);
		int kept = 0;
		for (int c = 0; c < found; c++)
		{
			if (_nearby[c] < Count && !_shots[_nearby[c]].Gone)
				_nearby[kept++] = _nearby[c];
		}
		return kept;
	}

	/// <summary>Breaks one: a pop, and its enemy loses health, credited to what broke it.</summary>
	private void Break(int index, DamageSource by)
	{
		ref Shot shot = ref _shots[index];
		Fight.Effects.Impact(shot.Position, shot.Color, 0.5f);
		Fight.Sounds.Play(Sound.ShardBlock);
		int owner = Fight.Enemies.IndexOfId(shot.Owner);
		if (owner >= 0 && shot.LossHealth > 0f)
			Fight.Enemies.Damage(owner, shot.LossHealth, by);
		Finish(ref shot);
	}

	/// <summary>Breaks every one within <paramref name="radius"/> of a point (allowing for its own size), such as
	/// by a shard or a blast. Returns how many.</summary>
	public int DestroyWithin(Vector2 at, float radius, DamageSource by)
	{
		int found = Near(at, radius);
		int broken = 0;
		for (int c = 0; c < found; c++)
		{
			int i = _nearby[c];
			if (_shots[i].Position.DistanceTo(at) <= radius + _shots[i].Radius)
			{
				Break(i, by);
				broken++;
			}
		}
		return broken;
	}

	/// <summary>Breaks every one between two distances from a point, such as by a ring passing over it this frame.</summary>
	public int DestroyInBand(Vector2 centre, float inner, float outer, DamageSource by)
	{
		int found = Near(centre, outer);
		int broken = 0;
		for (int c = 0; c < found; c++)
		{
			int i = _nearby[c];
			float distance = _shots[i].Position.DistanceTo(centre);
			if (distance + _shots[i].Radius >= inner && distance - _shots[i].Radius <= outer)
			{
				Break(i, by);
				broken++;
			}
		}
		return broken;
	}

	/// <summary>Breaks the first one a shot travelling between two points meets, and says whether there was one,
	/// so the shot can be used up.</summary>
	public bool DestroyAlong(Vector2 from, Vector2 to, float radius, DamageSource by)
	{
		int found = Near((from + to) * 0.5f, from.DistanceTo(to) * 0.5f + radius);
		int nearest = -1;
		float nearestDistance = float.MaxValue;
		for (int c = 0; c < found; c++)
		{
			int i = _nearby[c];
			if (PlaneMath.DistanceToSegment(_shots[i].Position, from, to) > radius + _shots[i].Radius)
				continue;
			float distance = from.DistanceSquaredTo(_shots[i].Position);
			if (distance < nearestDistance)
			{
				nearestDistance = distance;
				nearest = i;
			}
		}
		if (nearest < 0)
			return false;
		Break(nearest, by);
		return true;
	}

	/// <summary>Declares every one within <paramref name="radius"/> of a point as a hazard, for pilots and the run
	/// review: a moving point the size of the shot, harmful to the VNP or a drone, until it would be back.
	/// Returning ones are judged going straight, which their arc soon bends. Read-only.</summary>
	public void DeclareThreats(Arbiter.Situation situation, Vector2 center, float radius, Func<Vector2, bool> visible = null)
	{
		int found = Near(center, radius);
		float radiusSquared = radius * radius;
		for (int c = 0; c < found; c++)
		{
			ref Shot shot = ref _shots[_nearby[c]];
			if (shot.Position.DistanceSquaredTo(center) > radiusSquared || (visible != null && !visible(shot.Position)))
				continue;
			float lasts = shot.Returning
				? (_homes.TryGetValue(shot.Owner, out Vector2 home) ? home.DistanceTo(shot.Position) / Mathf.Max(0.01f, shot.ReturnSpeed) : 0f)
				: shot.OutLeft;
			var path = Arbiter.Area.Circle(shot.Position.ToArbiter(), shot.Radius, shot.Velocity.ToArbiter());
			situation.Add(Arbiter.Hazard.Timed(path, 0f, Mathf.Max(0f, lasts), shot.Damage, Arbiter.Endangers.Both, shot.Owner));
		}
	}

#if RECORDING
	/// <summary>Whether one is still flying, and where it is and how it moves, for the run review's recordings.</summary>
	public bool IsFlying(int index) => !_shots[index].Gone;
	public Vector2 PositionOf(int index) => _shots[index].Position;
	public Vector2 VelocityOf(int index) => _shots[index].Velocity;
#endif
}
