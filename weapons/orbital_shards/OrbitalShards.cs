using Godot;
using System.Collections.Generic;

/// <summary>
/// A VNP Weapon for the second slot: shards that circle the VNP and damage enemies they touch.
/// Each shard can hit a given enemy once per cooldown, which in practice is once each time it
/// passes. So faster shards hit more often. With the harvesting upgrade, they also harvest the
/// asteroids and debris they pass through, the same way.
/// </summary>
public partial class OrbitalShards : VnpWeapon
{
	private const float ShardHeight = 0.4f;

	private OrbitalShardsDefinition Shards => (OrbitalShardsDefinition)Definition;

	public override float Reach => (Shards.OrbitRadius + Shards.HitRadius) * Area;

	/// <summary>The shards' colour: tinted once they can block enemy shots.</summary>
	private Color ShardColor => HasFeature(WeaponFeature.Blocking) ? Shards.BlockingColor : Shards.Color;

	/// <summary>Damage compared with the start of the fight, so damage upgrades show in the trails and hits.</summary>
	private float Power => Mathf.Max(0.01f, Damage / Shards.Damage);

	private float _angle;
	private double _nextCleanup;

	// For each pair of shard and enemy, the time that shard can hit that enemy again.
	private readonly Dictionary<(int Shard, int Enemy), double> _nextHitAt = new();
	private readonly int[] _nearby = new int[64];

	// The same for each pair of shard and asteroid or piece of debris, with the harvesting upgrade.
	private readonly Dictionary<(int Shard, int Material), double> _nextHarvestAt = new();
	private readonly int[] _material = new int[64];

#if RECORDING
	// Where each shard was this frame, for CollectPlaced.
	private readonly List<Vector2> _shardPositions = new();

	public override void CollectPlaced(List<PlacedObject> placed)
	{
		foreach (Vector2 position in _shardPositions)
			placed.Add(PlacedObject.Point(position, Shards.HitRadius * Area));
	}
#endif
	private InstanceBatch _batch;
	private InstanceBatch _trailBatch;

	// Pieces each shard's trail is drawn with. Close together, so the trail reads as one streak.
	private const int TrailPieces = 16;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Weapons;
		_batch = new InstanceBatch(this, "ShardShapes", Placeholders.Mesh("orbital_shard"),
			Placeholders.Material("instanced_unshaded"), 8);
		_trailBatch = new InstanceBatch(this, "ShardTrails", Placeholders.Mesh("unit_box"),
			Placeholders.Material("instanced_glow"), 8 * TrailPieces);
	}

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		_angle += Mathf.DegToRad(Shards.OrbitSpeed * Speed) * (float)delta;

		int count = Shards.Count + ExtraCount;
		float orbit = Shards.OrbitRadius * Area;
		float reach = Shards.HitRadius * Area;
		double now = Fight.Elapsed;
		Vector2 center = Fight.Vnp.PlanePosition;
		EnemySwarm enemies = Fight.Enemies;

		bool trails = Tuning.Feedback.ShardTrails && !Fight.Vnp.IsDestroyed;
#if RECORDING
		_shardPositions.Clear();
