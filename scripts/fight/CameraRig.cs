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

	// How much shake is left, from 0 to 1. It dies away over ShakeTime.
	private float _shake;
	private readonly Random _shakeRandom = new();

	public override void _Ready()
	{
		// Run after the VNP has moved this frame, so the camera never trails a frame behind.
		ProcessPriority = UpdateOrder.Camera;
		Current = true;
	}

	/// <summary>
	/// Shakes the screen. <paramref name="amount"/> is from 0 to 1 of the strongest shake in feedback.tres.
	/// A weaker shake does not cut short a stronger one already going.
	/// </summary>
	public void Shake(float amount)
	{
		if (!Tuning.Feedback.ScreenShake)
			return;
		_shake = Mathf.Max(_shake, Mathf.Clamp(amount, 0f, 1f));
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

		float shakeTime = Tuning.Feedback.ShakeTime;
		_shake = shakeTime > 0f ? Mathf.Max(0f, _shake - (float)delta / shakeTime) : 0f;

		Apply();
	}

	private void Apply()
	{
		float tilt = Mathf.DegToRad(Tuning.Camera.Tilt);
		float height = Tuning.Camera.Height;
		float distance = height / Mathf.Cos(tilt);

		// Sit above the focus and slightly toward the bottom of the screen, looking back at it.
		// Pitching by -(90 - tilt) keeps the top of the screen pointing along -Z.
		GlobalPosition = Focus + new Vector3(0f, height, height * Mathf.Tan(tilt)) + ShakeOffset();
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

	/// <summary>A random nudge sideways on the plane. Squaring the shake makes it die away smoothly.</summary>
	private Vector3 ShakeOffset()
	{
		if (_shake <= 0f)
			return Vector3.Zero;
		float reach = Tuning.Feedback.ShakeStrength * _shake * _shake;
		return new Vector3(_shakeRandom.NextSingle() * 2f - 1f, 0f, _shakeRandom.NextSingle() * 2f - 1f) * reach;
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
