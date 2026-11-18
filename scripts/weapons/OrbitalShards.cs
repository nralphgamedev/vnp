using Godot;
using System.Collections.Generic;

/// <summary>
/// A VNP Weapon for the second slot: shards that circle the VNP and damage enemies they touch.
/// Each shard can hit a given enemy once per cooldown, which in practice is once each time it
/// passes. So faster shards hit more often.
/// </summary>
public partial class OrbitalShards : VnpWeapon
{
	private const float ShardHeight = 0.4f;

	private OrbitalShardsDefinition Shards => (OrbitalShardsDefinition)Definition;

	private float _angle;
	private double _nextCleanup;

	// For each pair of shard and enemy, the time that shard can hit that enemy again.
	private readonly Dictionary<(int Shard, int Enemy), double> _nextHitAt = new();
	private readonly int[] _nearby = new int[64];
	private InstanceBatch _batch;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Weapons;
		_batch = new InstanceBatch(this, "ShardShapes", Placeholders.Mesh("orbital_shard"),
			Placeholders.Material("instanced_unshaded"), 8);
	}

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		_angle += Mathf.DegToRad(Shards.OrbitSpeed * Speed) * (float)delta;

		int count = Shards.Count;
		float orbit = Shards.OrbitRadius * Area;
		float reach = Shards.HitRadius * Area + Tuning.EnemySpawning.Enemy.HitRadius;
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
			_batch.Set(s, position, ShardHeight, yaw, Shards.Size * Area, Shards.Color);
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
