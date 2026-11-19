using Godot;
using System;

/// <summary>
/// Every visual effect in the fight: sparks, the pieces things burst into, spreading rings, trails and
/// harvest particles. Gameplay code asks for a named effect, such as EnemyDeath, at a point on the plane;
/// how each one looks is decided here, with the amounts in feedback.tres.
///
/// Effects use their own random numbers, so turning them up or down never changes how a fight plays.
/// </summary>
public partial class Effects : Node3D
{
	// Sparks and rings are drawn glowing: their colour adds to what is behind them, so fading to black
	// fades them out.
	private static readonly Color Gone = Colors.Black;

	// Heights above the plane, so effects show over the units and beams.
	private const float SparkHeight = 0.8f;
	private const float ChunkHeight = 0.5f;
	private const float RingHeight = 0.15f;
	private const float FlowHeight = 0.4f;

	private readonly Random _random = new();
	private ParticleBatch _sparks;
	private ParticleBatch _chunks;
	private ParticleBatch _rings;
	// Harvest particles owed but not yet sent. Shared by every beam, which keeps the total rate right.
	private float _flowCarry;

	private static FeedbackTuning Feedback => Tuning.Feedback;

	/// <summary>Effect particles alive right now, across every kind.</summary>
	public int Count => _sparks.Count + _chunks.Count + _rings.Count;

