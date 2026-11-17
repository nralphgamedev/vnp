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

	/// <summary>Rotation around the vertical axis that points a model's forward (-Z) along a plane direction.</summary>
	public static float YawFacing(Vector2 planeDirection) => Mathf.Atan2(-planeDirection.X, -planeDirection.Y);

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
