using Godot;

/// <summary>The camera that follows the VNP. Values live in res://tuning/camera.tres.</summary>
[GlobalClass]
public partial class CameraTuning : Resource
{
	/// <summary>False is perspective, true is orthographic. F1 switches while playing.</summary>
	[ExportGroup("Camera")]
	[Export] public bool Orthographic;

	/// <summary>Perspective field of view, top to bottom. Narrow values flatten the picture.</summary>
	[Export] public float Fov;

	/// <summary>How far the camera sits above the plane. Raising it shows more of the arena.
	/// At 150 the screen shows about 97 by 55 units. Enemies spawn relative to the screen edge,
	/// so they stay off screen at any height.</summary>
	[Export] public float Height;

	/// <summary>How far the camera leans away from looking straight down. 0 is a pure top-down view.</summary>
	[Export] public float Tilt;

	/// <summary>Seconds the camera takes to catch up with the VNP. 0 locks it to the VNP.</summary>
	[Export] public float FollowLag;
}
