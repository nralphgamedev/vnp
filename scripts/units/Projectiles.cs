using Godot;
using System;

/// <summary>
/// Every projectile in flight, from both sides. Each flies straight until it hits something or
/// fizzles out. Enemy shots hit the VNP or a drone. Drone shots hit enemies.
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
		public DamageSource Source; // What fired it, for fight statistics.
		public Color Color;
		public float Size;
	}

	public Fight Fight;

	/// <summary>Projectiles currently in flight.</summary>
	public int Count { get; private set; }

	private Shot[] _shots = new Shot[256];

#if RECORDING
	/// <summary>Where a shot in flight is, for testing tools such as the run review's maps.</summary>
	public Vector2 PositionOf(int index) => _shots[index].Position;

	/// <summary>A shot's velocity on the plane.</summary>
	public Vector2 VelocityOf(int index) => _shots[index].Velocity;

	/// <summary>Whether a shot was fired by an enemy.</summary>
	public bool IsEnemyShot(int index) => _shots[index].FromEnemy;

	/// <summary>What fired a shot: a VNP Weapon, the Drone Weapon in use, or an enemy type. Read-only.</summary>
	public DamageSource SourceOf(int index) => _shots[index].Source;
#endif
	private InstanceBatch _batch;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Projectiles;
		_batch = new InstanceBatch(this, "ProjectileShapes", Placeholders.Mesh("projectile"),
			Placeholders.Material("instanced_unshaded"));
	}

	/// <summary>Launches a projectile from a point in a direction. <paramref name="size"/> scales the shot;
	/// 0 uses the usual size for its side. <paramref name="source"/> is what fired it, for fight statistics.</summary>
	public void Fire(Vector2 from, Vector2 direction, float damage, float speed, float lifetime, bool fromEnemy, Color color,
		DamageSource source, float size = 0f)
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
			Source = source,
			Color = color,
			Size = size > 0f ? size : fromEnemy ? 1f : 0.6f,
		};
	}

	/// <summary>
	/// Fills <paramref name="results"/> with the position and velocity of every enemy shot within a distance of
	/// a point, and returns how many. For pilots, to notice shots coming at them.
	/// </summary>
	public int CollectEnemyShots(Vector2 center, float radius, System.Collections.Generic.List<(Vector2 Position, Vector2 Velocity)> results)
	{
		results.Clear();
		float radiusSquared = radius * radius;
		for (int i = 0; i < Count; i++)
		{
			ref Shot shot = ref _shots[i];
			if (shot.FromEnemy && shot.Position.DistanceSquaredTo(center) <= radiusSquared)
				results.Add((shot.Position, shot.Velocity));
		}
		return results.Count;
	}

	/// <summary>
	/// Destroys every enemy shot within <paramref name="radius"/> of a point, each with a small pop.
	/// Returns how many it destroyed. Used by Orbital Shards blocking.
	/// </summary>
	public int BlockEnemyShots(Vector2 at, float radius)
	{
		int blocked = 0;
		float radiusSquared = radius * radius;
		for (int i = Count - 1; i >= 0; i--)
		{
			ref Shot shot = ref _shots[i];
			if (!shot.FromEnemy || shot.Position.DistanceSquaredTo(at) > radiusSquared)
				continue;
			Fight.Effects.Impact(shot.Position, shot.Color, 0.5f);
			_shots[i] = _shots[--Count];
			blocked++;
		}
		return blocked;
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
				? HitPlayerSide(before, shot.Position, shot.Damage, shot.Source)
				: HitEnemy(before, shot.Position, shot.Damage, shot.Source);
			if (hit)
				Fight.Effects.Impact(shot.Position, shot.Color, 0.6f);

			if (hit || shot.Life <= 0f)
				_shots[i] = _shots[--Count]; // Fill the gap with the last shot.
		}

		_batch.Begin(Count);
		for (int i = 0; i < Count; i++)
		{
			ref Shot shot = ref _shots[i];
			// Drone shots are smaller and a different colour, so the two sides are easy to tell apart.
			_batch.Set(i, shot.Position, 0f, PlaneMath.YawFacing(shot.Velocity), shot.Size, shot.Color);
		}
		_batch.Commit(Count);
	}

	/// <summary>An enemy shot hits the VNP or the first drone on its path. Drones can shield the VNP this way.</summary>
	private bool HitPlayerSide(Vector2 from, Vector2 to, float damage, DamageSource source)
	{
		Vnp vnp = Fight.Vnp;
		if (!vnp.IsDestroyed && PlaneMath.DistanceToSegment(vnp.PlanePosition, from, to) <= Tuning.Vnp.HitRadius)
		{
			vnp.TakeDamage(damage, source);
			return true;
		}

		int drone = Fight.Drones.FindHit(from, to);
		if (drone >= 0)
		{
			Fight.Drones.Damage(drone, damage, source);
			return true;
		}
		return false;
	}

	private bool HitEnemy(Vector2 from, Vector2 to, float damage, DamageSource source)
	{
		int enemy = Fight.Enemies.FindHit(from, to);
		if (enemy < 0)
			return false;

		Fight.Enemies.Damage(enemy, damage, source);
		return true;
	}
}
