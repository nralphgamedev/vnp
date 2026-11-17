using Godot;
using System;

/// <summary>
/// Every projectile in flight, from both sides. Each flies straight until it hits something or
/// fizzles out. Enemy shots hit the VNP or a probe. Probe shots hit enemies.
/// </summary>
public partial class Projectiles : Node3D
{
	private struct Shot
	{
		public Vector2 Position;
		public Vector2 Velocity;
		public float Damage;
		public float Life;      // Seconds left before it fizzles out.
		public bool FromEnemy;
	}

	public Fight Fight;

	/// <summary>Projectiles currently in flight.</summary>
	public int Count { get; private set; }

	private Shot[] _shots = new Shot[256];
	private InstanceBatch _batch;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Projectiles;
		_batch = new InstanceBatch(this, "ProjectileShapes",
			new BoxMesh { Size = new Vector3(0.4f, 0.4f, 1.3f) },
			Placeholder.InstanceMaterial(unshaded: true));
	}

	/// <summary>Launches a projectile from a point in a direction.</summary>
	public void Fire(Vector2 from, Vector2 direction, float damage, float speed, float lifetime, bool fromEnemy)
	{
		if (Count == _shots.Length)
			Array.Resize(ref _shots, _shots.Length * 2);

		_shots[Count++] = new Shot
		{
			Position = from,
			Velocity = direction * speed,
			Damage = damage,
			Life = lifetime,
			FromEnemy = fromEnemy,
		};
	}

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		float dt = (float)delta;
		for (int i = Count - 1; i >= 0; i--)
		{
			ref Shot shot = ref _shots[i];
			Vector2 before = shot.Position;
			shot.Position += shot.Velocity * dt;
			shot.Life -= dt;

			// Test the whole path covered this frame, so a fast shot cannot skip past its target.
			bool hit = shot.FromEnemy
				? HitPlayerSide(before, shot.Position, shot.Damage)
				: HitEnemy(before, shot.Position, shot.Damage);

			if (hit || shot.Life <= 0f)
				_shots[i] = _shots[--Count]; // Fill the gap with the last shot.
		}

		_batch.Begin(Count);
		for (int i = 0; i < Count; i++)
		{
			ref Shot shot = ref _shots[i];
			// Probe shots are smaller and a different colour, so the two sides are easy to tell apart.
			_batch.Set(i, shot.Position, 0f, PlaneMath.YawFacing(shot.Velocity),
				shot.FromEnemy ? 1f : 0.6f,
				shot.FromEnemy ? Tuning.EnemyProjectileColor : Tuning.ProbeProjectileColor);
		}
		_batch.Commit(Count);
	}

	/// <summary>An enemy shot hits the VNP or the first probe on its path. Probes can shield the VNP this way.</summary>
	private bool HitPlayerSide(Vector2 from, Vector2 to, float damage)
	{
		Vnp vnp = Fight.Vnp;
		if (!vnp.IsDestroyed && PlaneMath.DistanceToSegment(vnp.PlanePosition, from, to) <= Tuning.VnpHitRadius)
		{
			vnp.TakeDamage(damage);
			return true;
		}

		int probe = Fight.Probes.FindHit(from, to);
		if (probe >= 0)
		{
			Fight.Probes.Damage(probe, damage);
			return true;
		}
		return false;
	}

	private bool HitEnemy(Vector2 from, Vector2 to, float damage)
	{
		int enemy = Fight.Enemies.FindHit(from, to);
		if (enemy < 0)
			return false;

		Fight.Enemies.Damage(enemy, damage);
		return true;
	}
}
