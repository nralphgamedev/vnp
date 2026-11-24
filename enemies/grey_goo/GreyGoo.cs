using Arbiter;
using Godot;

/// <summary>
/// What every Grey Goo type does, on top of the swarm's shared movement, targeting and flocking: it heads
/// straight for its target, damages the VNP by touching it (once per touch, with a cooldown), drains and
/// absorbs drones that touch it if its type does (DroneDrainPerSecond above 0), grows larger and tougher with
/// each drone it absorbs, leaves more debris for what it absorbed, and is drawn as a slowly wobbling, glossy
/// grey blob. Each type is a subclass in its own folder below res://enemies/grey_goo/, beside its definition,
/// adding its own attack, such as the basic goo's splitting (BasicGoo.cs).
/// </summary>
public abstract class GreyGoo : EnemyBehavior
{
	protected readonly GreyGooDefinition GooType;

	// How fast it slowly turns as it drifts, in radians a second, so its lumps shift as it goes.
	private const float SpinSpeed = 0.4f;
	// A Grey Goo is a flattened blob: its height compared with its width.
	private const float Flatness = 0.7f;

	protected GreyGoo(EnemySwarm swarm, GreyGooDefinition definition) : base(swarm, definition)
	{
		GooType = definition;
	}

	public override Mesh Mesh => Placeholders.Mesh("goo");

	/// <summary>Glossy, with a soft glow at its edges, so it reads as wet slime rather than rock.</summary>
	public override Material Material => Placeholders.Material("goo");

	public override bool AbsorbsDrones => GooType.DroneDrainPerSecond > 0f;

	public override bool DamagesByTouch => true;

	/// <summary>Whether it turns slowly as it drifts, so its lumps shift. A type that faces where it attacks,
	/// such as the Lancer, doesn't.</summary>
	protected virtual bool Spins => true;

	/// <summary>Whether touching it hurts the VNP right now. The Lancer's touch doesn't while it launches, since
	/// the launch itself is what hits.</summary>
	protected virtual bool TouchHurts(in EnemySwarm.Enemy enemy) => true;

	public override void Spawned(ref EnemySwarm.Enemy enemy)
	{
		// Grey Goo spawned later in the fight is stronger, and keeps that strength.
		enemy.Health = GooType.Health * Fight.GreyGooHealthScale;
		enemy.BaseHealth = enemy.Health;
		enemy.Damage = GooType.ContactDamage * Fight.GreyGooDamageScale;
		enemy.Drain = GooType.DroneDrainPerSecond * Fight.GreyGooDamageScale;
		enemy.Bulk = 1f;
		enemy.Phase = Rng.Range(0f, Mathf.Tau);
		Resize(ref enemy);
	}

	/// <summary>Size follows the square root of bulk, so twice the bulk covers twice the area.</summary>
	protected void Resize(ref EnemySwarm.Enemy enemy)
	{
		float grown = Mathf.Sqrt(Mathf.Max(0.01f, enemy.Bulk));
		enemy.Radius = GooType.HitRadius * grown;
		enemy.Scale = GooType.Size * grown;
	}

	/// <summary>It heads straight for its target, so it can touch it.</summary>
	public override Vector2 Chase(ref EnemySwarm.Enemy enemy, Vector2 track, float distance) => track;

	public override bool Act(ref EnemySwarm.Enemy enemy, int index, in EnemySwarm.Target target, float dt)
	{
		enemy.FireTimer = Mathf.Max(0f, enemy.FireTimer - dt);
		enemy.Pulse = Mathf.Max(0f, enemy.Pulse - dt);
		if (Spins)
			enemy.Yaw += SpinSpeed * enemy.OrbitSign * dt;

		// Touching the VNP damages it, then it has to wait out its cooldown.
		Vnp vnp = Fight.Vnp;
		if (!vnp.IsDestroyed && enemy.FireTimer <= 0f && TouchHurts(enemy)
			&& enemy.Position.DistanceTo(vnp.PlanePosition) <= enemy.Radius + Tuning.Vnp.HitRadius)
		{
			vnp.TakeDamage(enemy.Damage, Source);
			enemy.FireTimer = GooType.ContactCooldown;
			Vector2 toward = (vnp.PlanePosition - enemy.Position).Normalized();
			Fight.Effects.Impact(enemy.Position + toward * enemy.Radius, GooType.Color, 1f);
		}

		// A type that spins keeps the facing its spin gives it; one that doesn't is faced where it goes, unless its
		// own attack faced it somewhere.
		return ActOwn(ref enemy, index, target, dt) || Spins;
	}

	/// <summary>The type's own attack for one frame, after the shared touch damage. Returns true if it set the
	/// enemy's facing itself; otherwise a type that doesn't spin is faced the way it is going.</summary>
	protected virtual bool ActOwn(ref EnemySwarm.Enemy enemy, int index, in EnemySwarm.Target target, float dt) => false;

	public override float DroneDrain(in EnemySwarm.Enemy enemy) => enemy.Drain;

