using Godot;

/// <summary>The fixed point where the fight starts. Placeholder: a ring with four pylons.</summary>
public partial class Gateway : Node3D
{
	// The gateway sits a little below the units so ships pass over it without clipping through.
	private const float VisualHeight = -1.0f;

	/// <summary>Position on the gameplay plane.</summary>
	public Vector2 PlanePosition => PlaneMath.ToPlane(GlobalPosition);

	private Node3D _innerRing;

	public override void _Ready()
	{
		Position = PlaneMath.ToWorld(Tuning.GatewayPosition);
		BuildModel();
	}

	public override void _Process(double delta)
	{
		_innerRing.RotateY((float)delta * 0.5f);
	}

	private void BuildModel()
	{
		float radius = Tuning.GatewayRadius;
		StandardMaterial3D solid = Placeholder.Material(Tuning.GatewayColor, glow: 0.3f);
		StandardMaterial3D faint = Placeholder.Material(Tuning.GatewayColor, glow: 0.15f, alpha: 0.12f);

		var model = new Node3D { Name = "Model", Position = new Vector3(0f, VisualHeight, 0f) };
		AddChild(model);

		// Outer ring marks the edge of the gateway.
		Placeholder.AddShape(model, new TorusMesh { InnerRadius = radius - 0.4f, OuterRadius = radius }, solid);

		// Faint disc fills the area inside the ring.
		Placeholder.AddShape(model, new CylinderMesh
		{
			TopRadius = radius - 0.4f,
			BottomRadius = radius - 0.4f,
			Height = 0.05f,
		}, faint);

		// Four pylons give the gateway height, so it reads differently from flat rings.
		var pylon = new BoxMesh { Size = new Vector3(0.9f, 2.6f, 0.9f) };
		for (int i = 0; i < 4; i++)
		{
			float angle = Mathf.Pi / 4f + i * Mathf.Pi / 2f;
			var offset = new Vector3(Mathf.Cos(angle) * radius, 0.8f, Mathf.Sin(angle) * radius);
			Placeholder.AddShape(model, pylon, solid, offset, new Vector3(0f, 45f, 0f));
		}

		// Slowly turning inner ring with three markers, so the gateway looks active.
		_innerRing = new Node3D { Name = "InnerRing" };
		model.AddChild(_innerRing);
		float innerRadius = radius * 0.5f;
		Placeholder.AddShape(_innerRing, new TorusMesh { InnerRadius = innerRadius - 0.2f, OuterRadius = innerRadius }, solid);
		var marker = new BoxMesh { Size = new Vector3(0.6f, 0.6f, 0.6f) };
		for (int i = 0; i < 3; i++)
		{
			float angle = i * Mathf.Tau / 3f;
			Placeholder.AddShape(_innerRing, marker, solid,
				new Vector3(Mathf.Cos(angle) * innerRadius, 0f, Mathf.Sin(angle) * innerRadius));
		}
	}
}
