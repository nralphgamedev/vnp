using Godot;
using System;

/// <summary>
/// Draws many copies of one shape in a single draw call, each with its own position, facing,
/// size and colour. This is what lets hundreds or thousands of units stay cheap to draw.
/// Each frame: call Begin, then Set for every copy, then Commit.
/// </summary>
public sealed class InstanceBatch
{
	// Godot's layout per copy: a 3x4 transform (12 numbers) followed by a colour (4 numbers).
	private const int Stride = 16;

	private readonly MultiMesh _multiMesh;
	private float[] _buffer;
	private int _capacity;

	public InstanceBatch(Node3D parent, string name, Mesh mesh, Material material, int capacity = 256)
	{
		_multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseColors = true,
			Mesh = mesh,
		};
		parent.AddChild(new MultiMeshInstance3D
		{
			Name = name,
			Multimesh = _multiMesh,
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		});
		Resize(capacity);
	}

	/// <summary>Makes room for <paramref name="count"/> copies.</summary>
	public void Begin(int count)
	{
		if (count > _capacity)
			Resize(Math.Max(count, _capacity * 2));
	}

	/// <summary>Places copy number <paramref name="index"/> on the plane.</summary>
	public void Set(int index, Vector2 planePosition, float height, float yaw, float scale, Color color)
	{
		int o = index * Stride;
		float cos = Mathf.Cos(yaw) * scale;
		float sin = Mathf.Sin(yaw) * scale;

		_buffer[o + 0] = cos;
		_buffer[o + 1] = 0f;
		_buffer[o + 2] = sin;
		_buffer[o + 3] = planePosition.X;

		_buffer[o + 4] = 0f;
		_buffer[o + 5] = scale;
		_buffer[o + 6] = 0f;
		_buffer[o + 7] = height;

		_buffer[o + 8] = -sin;
		_buffer[o + 9] = 0f;
		_buffer[o + 10] = cos;
		_buffer[o + 11] = planePosition.Y;

		_buffer[o + 12] = color.R;
		_buffer[o + 13] = color.G;
		_buffer[o + 14] = color.B;
		_buffer[o + 15] = color.A;
	}

	/// <summary>
	/// Places copy number <paramref name="index"/> as a bar stretched between two points on the plane.
	/// Use it with a 1 by 1 by 1 box: the box is scaled to <paramref name="width"/> across,
	/// <paramref name="thickness"/> high, and the length of the gap.
	/// </summary>
	public void SetBeam(int index, Vector2 from, Vector2 to, float height, float width, float thickness, Color color)
	{
		Vector2 along = to - from;
		float length = along.Length();
		float yaw = length > 0.0001f ? PlaneMath.YawFacing(along) : 0f;
		float cos = Mathf.Cos(yaw);
		float sin = Mathf.Sin(yaw);
		Vector2 middle = (from + to) * 0.5f;

		int o = index * Stride;
		_buffer[o + 0] = cos * width;
		_buffer[o + 1] = 0f;
		_buffer[o + 2] = sin * length;
		_buffer[o + 3] = middle.X;

		_buffer[o + 4] = 0f;
		_buffer[o + 5] = thickness;
		_buffer[o + 6] = 0f;
		_buffer[o + 7] = height;

		_buffer[o + 8] = -sin * width;
		_buffer[o + 9] = 0f;
		_buffer[o + 10] = cos * length;
		_buffer[o + 11] = middle.Y;

		_buffer[o + 12] = color.R;
		_buffer[o + 13] = color.G;
		_buffer[o + 14] = color.B;
		_buffer[o + 15] = color.A;
	}

	/// <summary>Sends the copies to the renderer and shows the first <paramref name="count"/> of them.</summary>
	public void Commit(int count)
	{
		_multiMesh.Buffer = _buffer;
		_multiMesh.VisibleInstanceCount = count;
	}

	private void Resize(int capacity)
	{
		_capacity = capacity;
		_buffer = new float[capacity * Stride];
		_multiMesh.InstanceCount = capacity;
		_multiMesh.VisibleInstanceCount = 0;
	}
}
