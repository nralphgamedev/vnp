using Godot;

/// <summary>
/// The visible plane: a dark sheet with grid lines, so movement can be seen in empty space.
/// The sheet travels with the camera while its lines stay fixed in the world, which makes
/// the arena look endless.
/// </summary>
public partial class GroundGrid : MeshInstance3D
{
	// Below the units and the gateway.
	private const float VisualHeight = -2.0f;
	private const float SheetSize = 800f;

	/// <summary>The camera the sheet stays under.</summary>
	public CameraRig Camera;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Ground;

		Mesh = new PlaneMesh { Size = new Vector2(SheetSize, SheetSize) };
		CastShadow = ShadowCastingSetting.Off;
		// The shader offsets nothing, but the sheet is always under the camera, so never cull it.
		ExtraCullMargin = SheetSize;

		var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/ground_grid.gdshader") };
		material.SetShaderParameter("base_color", Tuning.GridBaseColor);
		material.SetShaderParameter("line_color", Tuning.GridLineColor);
		material.SetShaderParameter("major_color", Tuning.GridMajorLineColor);
		material.SetShaderParameter("cell_size", Tuning.GridCellSize);
		MaterialOverride = material;

		Position = new Vector3(0f, VisualHeight, 0f);
	}

	public override void _Process(double delta)
	{
		if (Camera == null)
			return;
		Position = new Vector3(Camera.Focus.X, VisualHeight, Camera.Focus.Z);
	}
}
