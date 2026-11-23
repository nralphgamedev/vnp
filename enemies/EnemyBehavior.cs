using Arbiter;
using Godot;

/// <summary>
/// How one enemy type behaves. The swarm in EnemySwarm.cs keeps every enemy as plain data and does what
/// they all share: spawning, picking a target, roaming, flocking, moving, and taking damage. The type's
/// behavior then decides how it closes in, how it attacks, and how it looks. There is one behavior per
/// enemy type in the fight. A new kind of enemy is a new subclass in its own folder, beside its
/// definition, like the Drone Weapons in res://drone_weapons/.
/// </summary>
public abstract class EnemyBehavior
{
	protected readonly EnemySwarm Swarm;

	/// <summary>The type's numbers.</summary>
	public readonly EnemyDefinition Definition;

	/// <summary>Draws every enemy of this type.</summary>
	public InstanceBatch Batch;

	/// <summary>This enemy type, as the source of its hits, for fight statistics.</summary>
	public readonly DamageSource Source;

	protected EnemyBehavior(EnemySwarm swarm, EnemyDefinition definition)
	{
		Swarm = swarm;
		Definition = definition;
		Source = new DamageSource(definition.Id, definition.Name, DamageSourceKind.Enemy, definition.Color);
	}

	protected Fight Fight => Swarm.Fight;

	/// <summary>The behavior for an enemy definition's kind, or null if the kind is not known.</summary>
	public static EnemyBehavior Create(EnemySwarm swarm, EnemyDefinition definition) => definition switch
	{
		LancerDefinition lancer => new Lancer(swarm, lancer),
		QuillerDefinition quiller => new Quiller(swarm, quiller),
		SpikerDefinition spiker => new Spiker(swarm, spiker),
		BursterDefinition burster => new Burster(swarm, burster),
		BasicGooDefinition basicGoo => new BasicGoo(swarm, basicGoo),
		BasicEnemyDefinition basic => new BasicEnemy(swarm, basic),
		_ => null,
	};

	/// <summary>The placeholder model drawn for each enemy of this type.</summary>
	public abstract Mesh Mesh { get; }

	/// <summary>The placeholder material it is drawn with. Each enemy brings its own colour.</summary>
	public virtual Material Material => Placeholders.Material("instanced_lit");

	/// <summary>Whether a drone that touches this enemy is absorbed.</summary>
	public virtual bool AbsorbsDrones => false;

	/// <summary>Whether it damages the VNP by touching it, as every Grey Goo type does, so a pilot keeps clear of it.</summary>
	public virtual bool DamagesByTouch => false;

	/// <summary>Sets up a newly spawned enemy: its health, damage and size, at the difficulty of the moment.</summary>
	public abstract void Spawned(ref EnemySwarm.Enemy enemy);

	/// <summary>
	/// Which way an enemy that is chasing wants to go, before flocking, as a direction of length up to 1.
	/// <paramref name="track"/> is its sense of where its target is, and <paramref name="distance"/> how far.
	/// </summary>
	public abstract Vector2 Chase(ref EnemySwarm.Enemy enemy, Vector2 track, float distance);

	/// <summary>
	/// Attacks for one frame, after the enemy has moved. Returns true if it has set the enemy's facing
	/// itself; otherwise the swarm faces it the way it is going.
	/// </summary>
	public abstract bool Act(ref EnemySwarm.Enemy enemy, int index, in EnemySwarm.Target target, float dt);

	/// <summary>Health per second this enemy drains from a drone touching it, for a type that absorbs drones.</summary>
	public virtual float DroneDrain(in EnemySwarm.Enemy enemy) => 0f;

	/// <summary>A drone touching an enemy of a type that absorbs drones has been drained to nothing.</summary>
	public virtual void Absorb(ref EnemySwarm.Enemy enemy)
	{
	}

	/// <summary>Mass of the debris a destroyed enemy leaves.</summary>
	public virtual float DebrisMass(in EnemySwarm.Enemy enemy) =>
		Rng.Range(Definition.DebrisMassMin, Definition.DebrisMassMax);

	/// <summary>The largest debris this type normally leaves, so harvesting knows how big to search.</summary>
	public virtual float LargestDebrisMass => Definition.DebrisMassMax;

	/// <summary>
	/// Declares what one enemy is and how it can hurt, for pilots (Arbiter, docs/dev/PILOT_SPEC.md) and the run
	/// review's maps: its body, tagged with what it does (DeclareBody), and a hazard for each way it can hurt the
	/// VNP or drones, declared from the moment a player could see it coming and no sooner. Required, so a new
	/// enemy type can't be invisible to pilots. Game code, since rival and escort VNPs will need it in exported
	/// games. Read-only: it must not change the enemy or draw random numbers.
	/// </summary>
	public abstract void DeclareThreats(in EnemySwarm.Enemy enemy, Situation situation);

	/// <summary>Adds the enemy's body, tagged with what its type does (ThreatTags), plus any tags of its own.</summary>
	protected void DeclareBody(in EnemySwarm.Enemy enemy, Situation situation, ulong extraTags = 0)
	{
		ulong tags = extraTags | (DamagesByTouch ? ThreatTags.HarmsByTouch : 0) | (AbsorbsDrones ? ThreatTags.DrainsDrones : 0);
		situation.Add(new Body
		{
			Position = enemy.Position.ToArbiter(),
			Velocity = enemy.Velocity.ToArbiter(),
			Radius = enemy.Radius,
			Id = enemy.Id,
			Kind = enemy.Type,
			Tags = tags,
		});
	}

	/// <summary>The effect and sound of an enemy being destroyed.</summary>
	public abstract void Died(in EnemySwarm.Enemy enemy);

	/// <summary>Draws one enemy as copy <paramref name="slot"/> of this type's batch.</summary>
	public abstract void Draw(int slot, in EnemySwarm.Enemy enemy, Color color);

	/// <summary>Before and after each frame's Draw calls, for a type that draws parts of its own beside its body,
	/// such as the Spiker's spikes.</summary>
	public virtual void BeginDraw()
	{
	}

	public virtual void EndDraw()
	{
	}
}
