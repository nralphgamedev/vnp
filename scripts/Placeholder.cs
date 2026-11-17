using Godot;

/// <summary>Helpers for building the simple geometric placeholder art.</summary>
public static class Placeholder
{
	/// <summary>A flat-coloured material with a little self-glow so shapes stay readable against space.</summary>
	public static StandardMaterial3D Material(Color color, float glow = 0.1f, float alpha = 1f)
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = new Color(color, alpha),
			Roughness = 0.7f,
			EmissionEnabled = glow > 0f,
			Emission = color,
			EmissionEnergyMultiplier = glow,
		};
		if (alpha < 1f)
		{
			material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
			material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
		}
		return material;
	}

	/// <summary>Adds a mesh as a child of <paramref name="parent"/> and returns it.</summary>
	public static MeshInstance3D AddShape(Node3D parent, Mesh mesh, Material material,
		Vector3 position = default, Vector3 rotationDegrees = default)
	{
		var shape = new MeshInstance3D
		{
			Mesh = mesh,
			MaterialOverride = material,
			Position = position,
			RotationDegrees = rotationDegrees,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		parent.AddChild(shape);
		return shape;
	}
}
