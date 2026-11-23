using Godot;

/// <summary>
/// The basic goo, the most common Grey Goo type. Everything shared by Grey Goo (GreyGoo.cs) applies: it heads
/// for drones first, drains and absorbs any drone touching it, grows with each one, and hurts the VNP only by
/// touching it. Its own part is splitting: once it has absorbed enough drones, it bulges and splits into two
/// basic goos that share what it had. Its numbers are in basic_goo.tres.
/// </summary>
public sealed class BasicGoo : GreyGoo
{
	private readonly BasicGooDefinition _type;

	// While bulging before a split, how far it stretches along the split, and squeezes across it.
	private const float BulgeStretch = 0.7f;
	private const float BulgeSqueeze = 0.25f;

	public BasicGoo(EnemySwarm swarm, BasicGooDefinition definition) : base(swarm, definition)
	{
		_type = definition;
	}

	/// <summary>Bulging toward a split. The swarm splits it once the bulge is done.</summary>
	protected override bool ActOwn(ref EnemySwarm.Enemy enemy, int index, in EnemySwarm.Target target, float dt)
	{
		if (enemy.SplitTimer > 0f)
		{
			enemy.SplitTimer -= dt;
			if (enemy.SplitTimer <= 0f)
				Swarm.QueueSplit(index);
		}
		return false;
	}

	protected override void AfterAbsorbing(ref EnemySwarm.Enemy enemy)
	{
		enemy.AbsorbedSinceSplit++;
		StartSplitIfFull(ref enemy);
	}

	/// <summary>Once it has absorbed enough drones, it starts to bulge, and splits when the bulge is done.</summary>
	private void StartSplitIfFull(ref EnemySwarm.Enemy enemy)
	{
		if (_type.DronesToSplit > 0 && enemy.AbsorbedSinceSplit >= _type.DronesToSplit && enemy.SplitTimer <= 0f)
		{
			enemy.SplitTimer = Mathf.Max(0.01f, _type.SplitBulgeSeconds);
			enemy.SplitAngle = Rng.Range(0f, Mathf.Tau);
		}
	}

	/// <summary>
	/// Splits it in two along the way it bulged. <paramref name="goo"/> becomes one half and
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

	/// <summary>The most it can have absorbed before it splits sets the largest debris it leaves.</summary>
	public override float LargestDebrisMass => _type.DebrisMassMax + _type.DronesToSplit * _type.DebrisMassPerDrone;

	/// <summary>Before a split, it bulges along the line it will split on, quivering more as the split nears.</summary>
	protected override Basis Reshape(in EnemySwarm.Enemy enemy, Basis shape, float time)
	{
		if (enemy.SplitTimer <= 0f)
			return shape;
		float bulge = 1f - enemy.SplitTimer / Mathf.Max(0.01f, _type.SplitBulgeSeconds);
		float quiver = 1f + 0.06f * bulge * Mathf.Sin(time * 40f);
		return StretchAlong(Vector2.FromAngle(enemy.SplitAngle), (1f + BulgeStretch * bulge) * quiver,
			1f - BulgeSqueeze * bulge) * shape;
	}

}
