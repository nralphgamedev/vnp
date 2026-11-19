using Godot;
using System;

/// <summary>
/// A VNP Weapon that drops mines behind the VNP while it moves. A mine explodes when an enemy touches it,
/// damaging every enemy within its blast. Mines are dropped only while the VNP is moving, one per cooldown.
/// Each lasts a set time; if a new mine would go over the most allowed at once, the oldest one disappears.
/// Area of effect upgrades widen the blast; damage upgrades make mines and blasts bigger.
/// </summary>
public partial class MineLayer : VnpWeapon
{
	private struct Mine
	{
		public Vector2 Position;
		public float Age;
		public float Yaw;
		public float Damage;
	}

	private const float MineHeight = 0.1f;

	private MineLayerDefinition Mines => (MineLayerDefinition)Definition;

	/// <summary>Damage compared with the start of the fight, so damage upgrades show as bigger mines and blasts.</summary>
	private float Power => Mathf.Sqrt(Mathf.Max(0.01f, Damage / Mines.Damage));

	private Mine[] _mines = new Mine[16];
	private int _count;
	private float _cooldownLeft;
	private readonly int[] _caught = new int[1024];
	private readonly Random _random = new();
	private InstanceBatch _batch;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Weapons;
		_batch = new InstanceBatch(this, "Mines", Placeholders.Mesh("mine"), Placeholders.Material("instanced_lit"), 16);
	}

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		float dt = (float)delta;
		_cooldownLeft = Mathf.Max(0f, _cooldownLeft - dt);

		Vnp vnp = Fight.Vnp;
		bool moving = vnp.Velocity.Length() >= Mines.MinMoveSpeed;
		if (_cooldownLeft <= 0f && moving && !vnp.IsDestroyed)
		{
			Drop(vnp.PlanePosition - vnp.Velocity.Normalized() * Mines.DropDistance);
			_cooldownLeft = Cooldown;
		}

		Update(dt);
		Draw();
	}

	private void Drop(Vector2 at)
	{
		// Over the limit: the oldest mine disappears to make room.
		if (_count >= Mathf.Max(1, Mines.MaxMines))
			Remove(Oldest(), fizzle: true);

		if (_count == _mines.Length)
			Array.Resize(ref _mines, _mines.Length * 2);
		_mines[_count++] = new Mine { Position = at, Yaw = (float)_random.NextDouble() * Mathf.Tau, Damage = Damage };
		Fight.Sounds.Play(Sound.MineDrop);
	}

	private void Update(float dt)
	{
		EnemySwarm enemies = Fight.Enemies;
		float trigger = Mines.TriggerRadius + Tuning.EnemySpawning.Enemy.HitRadius;

		for (int i = _count - 1; i >= 0; i--)
		{
			ref Mine mine = ref _mines[i];
			mine.Age += dt;
			if (mine.Age >= Mines.MineLifetime)
			{
				Remove(i, fizzle: true);
				continue;
			}

			if (enemies.CollectNear(mine.Position, trigger, _caught) > 0)
				Explode(i);
		}
	}

	/// <summary>Damages every enemy within the blast and removes the mine.</summary>
	private void Explode(int index)
	{
		Mine mine = _mines[index];
		float blast = Mines.BlastRadius * Area;
		EnemySwarm enemies = Fight.Enemies;
		int caught = enemies.CollectNear(mine.Position, blast + Tuning.EnemySpawning.Enemy.HitRadius, _caught);
		for (int c = 0; c < caught; c++)
			enemies.Damage(_caught[c], mine.Damage);

		Fight.Effects.Blast(mine.Position, Mines.Color, blast, Power);
		Fight.Sounds.Play(Sound.MineExplode);
		Remove(index, fizzle: false);
	}

	private int Oldest()
	{
		int oldest = 0;
		for (int i = 1; i < _count; i++)
		{
			if (_mines[i].Age > _mines[oldest].Age)
				oldest = i;
		}
		return oldest;
	}

	private void Remove(int index, bool fizzle)
	{
		if (fizzle)
			Fight.Effects.Fizzle(_mines[index].Position, Mines.Color);
		_mines[index] = _mines[--_count]; // Fill the gap with the last one.
	}

	private void Draw()
	{
		_batch.Begin(_count);
		float size = Mines.MineSize * Power;
		for (int i = 0; i < _count; i++)
		{
			ref Mine mine = ref _mines[i];
			// A slow blink, quickening in the last two seconds so the player can see which are about to go.
			float left = Mines.MineLifetime - mine.Age;
			float rate = left < 2f ? 8f : 2.5f;
			float blink = 0.5f + 0.5f * Mathf.Sin(mine.Age * rate * Mathf.Tau);
			Color color = Mines.Color.Darkened(0.5f).Lerp(Mines.Color.Lerp(Colors.White, 0.3f), blink);
			_batch.Set(i, mine.Position, MineHeight, mine.Yaw + mine.Age * 0.6f, size, color);
		}
		_batch.Commit(_count);
	}
}
