using Godot;

/// <summary>Root of the fight scene. Connects the pieces to each other and starts the fight at the gateway.</summary>
public partial class Fight : Node3D
{
	private Vnp _vnp;
	private Gateway _gateway;
	private CameraRig _camera;
	private Label _debugLabel;
	private double _debugRefresh;

	public override void _Ready()
	{
		_vnp = GetNode<Vnp>("Vnp");
		_gateway = GetNode<Gateway>("Gateway");
		_camera = GetNode<CameraRig>("Camera");
		_debugLabel = GetNode<Label>("Hud/DebugLabel");

		var ground = GetNode<GroundGrid>("Ground");
		var indicator = GetNode<GatewayIndicator>("Hud/GatewayIndicator");

		// The fight starts at the gateway.
		_vnp.PlaceAt(_gateway.PlanePosition);

		_camera.Target = _vnp;
		_camera.SnapToTarget();
		ground.Camera = _camera;
		indicator.Camera = _camera;
		indicator.Gateway = _gateway;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Playtest shortcut: switch between the perspective and orthographic cameras.
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F1 })
			Tuning.CameraOrthographic = !Tuning.CameraOrthographic;
	}

	public override void _Process(double delta)
	{
		_debugRefresh -= delta;
		if (_debugRefresh > 0)
			return;
		_debugRefresh = 0.25;

		Vector2 position = _vnp.PlanePosition;
		float gatewayDistance = position.DistanceTo(_gateway.PlanePosition);
		string cameraMode = Tuning.CameraOrthographic ? "orthographic" : "perspective";
		_debugLabel.Text =
			$"{Engine.GetFramesPerSecond():0} fps    " +
			$"VNP at ({position.X:0}, {position.Y:0})    " +
			$"speed {_vnp.Velocity.Length():0.0}    " +
			$"gateway {gatewayDistance:0} away    " +
			$"camera: {cameraMode} (F1 switches)";
	}
}
