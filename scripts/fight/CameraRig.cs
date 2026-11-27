using Godot;
using System;

/// <summary>
/// Follows a target from high above the plane. Reads its settings from Tuning every frame,
/// so switching between perspective and orthographic takes effect straight away.
///
/// It works out its own height, from how much of the arena the screen shows (ViewHeight) and its lens (Fov), so
/// the lens never zooms. The Camera node's own fov in the fight scene is overwritten every frame and does
/// nothing: set the lens in res://tuning/camera.tres.
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
	// Where the screen's four corners land on the plane, from the camera's steady position, before any shake.
	// Gameplay measures from these, so the shake, which uses its own randomness, never changes where enemies
	// spawn or what a pilot sees: a fight then repeats exactly from its seed.
	private readonly Vector2[] _steadyCorners = new Vector2[4];
	private bool _cornersReady;

	// The top of the screen looks at most this far from straight down, so it stays well below the horizon, where
	// the screen's edge still lands on the plane and "just off screen" exists.
	private const float MaxTopAngle = 70f;
	private const float MaxTilt = 60f;

	public override void _Ready()
	{
		// Run after the VNP has moved this frame, so the camera never trails a frame behind.
		ProcessPriority = UpdateOrder.Camera;
		Current = true;
		ReportUnsafeSettings();
	}

	/// <summary>The tilt in use, in degrees: the tuning's, kept between 0 and MaxTilt.</summary>
	private static float SafeTilt => Mathf.Clamp(Tuning.Camera.Tilt, 0f, MaxTilt);

	/// <summary>The lens in use, in degrees: the tuning's, kept between 1 and the widest that keeps the top of
	/// the screen MaxTopAngle from straight down.</summary>
	private static float SafeFov => Mathf.Clamp(Tuning.Camera.Fov, 1f, 2f * (MaxTopAngle - SafeTilt));

	/// <summary>How far the camera is from the point it looks at, for its lens to show ViewHeight there.</summary>
	private static float ViewDistance => Tuning.Camera.ViewHeight * 0.5f / Mathf.Tan(Mathf.DegToRad(SafeFov) * 0.5f);

	/// <summary>How high above the plane the camera sits, worked out from the zoom, the lens and the tilt.</summary>
	public static float Height => ViewDistance * Mathf.Cos(Mathf.DegToRad(SafeTilt));

	/// <summary>Reports, once, settings the camera can't use as they are. Printing is slow, so never per frame.</summary>
	private static void ReportUnsafeSettings()
	{
		CameraTuning camera = Tuning.Camera;
		if (camera.ViewHeight <= 0f)
			GD.PushError($"camera.tres: ViewHeight {camera.ViewHeight} must be above 0.");
		if (camera.Tilt != SafeTilt)
			GD.PushError($"camera.tres: Tilt {camera.Tilt} is outside 0 to {MaxTilt}; using {SafeTilt}.");
		if (camera.Fov != SafeFov)
			GD.PushError($"camera.tres: Fov {camera.Fov} is outside 1 to {2f * (MaxTopAngle - SafeTilt)} at a tilt of "
				+ $"{SafeTilt}, past which the top of the screen nears the horizon; using {SafeFov}.");
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
		float tilt = Mathf.DegToRad(SafeTilt);
		float distance = ViewDistance;
		float height = Height;

		// Sit above the focus and slightly toward the bottom of the screen, looking back at it.
		// Pitching by -(90 - tilt) keeps the top of the screen pointing along -Z.
		Vector3 steady = Focus + new Vector3(0f, height, height * Mathf.Tan(tilt));
		GlobalPosition = steady;
		RotationDegrees = new Vector3(-(90f - SafeTilt), 0f, 0f);

		Near = 1f;
		Far = distance * 4f;

		if (Tuning.Camera.Orthographic)
		{
			// The same height as the perspective camera shows at the focus, so switching projection keeps the framing.
			Projection = ProjectionType.Orthogonal;
			Size = Tuning.Camera.ViewHeight;
		}
		else
		{
			Projection = ProjectionType.Perspective;
			Fov = SafeFov;
		}

		// Measure the screen from the steady camera, then add the shake, which only changes what is drawn.
		MeasureSteadyCorners();
		GlobalPosition = steady + ShakeOffset();
	}

	/// <summary>
	/// Works out where the screen's corners land on the plane from the camera's own settings (where it sits, its
	/// tilt, its field of view or size) at the project's reference screen shape, rather than from the window.
	/// Gameplay measures from these, so a fight plays the same headless or in a window of any size, and on any
	/// computer. The camera keeps the screen's height fixed, so a window wider than the reference shows a
	/// little more at the sides than this counts as visible.
	/// </summary>
	private void MeasureSteadyCorners()
	{
		float aspect = ReferenceAspect();
		float tilt = Mathf.DegToRad(SafeTilt);
		float height = Height;
		var position = new Vector3(Focus.X, height, Focus.Z + height * Mathf.Tan(tilt));
		Basis basis = Basis.FromEuler(new Vector3(-(Mathf.Pi * 0.5f - tilt), 0f, 0f));
		Vector3 right = basis.X, up = basis.Y, forward = -basis.Z;
		float halfHeight = Tuning.Camera.Orthographic
			? Tuning.Camera.ViewHeight * 0.5f
			: Mathf.Tan(Mathf.DegToRad(SafeFov) * 0.5f);

		for (int i = 0; i < 4; i++)
		{
			Vector3 across = right * Corners[i].X * halfHeight * aspect + up * Corners[i].Y * halfHeight;
			// A perspective camera fans its rays out from one point; an orthographic one sends them in parallel.
			Vector3 origin = Tuning.Camera.Orthographic ? position + across : position;
			Vector3 ray = Tuning.Camera.Orthographic ? forward : forward + across;
			Vector3 hit = origin + ray * (-origin.Y / ray.Y);
			_steadyCorners[i] = new Vector2(hit.X, hit.Z);
		}
		_cornersReady = true;
	}

	// Top left, top right, bottom right, bottom left: around the screen in order. Made once, not every frame.
	private static readonly Vector2[] Corners = { new(-1f, 1f), new(1f, 1f), new(1f, -1f), new(-1f, -1f) };

	private static float _referenceAspect;

	/// <summary>
	/// The screen shape the game is made for: width over height, from the project's viewport size. Read once,
	/// since reading a project setting by name makes garbage, and the setting doesn't change while playing.
	/// </summary>
	private static float ReferenceAspect()
	{
		if (_referenceAspect > 0f)
			return _referenceAspect;
		float width = ProjectSettings.GetSetting("display/window/size/viewport_width", 1920).AsSingle();
		float height = ProjectSettings.GetSetting("display/window/size/viewport_height", 1080).AsSingle();
		_referenceAspect = height > 0f ? width / height : 16f / 9f;
		return _referenceAspect;
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
	/// was last placed, without its screen shake: the shake uses its own randomness, and if it moved where
	/// enemies spawn, a fight would no longer repeat from its seed.
	/// </summary>
	/// <summary>
	/// Whether a point on the plane is on screen, in the steady view at the reference screen shape that
	/// gameplay measures from (see MeasureSteadyCorners). Pilots see only what this says is on screen.
	/// </summary>
	public bool IsOnScreen(Vector2 point)
	{
		if (!_cornersReady)
			MeasureSteadyCorners();
		// Inside the four-sided view when on the same side of every edge.
		float side = 0f;
		for (int i = 0; i < 4; i++)
		{
			Vector2 a = _steadyCorners[i];
			Vector2 b = _steadyCorners[(i + 1) % 4];
			float cross = (b - a).Cross(point - a);
			if (side == 0f)
				side = Mathf.Sign(cross);
			else if (cross != 0f && Mathf.Sign(cross) != side)
				return false;
		}
		return true;
	}

	/// <summary>How far the farthest point on screen is from a point on the plane: the most a pilot there could see.</summary>
	public float FarthestOnScreen(Vector2 from)
	{
		if (!_cornersReady)
			MeasureSteadyCorners();
		float farthest = 0f;
		foreach (Vector2 corner in _steadyCorners)
			farthest = Mathf.Max(farthest, corner.DistanceTo(from));
		return farthest;
	}

	public float DistanceOutOfView(Vector2 from, Vector2 direction, float margin)
	{
		// Where the four screen corners land on the plane, measured from the steady camera.
		if (!_cornersReady)
			MeasureSteadyCorners();
		Vector2[] corners = _steadyCorners;
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

	private static Vector3 PlaneOnly(Vector3 world) => new(world.X, 0f, world.Z);
}
