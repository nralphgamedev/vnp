using Godot;
using System;

/// <summary>
/// A VNP Weapon that fires volleys of slow missiles that home in on enemies. The volley fans out as it
/// launches, then each missile turns toward its target at a limited rate, so it curves in. A volley spreads
/// over the nearest enemies in range, one missile each, nearest first, and shares them out only when there
/// are fewer enemies than missiles. If a missile's target is destroyed first, it picks the nearest enemy to
/// itself within range that no other missile is after (the nearest at all if every one is), and flies
/// straight on if there is none. A missile hits the first enemy in its path, whether or not it was the
/// target, and fizzles out if it flies too long.
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

	/// <summary>It hits what comes within its reach around the VNP.</summary>
	public override void DeclareZones(Arbiter.Situation situation, int source) => DeclareReach(situation, source);

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
		int volley = Mathf.Max(1, Seekers.MissilesPerVolley + ExtraCount);
		int targets = NearestTargets(from, volley);
		if (targets == 0)
			return false;

		Vector2 toTarget = enemies.PositionOf(_targets[0]) - from;
		Vector2 aim = toTarget.LengthSquared() > 0.0001f ? toTarget.Normalized() : Vector2.Up;
		float spread = Mathf.DegToRad(Seekers.LaunchSpread);

		// Order the targets round the volley's fan, so each missile leaves on the side of the one it goes for and
		// the missiles don't cross.
		for (int t = 0; t < targets; t++)
			_targetAngles[t] = aim.AngleTo(enemies.PositionOf(_targets[t]) - from);
		Array.Sort(_targetAngles, _targets, 0, targets);

		for (int i = 0; i < volley; i++)
		{
			if (_count == _missiles.Length)
				Array.Resize(ref _missiles, _missiles.Length * 2);
			float angle = volley == 1 ? 0f : spread * ((float)i / (volley - 1) - 0.5f);
			// With fewer targets than missiles, neighbouring missiles share a target, so they are spread evenly.
			int target = _targets[i * targets / volley];
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

	private readonly int[] _near = new int[1024];
	private int[] _targets = new int[8];
	private float[] _targetAngles = new float[8];

	/// <summary>
	/// Fills _targets with up to <paramref name="most"/> different enemies within range of a point, nearest first, and
	/// returns how many there are.
	/// </summary>
	private int NearestTargets(Vector2 from, int most)
	{
		if (_targets.Length < most)
		{
			_targets = new int[most];
			_targetAngles = new float[most];
		}
		EnemySwarm enemies = Fight.Enemies;
		int found = enemies.CollectNear(from, Seekers.Range, _near);
		int kept = Mathf.Min(most, found);
		// A partial selection sort: only the nearest few are needed, from what may be a crowd.
		for (int k = 0; k < kept; k++)
		{
			int best = k;
			float bestSquared = from.DistanceSquaredTo(enemies.PositionOf(_near[k]));
			for (int c = k + 1; c < found; c++)
			{
				float squared = from.DistanceSquaredTo(enemies.PositionOf(_near[c]));
				if (squared < bestSquared)
				{
					bestSquared = squared;
					best = c;
				}
			}
			(_near[k], _near[best]) = (_near[best], _near[k]);
			_targets[k] = _near[k];
		}
		return kept;
	}

	/// <summary>
	/// A new target for a missile whose own is gone: the nearest enemy within range of it that no other missile is
	/// after, or, if every one is taken, the nearest. -1 if there is none.
	/// </summary>
	private int NewTarget(int missile)
	{
		EnemySwarm enemies = Fight.Enemies;
		Vector2 at = _missiles[missile].Position;
		int found = enemies.CollectNear(at, Seekers.Range, _near);
		int nearest = -1, nearestFree = -1;
		float nearestSquared = float.MaxValue, freeSquared = float.MaxValue;
		for (int c = 0; c < found; c++)
		{
			int index = _near[c];
			float squared = at.DistanceSquaredTo(enemies.PositionOf(index));
			if (squared < nearestSquared)
			{
				nearestSquared = squared;
				nearest = index;
			}
			if (squared < freeSquared && !Claimed(enemies.IdOf(index), missile))
			{
				freeSquared = squared;
				nearestFree = index;
			}
		}
		return nearestFree >= 0 ? nearestFree : nearest;
	}

	/// <summary>Whether a missile other than <paramref name="except"/> is homing on this enemy.</summary>
	private bool Claimed(int id, int except)
	{
		for (int i = 0; i < _count; i++)
		{
			if (i != except && _missiles[i].TargetId == id)
				return true;
		}
		return false;
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
				target = NewTarget(i);
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
				Explode(missile.Position, missile.Damage, hit);
				_missiles[i] = _missiles[--_count];
				continue;
			}

			// An explosive missile that flies into a fragile enemy shot, such as a Quiller's spike, breaks it and
			// explodes there.
			if (Definition.Explosive && Fight.FragileShots.DestroyAlong(before, missile.Position, 0.3f, Source))
			{
				Explode(missile.Position, missile.Damage, -1);
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

	/// <summary>How far an explosion reaches, with area of effect upgrades.</summary>
	private float BlastRadius => Seekers.BlastRadius * Area;

	private readonly int[] _caught = new int[1024];

	/// <summary>
	/// A missile explodes: the enemy it hit, if any, takes its damage, every other enemy within its blast takes
	/// SplashDamage of it, and, since it is explosive, fragile enemy shots there break.
	/// </summary>
	private void Explode(Vector2 at, float damage, int hit)
	{
		EnemySwarm enemies = Fight.Enemies;
		if (hit >= 0)
			enemies.Damage(hit, damage, Source);
		float blast = BlastRadius;
		float splash = damage * Seekers.SplashDamage;
		if (blast > 0f)
		{
			int caught = enemies.CollectTouching(at, blast, _caught);
			for (int c = 0; c < caught; c++)
			{
				if (_caught[c] != hit)
					enemies.Damage(_caught[c], splash, Source);
			}
			if (Definition.Explosive)
				Fight.FragileShots.DestroyWithin(at, blast, Source);
			Fight.Effects.Blast(at, Seekers.Color, blast, Power);
		}
		Fight.Effects.Explosion(at, Seekers.Color, Power);
		Fight.Sounds.Play(Sound.SeekerHit);
	}

#if RECORDING
	public override void CollectPlaced(System.Collections.Generic.List<PlacedObject> placed)
	{
		for (int i = 0; i < _count; i++)
			placed.Add(PlacedObject.Point(_missiles[i].Position, Mathf.Max(0.5f, BlastRadius)));
	}
#endif
}