	/// <summary>Its body, which hurts the VNP by touch, and for a type that drains drones, the same area draining
	/// drones each second. Both move with it. Its touch cooldown isn't declared, since a player can't see it.</summary>
	public override void DeclareThreats(in EnemySwarm.Enemy enemy, Situation situation)
	{
		DeclareBody(enemy, situation, OwnTags);
		Area body = Area.Circle(enemy.Position.ToArbiter(), enemy.Radius, enemy.Velocity.ToArbiter());
		if (TouchHurts(enemy))
			situation.Add(Hazard.Lasting(body, enemy.Damage, Endangers.Agent, enemy.Id));
		if (AbsorbsDrones)
			situation.Add(Hazard.Lasting(body, enemy.Drain, Endangers.Allies, enemy.Id, perSecond: true));
	}

	public override void Absorb(ref EnemySwarm.Enemy enemy)
	{
		enemy.Absorbed += 1f;
		Grow(ref enemy, GooType.BulkPerDrone);
		enemy.Pulse = GooType.AbsorbPulseSeconds;
		Fight.Sounds.Play(Sound.GooAbsorb);
		AfterAbsorbing(ref enemy);
	}

	/// <summary>The most bulk the type can grow to, such as a Devourer's large limit. No limit by default.</summary>
	protected virtual float MaxBulk => float.MaxValue;

	/// <summary>
	/// Grows by <paramref name="bulk"/>, up to MaxBulk: larger, and tougher as well, since health rises by the same
	/// share as bulk, measured against its health at spawn, and the extra is added to what it has left.
	/// </summary>
	protected void Grow(ref EnemySwarm.Enemy enemy, float bulk)
	{
		bulk = Mathf.Min(bulk, MaxBulk - enemy.Bulk);
		if (bulk <= 0f)
			return;
		enemy.Bulk += bulk;
		enemy.Health += enemy.BaseHealth * bulk;
		Resize(ref enemy);
	}

	/// <summary>What the type does once it has absorbed a drone and grown, such as starting to split.</summary>
	protected virtual void AfterAbsorbing(ref EnemySwarm.Enemy enemy)
	{
	}

	/// <summary>Its own debris, plus some for every drone it absorbed, which wins back part of what it took.</summary>
	public override float DebrisMass(in EnemySwarm.Enemy enemy) =>
		base.DebrisMass(enemy) + enemy.Absorbed * GooType.DebrisMassPerDrone;

	public override void Died(in EnemySwarm.Enemy enemy)
	{
		Fight.Effects.EnemyDeath(enemy.Position, GooType.Color, Mathf.Sqrt(Mathf.Max(1f, enemy.Bulk)));
		Fight.Sounds.Play(Sound.GooDeath);
	}

	public override void Draw(int slot, in EnemySwarm.Enemy enemy, Color color)
	{
		// A slow wobble: it squashes one way and stretches the other, out of step, so it never looks rigid.
		float time = (float)Fight.Elapsed;
		float wave = time * GooType.WobbleSpeed * Mathf.Tau + enemy.Phase;
		float wobble = GooType.Wobble;
		float width = 1f + wobble * Mathf.Sin(wave);
		float depth = 1f + wobble * Mathf.Sin(wave + 2.1f);
		float height = Flatness * (1f + wobble * 0.5f * Mathf.Sin(wave + 4.2f));

		// It swells for a moment when it absorbs a drone.
		float swell = 1f;
		if (GooType.AbsorbPulseSeconds > 0f && enemy.Pulse > 0f)
			swell += GooType.AbsorbPulse * enemy.Pulse / GooType.AbsorbPulseSeconds;

		Basis shape = new Basis(Vector3.Up, enemy.Yaw) * Basis.FromScale(new Vector3(width, height, depth));
		shape = Reshape(in enemy, shape, time);
		Batch.SetBasis(slot, enemy.Position, 0f, shape.Scaled(Vector3.One * enemy.Scale * swell), color);
	}

	/// <summary>The type's own change to the wobbling blob's shape, such as the basic goo's bulge before a split.</summary>
	protected virtual Basis Reshape(in EnemySwarm.Enemy enemy, Basis shape, float time) => shape;

	/// <summary>Tags of the type's own for the body it declares to pilots (ThreatTags), on top of the shared ones.</summary>
	protected virtual ulong OwnTags => 0;

	/// <summary>A basis that stretches by <paramref name="along"/> in a direction on the plane and by
	/// <paramref name="across"/> at right angles to it, leaving height alone.</summary>
	protected static Basis StretchAlong(Vector2 direction, float along, float across)
	{
		Vector3 u = PlaneMath.ToWorld(direction);
		Vector3 v = new Vector3(-u.Z, 0f, u.X);
		// Each column is where one axis ends up: along * u(u.axis) + across * v(v.axis), height unchanged.
		Vector3 Column(float uPart, float vPart) => u * (along * uPart) + v * (across * vPart);
		return new Basis(Column(u.X, v.X), Vector3.Up, Column(u.Z, v.Z));
	}
}
