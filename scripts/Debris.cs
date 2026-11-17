using Godot;
using System;

/// <summary>
/// What destroyed enemies leave behind. Debris never disappears. Each piece has a mass,
/// which harvesting will use in a later stage.
/// </summary>
public partial class Debris : Node3D
{
	private struct Piece
	{
		public Vector2 Position;
		public float Mass;
		public float Yaw;
	}

	// Drawn a little below the units, so ships pass over it.
	private const float VisualHeight = -0.4f;

	// Placeholder silhouette: an uneven chunk.
	private static readonly Vector2[] Outline =
	{
		new(-0.7f, -0.5f), new(0.1f, -0.9f), new(0.9f, -0.2f), new(0.5f, 0.8f), new(-0.4f, 0.6f), new(-0.95f, 0.1f),
	};

	/// <summary>Pieces of debris in the arena.</summary>
	public int Count { get; private set; }

	private Piece[] _pieces = new Piece[256];
	private InstanceBatch _batch;
	private bool _changed;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Debris;
		_batch = new InstanceBatch(this, "DebrisShapes", Placeholder.ExtrudedShape(Outline, 0.5f), Placeholder.InstanceMaterial());
	}

	/// <summary>Leaves a piece of debris at a point on the plane.</summary>
	public void Add(Vector2 position, float mass)
	{
		if (Count == _pieces.Length)
			Array.Resize(ref _pieces, _pieces.Length * 2);

		_pieces[Count++] = new Piece { Position = position, Mass = mass, Yaw = Rng.Range(0f, Mathf.Tau) };
		_changed = true;
	}

	public override void _Process(double delta)
	{
		// Debris does not move, so it only needs sending to the renderer when a piece is added.
		if (!_changed)
			return;
		_changed = false;

		_batch.Begin(Count);
		for (int i = 0; i < Count; i++)
		{
			// Heavier pieces are larger. The square root keeps a piece's area in step with its mass.
			float scale = Tuning.DebrisSize * Mathf.Sqrt(_pieces[i].Mass);
			_batch.Set(i, _pieces[i].Position, VisualHeight, _pieces[i].Yaw, scale, Tuning.DebrisColor);
		}
		_batch.Commit(Count);
	}
}
