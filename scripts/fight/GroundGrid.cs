using Godot;

/// <summary>
/// The visible plane: a dark sheet with grid lines, so movement can be seen in empty space.
/// The sheet travels with the camera while its lines stay fixed in the world, which makes
/// the arena look endless.
/// </summary>
public partial class GroundGrid : MeshInstance3D
{
	// Below the units, the gateway, and the lowest point of a tumbling asteroid (about -6.6 for the largest).
	private const float VisualHeight = -7.0f;

	// Width of the sheet mesh, so it is never culled while under the camera.
	private const float SheetSize = 800f;

	/// <summary>The camera the sheet stays under.</summary>
	public CameraRig Camera;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Ground;

		// The colours and line spacing are in the grid's material.
		Mesh = Placeholders.Mesh("ground_sheet");
		MaterialOverride = Placeholders.Material("ground_grid");
		CastShadow = ShadowCastingSetting.Off;
		// The shader offsets nothing, but the sheet is always under the camera, so never cull it.
		ExtraCullMargin = SheetSize;

		Position = new Vector3(0f, VisualHeight, 0f);
	}

	public override void _Process(double delta)
	{
		if (Camera == null)
			return;
		Position = new Vector3(Camera.Focus.X, VisualHeight, Camera.Focus.Z);
	}
}
