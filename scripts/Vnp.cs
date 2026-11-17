using Godot;

/// <summary>The player's ship. Moves on the plane from WASD or the left stick.</summary>
public partial class Vnp : Node3D
{
	/// <summary>Position on the gameplay plane.</summary>
	public Vector2 PlanePosition { get; private set; }

	/// <summary>Current velocity on the gameplay plane, in units per second.</summary>
	public Vector2 Velocity { get; private set; }

	private Node3D _model;

	public override void _Ready()
	{
		PlanePosition = PlaneMath.ToPlane(Position);
		BuildModel();
	}

	/// <summary>Places the VNP at a point on the plane and stops it.</summary>
	public void PlaceAt(Vector2 planePosition)
	{
		PlanePosition = planePosition;
		Velocity = Vector2.Zero;
		Position = PlaneMath.ToWorld(PlanePosition);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		// GetVector never returns more than length 1, so diagonals are not faster,
		// and a partly tilted stick gives a partial speed.
		Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down", Tuning.StickDeadzone);
		Vector2 wantedVelocity = input * Tuning.VnpMoveSpeed;
		float rate = input == Vector2.Zero ? Tuning.VnpDeceleration : Tuning.VnpAcceleration;

		Velocity = Velocity.MoveToward(wantedVelocity, rate * dt);
		PlanePosition += Velocity * dt;
		Position = PlaneMath.ToWorld(PlanePosition);

		// Turn the model toward the direction of travel. Holds its facing when nearly stopped.
		if (Velocity.LengthSquared() > 0.25f)
		{
			float wantedYaw = PlaneMath.YawFacing(Velocity);
			float step = Mathf.DegToRad(Tuning.VnpTurnSpeed) * dt;
			_model.Rotation = new Vector3(0f, Mathf.RotateToward(_model.Rotation.Y, wantedYaw, step), 0f);
		}
	}

	/// <summary>Placeholder: an arrowhead hull with two side pods and a glowing core.</summary>
	private void BuildModel()
	{
		_model = new Node3D { Name = "Model", Scale = Vector3.One * Tuning.VnpSize };
		AddChild(_model);

		StandardMaterial3D hull = Placeholder.Material(Tuning.VnpHullColor);
		StandardMaterial3D core = Placeholder.Material(Tuning.VnpCoreColor, glow: 0.8f);

		// A prism stands upright by default, so tip it forward to make the point face -Z.
		Placeholder.AddShape(_model, new PrismMesh { Size = new Vector3(2.2f, 3.0f, 0.8f) }, hull,
			rotationDegrees: new Vector3(-90f, 0f, 0f));

		var pod = new BoxMesh { Size = new Vector3(0.5f, 0.6f, 1.5f) };
		Placeholder.AddShape(_model, pod, hull, new Vector3(-1.25f, 0f, 1.0f));
		Placeholder.AddShape(_model, pod, hull, new Vector3(1.25f, 0f, 1.0f));

		Placeholder.AddShape(_model, new SphereMesh { Radius = 0.45f, Height = 0.9f }, core,
			new Vector3(0f, 0.45f, 0.55f));
	}
}
