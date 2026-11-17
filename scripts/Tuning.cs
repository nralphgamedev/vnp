using Godot;

/// <summary>
/// Every number that affects how the fight feels lives in this file and nowhere else.
/// Change a value, press Play, and Godot rebuilds before the fight starts.
///
/// Distances are in world units on the gameplay plane. For scale, the VNP is about
/// 3 units long. Times are in seconds, angles in degrees.
/// </summary>
public static class Tuning
{
	// ───────────── VNP movement ─────────────

	/// <summary>Top speed, in units per second, with the stick or keys fully held.</summary>
	public static float VnpMoveSpeed = 20f;

	/// <summary>How quickly the VNP gains speed or changes direction, in units per second squared.
	/// Higher is snappier. At 80 it reaches top speed in a quarter of a second.</summary>
	public static float VnpAcceleration = 80f;

	/// <summary>How quickly the VNP slows once input is released, in units per second squared.
	/// Higher stops sooner. Lower makes it drift.</summary>
	public static float VnpDeceleration = 60f;

	/// <summary>How fast the ship model turns to face its direction of travel, in degrees per second.
	/// Cosmetic only, because weapons aim on their own.</summary>
	public static float VnpTurnSpeed = 540f;

	/// <summary>Joystick movement below this fraction of full tilt is ignored.</summary>
	public static float StickDeadzone = 0.2f;

	// ───────────── Camera ─────────────

	/// <summary>False is perspective, true is orthographic. F1 switches while playing.</summary>
	public static bool CameraOrthographic = false;

	/// <summary>Perspective field of view, top to bottom. Narrow values flatten the picture.</summary>
	public static float CameraFov = 20f;

	/// <summary>How far the camera sits above the plane. Raising it shows more of the arena.</summary>
	public static float CameraHeight = 120f;

	/// <summary>How far the camera leans away from looking straight down. 0 is a pure top-down view.</summary>
	public static float CameraTilt = 15f;

	/// <summary>Seconds the camera takes to catch up with the VNP. 0 locks it to the VNP.</summary>
	public static float CameraFollowLag = 0f;

	// ───────────── Gateway ─────────────

	/// <summary>Where the gateway sits on the plane. The fight starts here.</summary>
	public static Vector2 GatewayPosition = Vector2.Zero;

	/// <summary>Size of the gateway ring. Later this is also the area that counts as "on the gateway".</summary>
	public static float GatewayRadius = 6f;

	// ───────────── Gateway indicator ─────────────

	/// <summary>Gap in pixels between the edge of the screen and the tip of the arrow.</summary>
	public static float IndicatorEdgeMargin = 28f;

	/// <summary>Length of the arrow in pixels.</summary>
	public static float IndicatorSize = 22f;

	/// <summary>False hides the arrow while the gateway itself is on screen.
	/// True keeps it at the screen edge at all times.</summary>
	public static bool IndicatorAlwaysVisible = false;

	// ───────────── Placeholder look ─────────────

	/// <summary>Scale of the VNP model. 1 is about 3 units long.</summary>
	public static float VnpSize = 1f;

	/// <summary>Spacing of the background grid lines. Every fifth line is brighter.</summary>
	public static float GridCellSize = 5f;

	public static Color VnpHullColor = new(0.62f, 0.7f, 0.9f);
	public static Color VnpCoreColor = new(1.0f, 0.72f, 0.2f);
	public static Color GatewayColor = new(0.2f, 0.95f, 0.85f);
	public static Color GridBaseColor = new(0.015f, 0.02f, 0.045f);
	public static Color GridLineColor = new(0.07f, 0.11f, 0.2f);
	public static Color GridMajorLineColor = new(0.13f, 0.22f, 0.4f);
}
