using Godot;

/// <summary>
/// Follows a target from high above the plane. Reads its settings from Tuning every frame,
/// so switching between perspective and orthographic takes effect straight away.
/// </summary>
public partial class CameraRig : Camera3D
{
	/// <summary>What the camera follows.</summary>
	public Node3D Target;

	/// <summary>The point on the plane at the center of the screen.</summary>
	public Vector3 Focus { get; private set; }

	public override void _Ready()
	{
		// Run after the VNP has moved this frame, so the camera never trails a frame behind.
		ProcessPriority = 10;
		Current = true;
	}

	/// <summary>Jumps straight to the target, skipping any follow lag.</summary>
	public void SnapToTarget()
	{
		if (Target == null)
			return;
		Focus = PlaneOnly(Target.GlobalPosition);
		Apply();
	}

	public override void _Process(double delta)
	{
		if (Target == null)
			return;

		Vector3 wanted = PlaneOnly(Target.GlobalPosition);
		if (Tuning.CameraFollowLag > 0f)
			Focus = Focus.Lerp(wanted, 1f - Mathf.Exp(-(float)delta / Tuning.CameraFollowLag));
		else
			Focus = wanted;

		Apply();
	}

	private void Apply()
	{
		float tilt = Mathf.DegToRad(Tuning.CameraTilt);
		float height = Tuning.CameraHeight;
		float distance = height / Mathf.Cos(tilt);

		// Sit above the focus and slightly toward the bottom of the screen, looking back at it.
		// Pitching by -(90 - tilt) keeps the top of the screen pointing along -Z.
		GlobalPosition = Focus + new Vector3(0f, height, height * Mathf.Tan(tilt));
		RotationDegrees = new Vector3(-(90f - Tuning.CameraTilt), 0f, 0f);

		Near = 1f;
		Far = distance * 4f;

		if (Tuning.CameraOrthographic)
		{
			// Match the height the perspective camera shows at the focus,
			// so switching projection keeps the same framing.
			Projection = ProjectionType.Orthogonal;
			Size = 2f * distance * Mathf.Tan(Mathf.DegToRad(Tuning.CameraFov) * 0.5f);
		}
		else
		{
			Projection = ProjectionType.Perspective;
			Fov = Tuning.CameraFov;
		}
	}

	private static Vector3 PlaneOnly(Vector3 world) => new(world.X, 0f, world.Z);
}
