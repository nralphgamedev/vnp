using Godot;

/// <summary>
/// Conversions between the flat gameplay plane and the 3D world.
/// Gameplay is calculated as Vector2 (x, y) on the plane. In the world that is (x, 0, y):
/// plane x runs to the right of the screen and plane y runs toward the bottom of the screen.
/// </summary>
public static class PlaneMath
{
	public static Vector2 ToPlane(Vector3 world) => new(world.X, world.Z);

	public static Vector3 ToWorld(Vector2 plane, float height = 0f) => new(plane.X, height, plane.Y);

	/// <summary>
	/// Places a 1 by 1 by 1 box as a bar from one point to another in 3D, <paramref name="width"/> across
	/// and <paramref name="thickness"/> high. Used for beams that dip down into what they reach.
	/// </summary>
	public static Transform3D Bar(Vector3 from, Vector3 to, float width, float thickness)
	{
		Vector3 along = to - from;
		float length = along.Length();
		if (length < 0.0001f)
			return new Transform3D(Basis.FromScale(Vector3.Zero), from);

		Vector3 forward = along / length;
		Vector3 side = Vector3.Up.Cross(forward);
		side = side.LengthSquared() > 0.000001f ? side.Normalized() : Vector3.Right;
		Vector3 up = forward.Cross(side);
		return new Transform3D(new Basis(side * width, up * thickness, forward * length), (from + to) * 0.5f);
	}

	/// <summary>Rotation around the vertical axis that points a model's forward (-Z) along a plane direction.</summary>
	public static float YawFacing(Vector2 planeDirection) => Mathf.Atan2(-planeDirection.X, -planeDirection.Y);

	/// <summary>
	/// Where to aim a shot so that it meets a moving target: the point the target will have reached
	/// when a shot of the given speed arrives. If the target cannot be caught within
	/// <paramref name="maxTime"/> seconds, aims as far ahead as that time allows.
	/// </summary>
	public static Vector2 AimAhead(Vector2 shooter, Vector2 target, Vector2 targetVelocity, float shotSpeed, float maxTime)
	{
		// Solve |toTarget + targetVelocity * t| = shotSpeed * t for the earliest t above zero.
		Vector2 toTarget = target - shooter;
		float a = targetVelocity.LengthSquared() - shotSpeed * shotSpeed;
		float b = 2f * toTarget.Dot(targetVelocity);
		float c = toTarget.LengthSquared();

		float time = maxTime;
		if (Mathf.Abs(a) < 0.0001f)
		{
			// Target and shot are equally fast: the equation is no longer quadratic.
			if (b < -0.0001f)
				time = -c / b;
		}
		else
		{
			float discriminant = b * b - 4f * a * c;
			if (discriminant >= 0f)
			{
				float root = Mathf.Sqrt(discriminant);
				float first = (-b - root) / (2f * a);
				float second = (-b + root) / (2f * a);
				float earliest = Mathf.Min(first, second);
				float latest = Mathf.Max(first, second);
				if (earliest > 0f)
					time = earliest;
				else if (latest > 0f)
					time = latest;
			}
		}

		return target + targetVelocity * Mathf.Min(time, maxTime);
	}

	/// <summary>Shortest distance from a point to the line segment between two other points.</summary>
	public static float DistanceToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
	{
		Vector2 along = segmentEnd - segmentStart;
		float lengthSquared = along.LengthSquared();
		if (lengthSquared < 0.000001f)
			return point.DistanceTo(segmentStart);

		float t = Mathf.Clamp((point - segmentStart).Dot(along) / lengthSquared, 0f, 1f);
		return point.DistanceTo(segmentStart + along * t);
	}
}
