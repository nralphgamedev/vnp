using Godot;

/// <summary>
/// Finds the placeholder art. Every placeholder visual is a file in res://placeholder/, listed in
/// res://placeholder/PLACEHOLDERS.md, so final art can replace it one file at a time.
/// </summary>
public static class Placeholders
{
	public static Mesh Mesh(string name) => Load<Mesh>($"res://placeholder/meshes/{name}.tres");

	public static Material Material(string name) => Load<Material>($"res://placeholder/materials/{name}.tres");

	/// <summary>A HUD icon. Weapon icons are named after the weapon's ID.</summary>
	public static Texture2D Icon(string name) => Load<Texture2D>($"res://placeholder/textures/icons/{name}.svg");

	/// <summary>A model: a small scene of meshes and materials, placed under a unit as one piece.</summary>
	public static PackedScene Model(string name) => Load<PackedScene>($"res://placeholder/models/{name}.tscn");

	private static T Load<T>(string path) where T : Resource
	{
		T asset = GD.Load<T>(path);
		if (asset == null)
			GD.PushError($"Missing placeholder asset {path}. Run res://placeholder/code/Bake.tscn to make it again.");
		return asset;
	}
}
