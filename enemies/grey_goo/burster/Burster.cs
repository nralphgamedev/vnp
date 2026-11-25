using Godot;

/// <summary>
/// The Burster, a Grey Goo type. Everything shared by Grey Goo (GreyGoo.cs) applies; it drains no drones and doesn't
/// grow. It is faster than the other types (its Speed in burster.tres), and its own part is its death:
/// - alive: a swollen, bubbly blob that throbs, faster the more it is hurt, the tell that it will burst;
/// - destroyed: it bursts into BlobCount tiny blobs, thrown out evenly all round it, that curl in on the VNP as
///   fragile shots (FragileShots.cs). Each pops on the first thing it hits, the VNP or a drone, and the player's
///   weapons can break them in flight. They leave no debris and don't count as kills.
/// The blobs declare themselves to pilots as they fly. Its numbers are in burster.tres.
/// </summary>
public sealed class Burster : GreyGoo
{
	private readonly BursterDefinition _type;

	public Burster(EnemySwarm swarm, BursterDefinition definition) : base(swarm, definition)
	{
		_type = definition;
	}

	public override Mesh Mesh => Placeholders.Mesh("burster");

	public override void Spawned(ref EnemySwarm.Enemy enemy)
	{
		base.Spawned(ref enemy);
		enemy.AttackDamage = _type.BlobDamage * Fight.GreyGooDamageScale;
		enemy.AttackTimer = Rng.Range(0f, 1f); // So a group doesn't throb in step.
	}

	public override void Died(in EnemySwarm.Enemy enemy)
	{
		base.Died(enemy);
		Fight.Sounds.Play(Sound.BursterBurst);
		int count = Mathf.Max(0, _type.BlobCount);
		float start = Rng.Range(0f, Mathf.Tau);
		for (int b = 0; b < count; b++)
		{
			Vector2 out_ = Vector2.FromAngle(start + Mathf.Tau * b / count);
			Fight.FragileShots.FireHoming(enemy.Id, enemy.Position + out_ * enemy.Radius, out_, _type.BlobSpeed,
				_type.BlobTurnDegrees, _type.BlobLife, enemy.AttackDamage, _type.BlobRadius, Source, _type.Color,
				_type.BlobBlastRadius);
		}
	}

	/// <summary>Its throb clock, kept in its attack timer, which it has no other use for: it runs faster as its
	/// health runs out, and counts on from where it was, so a hit speeds the throb up without a jump.</summary>
	protected override bool ActOwn(ref EnemySwarm.Enemy enemy, int index, in EnemySwarm.Target target, float dt)
	{
		float hurt = 1f - Mathf.Clamp(enemy.Health / Mathf.Max(0.01f, enemy.BaseHealth), 0f, 1f);
		enemy.AttackTimer = Mathf.PosMod(enemy.AttackTimer + dt * _type.ThrobSpeed * (1f + (_type.HurtThrobFactor - 1f) * hurt), 1f);
		return false;
	}

	/// <summary>It throbs, swelling and shrinking, faster as its health runs out.</summary>
	protected override Basis Reshape(in EnemySwarm.Enemy enemy, Basis shape, float time)
	{
		float throb = 1f + _type.Throb * (0.5f + 0.5f * Mathf.Sin(enemy.AttackTimer * Mathf.Tau));
		return shape.Scaled(Vector3.One * throb);
	}
}
