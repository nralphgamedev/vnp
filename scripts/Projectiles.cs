using Godot;
using System;

/// <summary>Every enemy projectile in flight. Each flies straight, and hits the VNP if it passes close enough.</summary>
public partial class Projectiles : Node3D
{
	private struct Shot
	{
		public Vector2 Position;
		public Vector2 Velocity;
		public float Damage;
		public float Life; // Seconds left before it fizzles out.
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
	public void Fire(Vector2 from, Vector2 direction, float damage)
	{
		if (Count == _shots.Length)
			Array.Resize(ref _shots, _shots.Length * 2);

		_shots[Count++] = new Shot
		{
			Position = from,
			Velocity = direction * Tuning.EnemyProjectileSpeed,
			Damage = damage,
			Life = Tuning.EnemyProjectileLifetime,
		};
	}

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		float dt = (float)delta;
		Vnp vnp = Fight.Vnp;

		for (int i = Count - 1; i >= 0; i--)
		{
			ref Shot shot = ref _shots[i];
			Vector2 before = shot.Position;
			shot.Position += shot.Velocity * dt;
			shot.Life -= dt;

			// Test the whole path covered this frame, so a fast shot cannot skip past the VNP.
			bool hit = !vnp.IsDestroyed
				&& PlaneMath.DistanceToSegment(vnp.PlanePosition, before, shot.Position) <= Tuning.VnpHitRadius;
			if (hit)
				vnp.TakeDamage(shot.Damage);

			if (hit || shot.Life <= 0f)
				_shots[i] = _shots[--Count]; // Fill the gap with the last shot.
		}

		_batch.Begin(Count);
		for (int i = 0; i < Count; i++)
			_batch.Set(i, _shots[i].Position, 0f, PlaneMath.YawFacing(_shots[i].Velocity), 1f, Tuning.EnemyProjectileColor);
		_batch.Commit(Count);
	}
}
