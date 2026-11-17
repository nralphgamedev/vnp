using Godot;
using System.Collections.Generic;

/// <summary>
/// A VNP Weapon for the second slot: shards that circle the VNP and damage enemies they touch.
/// Each shard can hit a given enemy once per cooldown, which in practice is once each time it
/// passes. So faster shards hit more often.
/// </summary>
public partial class OrbitalShards : Node3D
{
	// Placeholder silhouette: a long thin diamond. Forward is -y.
	private static readonly Vector2[] Outline =
	{
		new(0f, -1f), new(0.32f, 0f), new(0f, 1f), new(-0.32f, 0f),
	};

	private const float ShardHeight = 0.4f;

	public Fight Fight;

	// These belong to the weapon, so upgrades can change them.
	public float Damage = Tuning.ShardDamage;
	public float Cooldown = Tuning.ShardCooldown;

	/// <summary>Area of effect: scales both the orbit and the size of each shard.</summary>
	public float Area = 1f;

	/// <summary>How fast the shards circle, in degrees per second.</summary>
	public float Speed = Tuning.ShardOrbitSpeed;

	private float _angle;
	private double _nextCleanup;

	// For each pair of shard and enemy, the time that shard can hit that enemy again.
	private readonly Dictionary<(int Shard, int Enemy), double> _nextHitAt = new();
	private readonly int[] _nearby = new int[64];
	private InstanceBatch _batch;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Weapons;
		_batch = new InstanceBatch(this, "ShardShapes", Placeholder.ExtrudedShape(Outline, 0.4f),
			Placeholder.InstanceMaterial(unshaded: true), 8);
	}

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		_angle += Mathf.DegToRad(Speed) * (float)delta;

		int count = Tuning.ShardCount;
		float orbit = Tuning.ShardOrbitRadius * Area;
		float reach = Tuning.ShardHitRadius * Area + Tuning.EnemyHitRadius;
		double now = Fight.Elapsed;
		Vector2 center = Fight.Vnp.PlanePosition;
		EnemySwarm enemies = Fight.Enemies;

		_batch.Begin(count);
		for (int s = 0; s < count; s++)
		{
			float angle = _angle + s * Mathf.Tau / count;
			Vector2 around = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			Vector2 position = center + around * orbit;

			if (!Fight.Vnp.IsDestroyed)
			{
				int found = enemies.CollectNear(position, reach, _nearby);
				for (int c = 0; c < found; c++)
				{
					int enemy = _nearby[c];
					var key = (s, enemies.IdOf(enemy));
					if (_nextHitAt.TryGetValue(key, out double readyAt) && readyAt > now)
						continue;
					_nextHitAt[key] = now + Cooldown;
					enemies.Damage(enemy, Damage);
				}
			}

			// Point each shard along its path.
			float yaw = PlaneMath.YawFacing(new Vector2(-around.Y, around.X));
			_batch.Set(s, position, ShardHeight, yaw, Tuning.ShardSize * Area, Tuning.ShardColor);
		}
		_batch.Commit(count);

		// Forget enemies whose cooldown has long passed, so the list does not grow all fight.
		if (now >= _nextCleanup)
		{
			_nextCleanup = now + 5.0;
			var expired = new List<(int, int)>();
			foreach (var entry in _nextHitAt)
			{
				if (entry.Value < now)
					expired.Add(entry.Key);
			}
			foreach (var key in expired)
				_nextHitAt.Remove(key);
		}
	}
}
