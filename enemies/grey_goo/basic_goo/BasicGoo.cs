using Godot;

/// <summary>
/// The Goo: a slow, wobbling blob. It goes after drones first, and drains the health of any drone touching
/// it, whatever the drone was doing; a drone drained to nothing is absorbed. Each absorbed drone adds to its bulk, so it grows larger and tougher,
/// and once it has absorbed enough it bulges and splits into two Goos that share what it had. It hurts
/// the VNP only by touching it, once per touch with a cooldown. Its numbers are in goo.tres.
/// </summary>
public sealed class Goo : EnemyBehavior
{
	private readonly GooDefinition _type;

	// How fast a Goo slowly turns as it drifts, in radians a second, so its lumps shift as it goes.
	private const float SpinSpeed = 0.4f;
	// A Goo is a flattened blob: its height compared with its width.
	private const float Flatness = 0.7f;
	// While bulging before a split, how far it stretches along the split, and squeezes across it.
	private const float BulgeStretch = 0.7f;
	private const float BulgeSqueeze = 0.25f;

	public Goo(EnemySwarm swarm, GooDefinition definition) : base(swarm, definition)
	{
		_type = definition;
	}

	public override Mesh Mesh => Placeholders.Mesh("goo");

	/// <summary>Glossy, with a soft glow at its edges, so it reads as wet slime rather than rock.</summary>
	public override Material Material => Placeholders.Material("goo");

	public override bool AbsorbsDrones => true;

	public override bool DamagesByTouch => true;

	public override void Spawned(ref EnemySwarm.Enemy enemy)
	{
		// Goos spawned later in the fight are stronger, and keep that strength.
		enemy.Health = _type.Health * Fight.GooHealthScale;
		enemy.BaseHealth = enemy.Health;
		enemy.Damage = _type.ContactDamage * Fight.GooDamageScale;
		enemy.Drain = _type.DroneDrainPerSecond * Fight.GooDamageScale;
		enemy.Bulk = 1f;
		enemy.Phase = Rng.Range(0f, Mathf.Tau);
		Resize(ref enemy);
	}

	/// <summary>Size follows the square root of bulk, so twice the bulk covers twice the area.</summary>
	private void Resize(ref EnemySwarm.Enemy enemy)
	{
		float grown = Mathf.Sqrt(Mathf.Max(0.01f, enemy.Bulk));
		enemy.Radius = _type.HitRadius * grown;
		enemy.Scale = _type.Size * grown;
	}

	/// <summary>A Goo heads straight for its target, so it can touch it.</summary>
	public override Vector2 Chase(ref EnemySwarm.Enemy enemy, Vector2 track, float distance) => track;

	public override bool Act(ref EnemySwarm.Enemy enemy, int index, in EnemySwarm.Target target, float dt)
	{
		enemy.FireTimer = Mathf.Max(0f, enemy.FireTimer - dt);
		enemy.Pulse = Mathf.Max(0f, enemy.Pulse - dt);
		enemy.Yaw += SpinSpeed * enemy.OrbitSign * dt;

		// Touching the VNP damages it, then the Goo has to wait out its cooldown.
		Vnp vnp = Fight.Vnp;
		if (!vnp.IsDestroyed && enemy.FireTimer <= 0f
			&& enemy.Position.DistanceTo(vnp.PlanePosition) <= enemy.Radius + Tuning.Vnp.HitRadius)
		{
			vnp.TakeDamage(enemy.Damage, Source);
			enemy.FireTimer = _type.ContactCooldown;
			Vector2 toward = (vnp.PlanePosition - enemy.Position).Normalized();
			Fight.Effects.Impact(enemy.Position + toward * enemy.Radius, _type.Color, 1f);
		}

		// Bulging toward a split. The swarm splits it once the bulge is done.
		if (enemy.SplitTimer > 0f)
		{
			enemy.SplitTimer -= dt;
			if (enemy.SplitTimer <= 0f)
				Swarm.QueueSplit(index);
		}
		return true;
	}

	public override float DroneDrain(in EnemySwarm.Enemy enemy) => enemy.Drain;

	public override void Absorb(ref EnemySwarm.Enemy enemy)
	{
		enemy.Absorbed += 1f;
		enemy.AbsorbedSinceSplit++;
		enemy.Bulk += _type.BulkPerDrone;
		// Tougher as well as larger: health rises by the same share as bulk, measured against its health at
		// spawn, and the extra is added to what it has left.
		float extraHealth = enemy.BaseHealth * _type.BulkPerDrone;
		enemy.Health += extraHealth;
		Resize(ref enemy);
		enemy.Pulse = _type.AbsorbPulseSeconds;
		Fight.Sounds.Play(Sound.GooAbsorb);
		StartSplitIfFull(ref enemy);
	}

	/// <summary>Once a Goo has absorbed enough drones, it starts to bulge, and splits when the bulge is done.</summary>
	private void StartSplitIfFull(ref EnemySwarm.Enemy enemy)
	{
		if (_type.DronesToSplit > 0 && enemy.AbsorbedSinceSplit >= _type.DronesToSplit && enemy.SplitTimer <= 0f)
		{
			enemy.SplitTimer = Mathf.Max(0.01f, _type.SplitBulgeSeconds);
			enemy.SplitAngle = Rng.Range(0f, Mathf.Tau);
		}
	}

