using Godot;

/// <summary>
/// The camera that follows the VNP. Values live in res://tuning/camera.tres.
///
/// Zoom and lens are separate: ViewHeight sets how much of the arena the screen shows, and Fov only how strong
/// the perspective looks, since the camera sits as high as it must for that field of view to show ViewHeight.
/// Before, the camera sat at a set height and Fov zoomed as well, which a high Fov took past the horizon.
/// </summary>
[GlobalClass]
public partial class CameraTuning : Resource
{
	/// <summary>False is perspective, true is orthographic. F1 switches while playing.</summary>
	[ExportGroup("Camera")]
	[Export] public bool Orthographic;

	/// <summary>How much of the arena the screen shows: its height, in units, across the middle of the screen,
	/// where the VNP is. The width follows from the screen's shape (at 16:9, about 1.8 times as wide). With the
	/// tilt, a little more shows above the VNP than below it, and the top of the screen is wider than the bottom.
	/// Raising it zooms out. Enemies spawn relative to the screen's edge, so they stay off screen at any zoom, but
	/// a zoom so wide that they would spawn farther away than DespawnDistance in enemy_spawning.tres is reported as
	/// an error, since they would be removed as they spawned.</summary>
	[Export] public float ViewHeight;

	/// <summary>The perspective lens, top to bottom, in degrees: how much nearer things look larger. Changing it
	/// doesn't zoom; the camera moves up or down to keep ViewHeight. Narrow is flatter, close to a map; wide gives
	/// more depth, with the top of the screen visibly farther away. Kept between 1 and the most that keeps the top
	/// of the screen at least 20 degrees below the horizon (110 at a tilt of 15); a value outside is reported and
	/// clamped. Orthographic ignores it.</summary>
	[Export] public float Fov;

	/// <summary>How far the camera leans away from looking straight down, in degrees. 0 is a pure top-down view.
	/// Kept between 0 and 60.</summary>
	[Export] public float Tilt;

	/// <summary>Seconds the camera takes to catch up with the VNP. 0 locks it to the VNP.</summary>
	[Export] public float FollowLag;
}
