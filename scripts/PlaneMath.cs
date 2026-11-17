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
}