	/// <summary>
	/// Splits a Goo in two along the way it bulged. <paramref name="goo"/> becomes one half and
	/// <paramref name="other"/>, a copy of it, the other. They share its bulk, health and absorbed drones
	/// evenly, and push apart.
	/// </summary>
	public void Split(ref EnemySwarm.Enemy goo, ref EnemySwarm.Enemy other)
	{
		// Drones absorbed while it bulged, beyond the number that made it split, are shared between the
		// halves and count toward their own next split.
		int extra = Mathf.Max(0, goo.AbsorbedSinceSplit - _type.DronesToSplit);
		goo.Bulk *= 0.5f;
		goo.Health *= 0.5f;
		goo.Absorbed *= 0.5f;
		goo.AbsorbedSinceSplit = (extra + 1) / 2;
		goo.SplitTimer = 0f;
		goo.Pulse = _type.AbsorbPulseSeconds;
		Resize(ref goo);

		Vector2 along = Vector2.FromAngle(goo.SplitAngle);
		Vector2 middle = goo.Position;
		other = goo;
		goo.Position = middle + along * goo.Radius * 0.6f;
		other.Position = middle - along * goo.Radius * 0.6f;
		goo.Velocity += along * _type.Speed;
		other.Velocity -= along * _type.Speed;
		other.Phase = goo.Phase + Mathf.Pi;
		other.OrbitSign = -goo.OrbitSign;
		other.TargetDroneId = 0;
		other.TargetTimer = 0f;
		other.AbsorbedSinceSplit = extra / 2;
		StartSplitIfFull(ref goo);
		StartSplitIfFull(ref other);

		Fight.Effects.GooSplit(middle, _type.Color, goo.Radius * 2f);
		Fight.Sounds.Play(Sound.GooSplit);
	}

	/// <summary>Its own debris, plus some for every drone it absorbed, which wins back part of what it took.</summary>
	public override float DebrisMass(in EnemySwarm.Enemy enemy) =>
		base.DebrisMass(enemy) + enemy.Absorbed * _type.DebrisMassPerDrone;

	public override float LargestDebrisMass => _type.DebrisMassMax + _type.DronesToSplit * _type.DebrisMassPerDrone;

	public override void Died(in EnemySwarm.Enemy enemy)
	{
		Fight.Effects.EnemyDeath(enemy.Position, _type.Color, Mathf.Sqrt(Mathf.Max(1f, enemy.Bulk)));
		Fight.Sounds.Play(Sound.GooDeath);
	}

	public override void Draw(int slot, in EnemySwarm.Enemy enemy, Color color)
	{
		// A slow wobble: it squashes one way and stretches the other, out of step, so it never looks rigid.
		float time = (float)Fight.Elapsed;
		float wave = time * _type.WobbleSpeed * Mathf.Tau + enemy.Phase;
		float wobble = _type.Wobble;
		float width = 1f + wobble * Mathf.Sin(wave);
		float depth = 1f + wobble * Mathf.Sin(wave + 2.1f);
		float height = Flatness * (1f + wobble * 0.5f * Mathf.Sin(wave + 4.2f));

		// It swells for a moment when it absorbs a drone.
		float swell = 1f;
		if (_type.AbsorbPulseSeconds > 0f && enemy.Pulse > 0f)
			swell += _type.AbsorbPulse * enemy.Pulse / _type.AbsorbPulseSeconds;

		Basis shape = new Basis(Vector3.Up, enemy.Yaw) * Basis.FromScale(new Vector3(width, height, depth));

		// Before a split, it bulges along the line it will split on, quivering more as the split nears.
		if (enemy.SplitTimer > 0f)
		{
			float bulge = 1f - enemy.SplitTimer / Mathf.Max(0.01f, _type.SplitBulgeSeconds);
			float quiver = 1f + 0.06f * bulge * Mathf.Sin(time * 40f);
			shape = StretchAlong(Vector2.FromAngle(enemy.SplitAngle), (1f + BulgeStretch * bulge) * quiver,
				1f - BulgeSqueeze * bulge) * shape;
		}

		Batch.SetBasis(slot, enemy.Position, 0f, shape.Scaled(Vector3.One * enemy.Scale * swell), color);
	}

	/// <summary>A basis that stretches by <paramref name="along"/> in a direction on the plane and by
	/// <paramref name="across"/> at right angles to it, leaving height alone.</summary>
	private static Basis StretchAlong(Vector2 direction, float along, float across)
	{
		Vector3 u = PlaneMath.ToWorld(direction);
		Vector3 v = new Vector3(-u.Z, 0f, u.X);
		// Each column is where one axis ends up: along * u(u.axis) + across * v(v.axis), height unchanged.
		Vector3 Column(float uPart, float vPart) => u * (along * uPart) + v * (across * vPart);
		return new Basis(Column(u.X, v.X), Vector3.Up, Column(u.Z, v.Z));
	}
}
