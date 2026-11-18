using Godot;
using System;

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
		ProcessPriority = UpdateOrder.Camera;
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
		if (Tuning.Camera.FollowLag > 0f)
			Focus = Focus.Lerp(wanted, 1f - Mathf.Exp(-(float)delta / Tuning.Camera.FollowLag));
		else
			Focus = wanted;

		Apply();
	}

	private void Apply()
	{
		float tilt = Mathf.DegToRad(Tuning.Camera.Tilt);
		float height = Tuning.Camera.Height;
		float distance = height / Mathf.Cos(tilt);

		// Sit above the focus and slightly toward the bottom of the screen, looking back at it.
		// Pitching by -(90 - tilt) keeps the top of the screen pointing along -Z.
		GlobalPosition = Focus + new Vector3(0f, height, height * Mathf.Tan(tilt));
		RotationDegrees = new Vector3(-(90f - Tuning.Camera.Tilt), 0f, 0f);

		Near = 1f;
		Far = distance * 4f;

		if (Tuning.Camera.Orthographic)
		{
			// Match the height the perspective camera shows at the focus,
			// so switching projection keeps the same framing.
			Projection = ProjectionType.Orthogonal;
			Size = 2f * distance * Mathf.Tan(Mathf.DegToRad(Tuning.Camera.Fov) * 0.5f);
		}
		else
		{
			Projection = ProjectionType.Perspective;
			Fov = Tuning.Camera.Fov;
		}
	}

	/// <summary>
	/// How far to go from a point on the plane, in a direction, to be out of sight with at least
	/// <paramref name="margin"/> to spare from every edge of the screen. With the tilt the visible
	/// area is wider at the top than the bottom, so this differs by direction. Uses the camera as it
	/// was last placed.
	/// </summary>
	public float DistanceOutOfView(Vector2 from, Vector2 direction, float margin)
	{
		// Where the four screen corners land on the plane.
		Vector2 screen = GetViewport().GetVisibleRect().Size;
		Span<Vector2> corners = stackalloc Vector2[4];
		corners[0] = OnPlane(Vector2.Zero);
		corners[1] = OnPlane(new Vector2(screen.X, 0f));
		corners[2] = OnPlane(screen);
		corners[3] = OnPlane(new Vector2(0f, screen.Y));
		Vector2 center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;

		// Push every edge of the visible area out by the margin, and find where the ray leaves the
		// grown area. Measuring the margin square to each edge, rather than along the ray, keeps
		// points near a corner from landing just past the edge beside it.
		float leave = float.MaxValue;
		for (int i = 0; i < 4; i++)
		{
			Vector2 a = corners[i];
			Vector2 edge = corners[(i + 1) % 4] - a;
			Vector2 outward = new Vector2(edge.Y, -edge.X).Normalized();
			if (outward.Dot(center - a) > 0f)
				outward = -outward;

			float heading = direction.Dot(outward);
			if (heading <= 0.000001f)
				continue; // Moving along or away from this edge never crosses it.
			float t = ((a - from).Dot(outward) + margin) / heading;
			leave = Mathf.Min(leave, t);
		}
		return Mathf.Max(0f, leave);
	}

	/// <summary>Where a point on the screen lands on the gameplay plane.</summary>
	private Vector2 OnPlane(Vector2 screenPoint)
	{
		Vector3 origin = ProjectRayOrigin(screenPoint);
		Vector3 normal = ProjectRayNormal(screenPoint);
		Vector3 hit = origin + normal * (-origin.Y / normal.Y);
		return new Vector2(hit.X, hit.Z);
	}

	private static Vector3 PlaneOnly(Vector3 world) => new(world.X, 0f, world.Z);
}
