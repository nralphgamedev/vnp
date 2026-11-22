using Godot;
using System;

/// <summary>
/// A VNP Weapon that fires volleys of slow missiles that home in on enemies. The volley fans out as it
/// launches, then each missile turns toward its target at a limited rate, so it curves in. Each missile
/// heads for the nearest enemy when fired; if that enemy is destroyed first, it picks the nearest enemy
/// to itself within range, and flies straight on if there is none. A missile hits the first enemy in its
/// path, whether or not it was the target, and fizzles out if it flies too long.
/// </summary>
public partial class SeekerMissiles : VnpWeapon
{
	private struct Missile
	{
		public Vector2 Position;
		public Vector2 Velocity;
		public int TargetId;   // The enemy it is homing on, or 0 for none.
		public float Life;     // Seconds left before it fizzles out.
		public float Damage;
	}

	private const float MissileHeight = 0.5f;

	private SeekerMissilesDefinition Seekers => (SeekerMissilesDefinition)Definition;

	public override float Reach => Seekers.Range * Range;

	/// <summary>Damage compared with the start of the fight, so damage upgrades show as bigger missiles and blasts.</summary>
	private float Power => Mathf.Sqrt(Mathf.Max(0.01f, Damage / Seekers.Damage));

	private Missile[] _missiles = new Missile[32];
	private int _count;
	private float _cooldownLeft;
	private float _trailTimer;
	private InstanceBatch _batch;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Weapons;
		_batch = new InstanceBatch(this, "Missiles", Placeholders.Mesh("missile"), Placeholders.Material("instanced_unshaded"), 32);
	}

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		float dt = (float)delta;
		_cooldownLeft = Mathf.Max(0f, _cooldownLeft - dt);
		if (_cooldownLeft <= 0f && !Fight.Vnp.IsDestroyed && Launch())
			_cooldownLeft = Cooldown;

		Fly(dt);
		Draw();
	}

	/// <summary>Fires a volley at the nearest enemy in range. False if there is nothing to fire at.</summary>
	private bool Launch()
	{
		EnemySwarm enemies = Fight.Enemies;
		Vector2 from = Fight.Vnp.PlanePosition;
		int target = enemies.FindNearest(from, Seekers.Range);
		if (target < 0)
			return false;

		Vector2 toTarget = enemies.PositionOf(target) - from;
		Vector2 aim = toTarget.LengthSquared() > 0.0001f ? toTarget.Normalized() : Vector2.Up;
		int volley = Mathf.Max(1, Seekers.MissilesPerVolley + ExtraCount);
		float spread = Mathf.DegToRad(Seekers.LaunchSpread);

		for (int i = 0; i < volley; i++)
		{
			if (_count == _missiles.Length)
				Array.Resize(ref _missiles, _missiles.Length * 2);
			float angle = volley == 1 ? 0f : spread * ((float)i / (volley - 1) - 0.5f);
			_missiles[_count++] = new Missile
			{
				Position = from,
				Velocity = aim.Rotated(angle) * Seekers.MissileSpeed,
				TargetId = enemies.IdOf(target),
				Life = Seekers.MissileLifetime,
				Damage = Damage,
			};
		}

		Fight.Effects.MuzzleFlash(from, aim, Seekers.Color, Power);
		Fight.Sounds.Play(Sound.SeekerLaunch);
		return true;
	}

	private void Fly(float dt)
	{
		EnemySwarm enemies = Fight.Enemies;
		float turnStep = Mathf.DegToRad(Seekers.TurnRate) * dt;

		// Smoke is left at a steady rate, not every frame, so the trail looks the same at any frame rate.
		_trailTimer -= dt;
		bool smoke = _trailTimer <= 0f;
		if (smoke)
			_trailTimer = 0.03f;

		for (int i = _count - 1; i >= 0; i--)
		{
			ref Missile missile = ref _missiles[i];
			missile.Life -= dt;

			// Keep homing on the target, or find a new one if it has been destroyed.
			int target = missile.TargetId != 0 ? enemies.IndexOfId(missile.TargetId) : -1;
			if (target < 0)
			{
				target = enemies.FindNearest(missile.Position, Seekers.Range);
				missile.TargetId = target >= 0 ? enemies.IdOf(target) : 0;
			}
			if (target >= 0)
			{
				Vector2 toTarget = enemies.PositionOf(target) - missile.Position;
				if (toTarget.LengthSquared() > 0.0001f)
				{
					float heading = missile.Velocity.Angle();
					float wanted = toTarget.Angle();
					heading = Mathf.RotateToward(heading, wanted, turnStep);
					missile.Velocity = Vector2.FromAngle(heading) * Seekers.MissileSpeed;
				}
			}

			Vector2 before = missile.Position;
			missile.Position += missile.Velocity * dt;

			int hit = enemies.FindHit(before, missile.Position);
			if (hit >= 0)
			{
				Vector2 at = missile.Position;
				enemies.Damage(hit, missile.Damage, Source);
				Fight.Effects.Explosion(at, Seekers.Color, Power);
				Fight.Sounds.Play(Sound.SeekerHit);
				_missiles[i] = _missiles[--_count];
				continue;
			}

			if (missile.Life <= 0f)
			{
				Fight.Effects.Fizzle(missile.Position, Seekers.Color);
				_missiles[i] = _missiles[--_count];
				continue;
			}

			if (smoke)
			{
				Vector2 tail = missile.Position - missile.Velocity.Normalized() * Seekers.MissileSize * Power * 0.8f;
				Fight.Effects.MissileTrail(tail, Seekers.Color, 0.35f * Power);
			}
		}
	}

	private void Draw()
	{
		_batch.Begin(_count);
		float size = Seekers.MissileSize * Power;
		for (int i = 0; i < _count; i++)
		{
			ref Missile missile = ref _missiles[i];
			_batch.Set(i, missile.Position, MissileHeight, PlaneMath.YawFacing(missile.Velocity), size, Seekers.Color);
		}
		_batch.Commit(_count);
	}
}
