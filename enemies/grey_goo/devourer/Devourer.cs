using Godot;
using System.Collections.Generic;

/// <summary>
/// The Devourer, a Grey Goo type. Everything shared by Grey Goo (GreyGoo.cs) applies: it drains and absorbs drones
/// that touch it and grows with each, and hurts the VNP by touch, but never splits. Its own parts:
/// - consuming: it heads for the nearest asteroid or debris within SeekRange and draws mass out of whatever it
///   touches, with motes flowing in, growing by BulkPerMass up to MaxBulk. None of it is fight experience: it
///   competes with the player for material. It never chases the VNP or drones, even when shot or sent in a wave;
/// - debris: a share of the mass it consumed is added to its debris.
/// Its body is declared to pilots like every Grey Goo type's. Its numbers are in devourer.tres.
/// </summary>
public sealed class Devourer : GreyGoo
{
	// How often it looks again for the nearest material, and how often it can gulp, in seconds.
	private const float RetargetSeconds = 0.5f;
	private const float GulpSeconds = 0.7f;

	private sealed class State
	{
		public Vector2 Target;
		public bool HasTarget;
		public float Retarget;
		public float Gulp;
	}

	private readonly DevourerDefinition _type;
	private readonly Dictionary<int, State> _states = new();
	private readonly int[] _material = new int[256];

	public Devourer(EnemySwarm swarm, DevourerDefinition definition) : base(swarm, definition)
	{
		_type = definition;
	}

	public override Mesh Mesh => Placeholders.Mesh("devourer");

	protected override float MaxBulk => _type.MaxBulk;

	public override bool Chases => false;

	public override void Spawned(ref EnemySwarm.Enemy enemy)
	{
		base.Spawned(ref enemy);
		enemy.Consumed = 0f;
	}

	private State StateOf(int id)
	{
		if (!_states.TryGetValue(id, out State state))
			_states[id] = state = new State { Retarget = Rng.Range(0f, RetargetSeconds) };
		return state;
	}

	/// <summary>It heads for the nearest material within SeekRange, or wanders if there is none.</summary>
	public override Vector2? Roam(ref EnemySwarm.Enemy enemy, float dt)
	{
		State state = StateOf(enemy.Id);
		state.Retarget -= dt;
		if (state.Retarget <= 0f)
		{
			state.Retarget = RetargetSeconds;
			Harvestables material = Fight.Harvestables;
			state.HasTarget = false;
			// Close by first, then twice as far each time, so a long range still finds the nearest: a wide search in a
			// rich field could fill the buffer before reaching it.
			for (float range = Mathf.Min(10f, _type.SeekRange); !state.HasTarget; range = Mathf.Min(range * 2f, _type.SeekRange))
			{
				int found = material.CollectNear(enemy.Position, range, _material);
				float best = float.MaxValue;
				for (int c = 0; c < found; c++)
				{
					float distance = material.EdgeDistance(_material[c], enemy.Position);
					if (distance < best)
					{
						best = distance;
						state.Target = material.PositionOf(_material[c]);
						state.HasTarget = true;
					}
				}
				if (range >= _type.SeekRange)
					break;
			}
		}
		if (!state.HasTarget)
			return null;
		Vector2 toward = state.Target - enemy.Position;
		// Slows as it arrives, so it settles over the material rather than overshooting.
		float closeness = Mathf.Clamp(toward.Length() / Mathf.Max(0.5f, enemy.Radius), 0f, 1f);
		return toward.LengthSquared() > 0.0001f ? toward.Normalized() * closeness : Vector2.Zero;
	}

	protected override bool ActOwn(ref EnemySwarm.Enemy enemy, int index, in EnemySwarm.Target target, float dt)
	{
		Consume(ref enemy, dt);
		return false;
	}

	/// <summary>Draws mass out of every piece of material it touches, and grows from it.</summary>
	private void Consume(ref EnemySwarm.Enemy enemy, float dt)
	{
		Harvestables material = Fight.Harvestables;
		int touching = material.CollectNear(enemy.Position, enemy.Radius, _material);
		if (touching == 0)
			return;
		float each = _type.ConsumePerSecond * dt / touching;
		float taken = 0f;
		for (int c = 0; c < touching; c++)
		{
			int item = _material[c];
			taken += material.Consume(item, each);
			Fight.Effects.HarvestFlow(material.PositionOf(item), enemy.Position,
				material.IsAsteroidOf(item) ? Tuning.Asteroids.Color : Tuning.Harvesting.DebrisColor,
				Tuning.Feedback.DroneHarvestParticles, dt, 0.3f);
		}
		enemy.Consumed += taken;
		Grow(ref enemy, taken * _type.BulkPerMass);

		State state = StateOf(enemy.Id);
		state.Gulp -= dt;
		if (taken > 0f && state.Gulp <= 0f)
		{
			state.Gulp = GulpSeconds;
			Fight.Sounds.Play(Sound.DevourerConsume);
		}
	}

	/// <summary>Its own debris and its drones', plus a share of what it consumed.</summary>
	public override float DebrisMass(in EnemySwarm.Enemy enemy) =>
		base.DebrisMass(enemy) + enemy.Consumed * _type.DebrisShareOfConsumed;

	/// <summary>A grown Devourer's debris can be far larger than its own type's.</summary>
	public override float LargestDebrisMass =>
		base.LargestDebrisMass + _type.MaxBulk / Mathf.Max(0.0001f, _type.BulkPerMass) * _type.DebrisShareOfConsumed;

	public override void Died(in EnemySwarm.Enemy enemy)
	{
		_states.Remove(enemy.Id);
		base.Died(enemy);
	}
}
