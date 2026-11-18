using Godot;
using System;

/// <summary>
/// Many short-lived copies of one shape, such as sparks or pieces of a burst. Each flies, spins, slows,
/// shrinks and changes colour over its life, then disappears. Plain data in one array, drawn in one batch,
/// so thousands stay cheap. Glowing particles fade by darkening to black; solid ones by shrinking.
/// </summary>
public sealed class ParticleBatch
{
	private struct Particle
	{
		public Vector2 Position;
		public Vector2 Velocity;
		public float Height;
		public float Age;
		public float Life;
		public float StartSize;
		public float EndSize;
		public float Yaw;
		public float Spin;     // Radians per second.
		public float Drag;     // Fraction of speed lost per second.
		public Color StartColor;
		public Color EndColor;
	}

	private Particle[] _particles = new Particle[256];
	private readonly InstanceBatch _batch;

	public int Count { get; private set; }

	public ParticleBatch(Node3D parent, string name, Mesh mesh, Material material)
	{
		_batch = new InstanceBatch(parent, name, mesh, material);
	}

	/// <summary>Adds one particle, unless <paramref name="room"/> says the effects are full.</summary>
	public void Emit(bool room, Vector2 position, Vector2 velocity, float life, float startSize, float endSize,
		Color startColor, Color endColor, float height, float yaw = 0f, float spin = 0f, float drag = 0f)
	{
		if (!room || life <= 0f)
			return;
		if (Count == _particles.Length)
			Array.Resize(ref _particles, _particles.Length * 2);

		_particles[Count++] = new Particle
		{
			Position = position,
			Velocity = velocity,
			Height = height,
			Life = life,
			StartSize = startSize,
			EndSize = endSize,
			Yaw = yaw,
			Spin = spin,
			Drag = drag,
			StartColor = startColor,
			EndColor = endColor,
		};
	}

	public void Update(float dt)
	{
		for (int i = Count - 1; i >= 0; i--)
		{
			ref Particle p = ref _particles[i];
			p.Age += dt;
			if (p.Age >= p.Life)
			{
				_particles[i] = _particles[--Count]; // Fill the gap with the last one.
				continue;
			}
			p.Position += p.Velocity * dt;
			p.Velocity *= Mathf.Max(0f, 1f - p.Drag * dt);
			p.Yaw += p.Spin * dt;
		}
	}

	public void Draw()
	{
		_batch.Begin(Count);
		for (int i = 0; i < Count; i++)
		{
			ref Particle p = ref _particles[i];
			float t = p.Age / p.Life;
			_batch.Set(i, p.Position, p.Height, p.Yaw, Mathf.Lerp(p.StartSize, p.EndSize, t),
				p.StartColor.Lerp(p.EndColor, t));
		}
		_batch.Commit(Count);
	}
}