#endif
		_batch.Begin(count);
		_trailBatch.Begin(trails ? count * TrailPieces : 0);
		for (int s = 0; s < count; s++)
		{
			float angle = _angle + s * Mathf.Tau / count;
			Vector2 around = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			Vector2 position = center + around * orbit;
#if RECORDING
			_shardPositions.Add(position);
#endif

			if (!Fight.Vnp.IsDestroyed)
			{
				int found = enemies.CollectTouching(position, reach, _nearby);
				for (int c = 0; c < found; c++)
				{
					int enemy = _nearby[c];
					var key = (s, enemies.IdOf(enemy));
					if (_nextHitAt.TryGetValue(key, out double readyAt) && readyAt > now)
						continue;
					_nextHitAt[key] = now + Cooldown;
					Fight.Effects.Impact(enemies.PositionOf(enemy), ShardColor, 0.7f * Mathf.Sqrt(Power));
					Fight.Sounds.Play(Sound.ShardHit);
					enemies.Damage(enemy, Damage, Source);
				}
			}

			// With the harvesting upgrade, a shard also harvests what it passes through: its damage, by the
			// conversion rate, as mass drawn out, credited like any beam's harvest.
			if (HasFeature(WeaponFeature.Harvesting) && !Fight.Vnp.IsDestroyed)
				HarvestAt(s, position, reach, now);

			// With the blocking upgrade, a shard also destroys enemy shots it touches. A shot moves about
			// half a unit a frame, so the reach allows a little for that.
			if (HasFeature(WeaponFeature.Blocking) && !Fight.Vnp.IsDestroyed)
			{
				int blocked = Fight.Projectiles.BlockEnemyShots(position, Shards.HitRadius * Area + 0.6f);
				if (blocked > 0)
				{
					Fight.Stats.ShotsBlocked += blocked;
					Fight.Sounds.Play(Sound.ShardBlock);
				}
			}

			// Point each shard along its path.
			float yaw = PlaneMath.YawFacing(new Vector2(-around.Y, around.X));
			_batch.Set(s, position, ShardHeight, yaw, Shards.Size * Area, ShardColor);

			if (trails)
				DrawTrail(s, center, angle, orbit);
		}
		_batch.Commit(count);
		_trailBatch.Commit(trails ? count * TrailPieces : 0);

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
			var spent = new List<(int, int)>();
			foreach (var entry in _nextHarvestAt)
			{
				if (entry.Value < now)
					spent.Add(entry.Key);
			}
			foreach (var key in spent)
				_nextHarvestAt.Remove(key);
		}
	}

	/// <summary>
	/// One shard harvesting the asteroids and debris it touches, each once per cooldown, as it hits enemies.
	/// Each harvest shows as a small burst in the fight experience colour where the shard passes, and the
	/// material glows as any harvested material does.
	/// </summary>
	private void HarvestAt(int shard, Vector2 position, float reach, double now)
	{
		Harvestables harvestables = Fight.Harvestables;
		int found = harvestables.CollectNear(position, reach, _material);
		for (int c = 0; c < found; c++)
		{
			int item = _material[c];
			var key = (shard, harvestables.IdOf(item));
			if (_nextHarvestAt.TryGetValue(key, out double readyAt) && readyAt > now)
				continue;
			_nextHarvestAt[key] = now + Cooldown;
			if (harvestables.Harvest(item, Damage * Shards.HarvestPerDamage, byVnp: true) > 0f)
				Fight.Effects.Impact(position, Tuning.Look.ExperienceColor, 0.35f);
		}
	}

	/// <summary>
	/// The trail behind one shard: where the shard was a moment ago on its orbit, drawn around where the
	/// VNP is now. So it stays fixed to the shard however the VNP moves, and shows only the circling.
	/// It covers ShardTrailTime seconds of travel, lengthened by damage upgrades up to three times, and
	/// thins and fades toward its end.
	/// </summary>
	private void DrawTrail(int shard, Vector2 center, float angle, float orbit)
	{
		float trailTime = Tuning.Feedback.ShardTrailTime;
		float time = Mathf.Min(trailTime * 3f, trailTime * Power);
		float turnRate = Mathf.DegToRad(Shards.OrbitSpeed * Speed);
		float size = 0.25f * Shards.Size * Area;
		Color color = ShardColor.Darkened(0.2f);

		for (int p = 0; p < TrailPieces; p++)
		{
			float along = (p + 1f) / TrailPieces;
			float pieceAngle = angle - turnRate * time * along;
			Vector2 around = new Vector2(Mathf.Cos(pieceAngle), Mathf.Sin(pieceAngle));
			float yaw = PlaneMath.YawFacing(new Vector2(-around.Y, around.X));
			_trailBatch.Set(shard * TrailPieces + p, center + around * orbit, ShardHeight, yaw,
				size * (1f - along), color.Lerp(Colors.Black, along));
		}
	}
}
