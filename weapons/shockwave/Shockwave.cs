using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// A VNP Weapon that sends out a ring every cooldown, whether or not an enemy is near. Each ring spreads from where the VNP was when it went
/// out, at a set speed, until it reaches its radius, damaging each enemy once as it passes through. More
/// than one ring can be spreading at a time. Area of effect upgrades make the ring reach further; damage
/// upgrades make it brighter.
/// </summary>
public partial class Shockwave : VnpWeapon
{
	private sealed class Ring
	{
		public Vector2 Center;
		public float Radius;
		public float MaxRadius;
		public float Damage;
		public readonly HashSet<int> Hit = new();
	}

	private const float RingHeight = 0.2f;

	private ShockwaveDefinition Wave => (ShockwaveDefinition)Definition;

	public override float Reach => Wave.Radius * Area;

	/// <summary>It hits what comes within its reach around the VNP.</summary>
	public override void DeclareZones(Arbiter.Situation situation, int source) => DeclareReach(situation, source);

	/// <summary>Damage compared with the start of the fight, so damage upgrades show as a brighter ring.</summary>
	private float Power => Mathf.Sqrt(Mathf.Max(0.01f, Damage / Wave.Damage));

	private readonly List<Ring> _rings = new();
	private readonly int[] _caught = new int[1024];
	private float _cooldownLeft;
	private InstanceBatch _batch;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Weapons;
		_batch = new InstanceBatch(this, "ShockwaveRings", Placeholders.Mesh("shockwave_ring"),
			Placeholders.Material("instanced_glow"), 8);
	}

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		float dt = (float)delta;
		_cooldownLeft = Mathf.Max(0f, _cooldownLeft - dt);
		if (_cooldownLeft <= 0f && !Fight.Vnp.IsDestroyed)
		{
			SendRing();
			_cooldownLeft = Cooldown;
		}

		Spread(dt);
		Draw();
	}

	private void SendRing()
	{
		_rings.Add(new Ring
		{
			Center = Fight.Vnp.PlanePosition,
			MaxRadius = Wave.Radius * Area,
			Damage = Damage,
		});
		Fight.Sounds.Play(Sound.ShockwaveFire);
	}

	private void Spread(float dt)
	{
		EnemySwarm enemies = Fight.Enemies;

		for (int r = _rings.Count - 1; r >= 0; r--)
		{
			Ring ring = _rings[r];
			float before = ring.Radius;
			ring.Radius = Mathf.Min(ring.MaxRadius, ring.Radius + Wave.RingSpeed * dt);
			// Only if explosive: it breaks fragile enemy shots it passes over this frame, such as a Quiller's spikes.
			if (Definition.Explosive)
				Fight.FragileShots.DestroyInBand(ring.Center, before, ring.Radius, Source);

			// Everything the ring has reached so far, that it has not hit yet.
			int caught = enemies.CollectTouching(ring.Center, ring.Radius, _caught);
			for (int c = 0; c < caught; c++)
			{
				int enemy = _caught[c];
				if (!ring.Hit.Add(enemies.IdOf(enemy)))
					continue;
				Fight.Effects.Impact(enemies.PositionOf(enemy), Wave.Color, 0.5f);
				enemies.Damage(enemy, ring.Damage, Source);
			}

			if (ring.Radius >= ring.MaxRadius)
				_rings.RemoveAt(r);
		}
	}

	private void Draw()
	{
		_batch.Begin(_rings.Count);
		for (int r = 0; r < _rings.Count; r++)
		{
			Ring ring = _rings[r];
			// Bright as it leaves, fading as it nears its full reach.
			float spent = ring.Radius / Mathf.Max(0.01f, ring.MaxRadius);
			Color color = Wave.Color.Lerp(Colors.White, Mathf.Clamp((Power - 1f) * 0.5f, 0f, 0.5f)) * (1f - spent * 0.8f);
			_batch.Set(r, ring.Center, RingHeight, 0f, Mathf.Max(0.01f, ring.Radius), color);
		}
		_batch.Commit(_rings.Count);
	}

#if RECORDING
	/// <summary>Each ring still spreading, as its centre and how far it has reached.</summary>
	public override void CollectPlaced(List<PlacedObject> placed)
	{
		foreach (Ring ring in _rings)
			placed.Add(PlacedObject.Ring(ring.Center, ring.Radius));
	}
#endif
}
