using Godot;

/// <summary>The fixed point where the fight starts.</summary>
public partial class Gateway : Node3D
{
	// The gateway sits a little below the units so ships pass over it without clipping through.
	private const float VisualHeight = -1.0f;

	// The radius the placeholder model is made at.
	private const float ModelRadius = 6f;

	/// <summary>Position on the gameplay plane.</summary>
	public Vector2 PlanePosition => PlaneMath.ToPlane(GlobalPosition);

	private Node3D _innerRing;

	public override void _Ready()
	{
		Position = PlaneMath.ToWorld(Tuning.Fight.GatewayPosition);
		BuildModel();
	}

	public override void _Process(double delta)
	{
		_innerRing.RotateY((float)delta * 0.5f);
	}

	/// <summary>
	/// Puts the placeholder model in place: a ring with four pylons and a turning inner ring. The model
	/// is made for a radius of <see cref="ModelRadius"/> and scaled to the gateway's radius.
	/// </summary>
	private void BuildModel()
	{
		var model = Placeholders.Model("gateway").Instantiate<Node3D>();
		model.Name = "Model";
		model.Position = new Vector3(0f, VisualHeight, 0f);
		model.Scale = Vector3.One * (Tuning.Fight.GatewayRadius / ModelRadius);
		AddChild(model);
		_innerRing = model.GetNode<Node3D>("InnerRing");
	}
}