	private bool Room => Count < Feedback.MaxParticles;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Effects;
		_sparks = new ParticleBatch(this, "Sparks", Placeholders.Mesh("unit_box"), Placeholders.Material("instanced_glow"));
		_chunks = new ParticleBatch(this, "Chunks", Placeholders.Mesh("fragment"), Placeholders.Material("instanced_lit"));
		_rings = new ParticleBatch(this, "Rings", Placeholders.Mesh("ring"), Placeholders.Material("instanced_glow"));
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_sparks.Update(dt);
		_chunks.Update(dt);
		_rings.Update(dt);
		_sparks.Draw();
		_chunks.Draw();
		_rings.Draw();
	}

	// ───────────── Weapons ─────────────

	/// <summary>A small puff of sparks where a shot leaves, thrown forward.</summary>
	public void MuzzleFlash(Vector2 at, Vector2 direction, Color color, float scale = 1f)
	{
		Sparks(at, color, Mathf.RoundToInt(Feedback.MuzzleSparks * scale), 0.6f, 0.8f * scale, direction, 0.5f);
	}

	/// <summary>Sparks where a shot, beam or shard hits. A larger scale adds more and bigger sparks and a small ring.</summary>
	public void Impact(Vector2 at, Color color, float scale = 1f)
	{
		Sparks(at, color, Mathf.Max(1, Mathf.RoundToInt(Feedback.ImpactSparks * scale)), 1f, scale);
		if (scale >= 1f)
			Ring(at, color, 0.2f, 1.1f * scale, 0.15f);
	}

	/// <summary>A wide flash of sparks thrown across the cone a scatter shot fires into.</summary>
	public void ScatterFlash(Vector2 at, Vector2 direction, Color color, float coneRadians, float scale = 1f)
	{
		Sparks(at, color, Mathf.RoundToInt(Feedback.MuzzleSparks * 2f * scale), 0.9f, 0.8f * scale, direction,
			coneRadians * 0.5f);
	}

	/// <summary>A puff of smoke left behind a missile. It stays where it was left and fades.</summary>
	public void MissileTrail(Vector2 at, Color color, float size)
	{
		_sparks.Emit(Room, at + RandomInCircle(0.1f), RandomDirection() * 0.6f, Feedback.SparkTime * 1.4f,
			size * Feedback.EffectSize, 0f, color.Lerp(Colors.White, 0.4f) * 0.7f, Gone, SparkHeight * 0.6f);
	}

	/// <summary>A missile hitting: a burst of sparks, a hot core and a ring.</summary>
	public void Explosion(Vector2 at, Color color, float scale = 1f)
	{
		Sparks(at, color, Mathf.RoundToInt(Feedback.ImpactSparks * 2f * scale), 1.2f, 1.3f * scale);
		Sparks(at, new Color(1f, 0.85f, 0.5f), Mathf.RoundToInt(Feedback.ImpactSparks * scale), 0.6f, 1.6f * scale);
		Ring(at, color.Lerp(Colors.White, 0.4f), 0.3f, 2.4f * scale, 0.25f);
	}

	/// <summary>
	/// A mine going off: an explosion, and a ring that spreads to exactly the blast radius, so the player
	/// can see how far it reached whatever the effect size setting.
	/// </summary>
	public void Blast(Vector2 at, Color color, float radius, float scale = 1f)
	{
		Explosion(at, color, scale);
		_rings.Emit(Room, at, Vector2.Zero, 0.3f, 0.3f, radius, color.Lerp(Colors.White, 0.3f), Gone, RingHeight);
	}

	/// <summary>A missile running out without hitting anything: a small, dim puff.</summary>
	public void Fizzle(Vector2 at, Color color)
	{
		Sparks(at, color.Darkened(0.4f), 3, 0.4f, 0.7f);
	}

	// ───────────── Deaths and building ─────────────

	/// <summary>A destroyed enemy bursts apart: pieces in its colour, hot sparks, and a flash ring.</summary>
	public void EnemyDeath(Vector2 at, Color color)
	{
		Chunks(at, color, Feedback.EnemyDeathChunks, 9f, 0.9f);
		Sparks(at, new Color(1f, 0.8f, 0.45f), Feedback.EnemyDeathSparks, 1.3f, 1.2f);
		Ring(at, color.Lerp(Colors.White, 0.5f), 0.5f, 3.2f, 0.3f);
	}

	/// <summary>A destroyed drone pops: a quick burst of sparks and a small white ring.</summary>
	public void DronePop(Vector2 at, Color color)
	{
		Sparks(at, color, Feedback.DronePopSparks, 0.9f, 0.8f);
		Ring(at, Colors.White, 0.3f, 1.8f, 0.2f);
	}

	/// <summary>A brief flash where a temporary drone is built.</summary>
	public void DroneBuilt(Vector2 at, Color color)
	{
		Ring(at, Colors.White.Lerp(color, 0.3f), 0.2f, 2.2f, 0.3f);
		Sparks(at, Colors.White, 4, 0.5f, 0.6f);
	}

	/// <summary>An asteroid or piece of debris that is used up pops into pieces.</summary>
	public void MaterialPop(Vector2 at, Color color, Color glow)
	{
		Chunks(at, color, Feedback.MaterialPopChunks, 6f, 0.6f);
		Ring(at, glow, 0.3f, 2.4f, 0.25f);
	}

	// ───────────── Harvesting ─────────────

	/// <summary>
	/// Particles flowing along a harvest beam, from the material to whatever is harvesting it.
	/// <paramref name="perSecond"/> is how many to send; call it every frame the beam works.
	/// </summary>
	public void HarvestFlow(Vector2 from, Vector2 to, Color color, float perSecond, float dt, float size = 0.22f,
		float height = FlowHeight)
	{
		_flowCarry += perSecond * dt;
		Vector2 along = to - from;
		float distance = along.Length();
		if (distance < 0.1f)
			return;
		Vector2 direction = along / distance;
		float speed = Mathf.Max(1f, Feedback.HarvestParticleSpeed);

		while (_flowCarry >= 1f)
		{
			_flowCarry -= 1f;
			Vector2 start = from + RandomInCircle(0.5f);
			_sparks.Emit(Room, start, direction * speed, distance / speed, size, size * 0.4f,
				color.Lerp(Colors.White, 0.3f), color, height);
		}
	}

	// ───────────── Building blocks ─────────────

	private void Sparks(Vector2 at, Color color, int count, float speedScale, float sizeScale,
		Vector2 direction = default, float spread = Mathf.Pi)
	{
		Color hot = color.Lerp(Colors.White, 0.5f);
		float scale = Feedback.EffectSize;
		for (int i = 0; i < count; i++)
		{
			Vector2 heading = direction == Vector2.Zero
				? RandomDirection()
				: direction.Normalized().Rotated(Range(-spread, spread));
			float speed = Feedback.SparkSpeed * speedScale * scale * Range(0.5f, 1f);
			float life = Feedback.SparkTime * Range(0.7f, 1.2f);
			_sparks.Emit(Room, at, heading * speed, life, 0.32f * sizeScale * scale, 0.04f, hot, Gone, SparkHeight,
				Range(0f, Mathf.Tau), Range(-10f, 10f), 4f);
		}
	}

	private void Chunks(Vector2 at, Color color, int count, float speed, float size)
	{
		speed *= Feedback.EffectSize;
		size *= Feedback.EffectSize;
		for (int i = 0; i < count; i++)
		{
			_chunks.Emit(Room, at + RandomInCircle(0.4f), RandomDirection() * speed * Range(0.5f, 1f),
				Range(0.45f, 0.7f), size * Range(0.6f, 1.1f), 0f, color, color.Darkened(0.7f), ChunkHeight,
				Range(0f, Mathf.Tau), Range(-9f, 9f), 2.5f);
		}
	}

	/// <summary>A flat ring that spreads from one radius to another and fades.</summary>
	private void Ring(Vector2 at, Color color, float startRadius, float endRadius, float life)
	{
		float scale = Feedback.EffectSize;
		_rings.Emit(Room, at, Vector2.Zero, life, startRadius * scale, endRadius * scale, color, Gone, RingHeight);
	}

	private float Range(float min, float max) => min + (max - min) * _random.NextSingle();

	private Vector2 RandomDirection()
	{
		float angle = _random.NextSingle() * Mathf.Tau;
		return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
	}

	private Vector2 RandomInCircle(float radius) => RandomDirection() * radius * Mathf.Sqrt(_random.NextSingle());
}
