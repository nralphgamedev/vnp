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

	/// <summary>
	/// A material for shapes drawn through an InstanceBatch, where each copy brings its own colour.
	/// Unshaded ignores the lights and shows the colour at full brightness, for shots and beams.
	/// </summary>
	public static StandardMaterial3D InstanceMaterial(bool unshaded = false)
	{
		return new StandardMaterial3D
		{
			VertexColorUseAsAlbedo = true,
			VertexColorIsSrgb = true,
			Roughness = 0.7f,
			ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
		};
	}

	/// <summary>
	/// Builds a flat-topped solid from an outline drawn on the plane, like a cookie cutter.
	/// Outline points are (x, y) with -y as the shape's forward direction.
	/// This is how each kind of unit gets its own silhouette.
	/// </summary>
	public static ArrayMesh ExtrudedShape(Vector2[] outline, float thickness)
	{
		var surface = new SurfaceTool();
		surface.Begin(Mesh.PrimitiveType.Triangles);

		float top = thickness * 0.5f;
		float bottom = -top;

		// Top and bottom faces.
		int[] triangles = Geometry2D.TriangulatePolygon(outline);
		for (int t = 0; t + 2 < triangles.Length; t += 3)
		{
			Vector2 a = outline[triangles[t]];
			Vector2 b = outline[triangles[t + 1]];
			Vector2 c = outline[triangles[t + 2]];
			AddTriangle(surface, PlaneMath.ToWorld(a, top), PlaneMath.ToWorld(b, top), PlaneMath.ToWorld(c, top), Vector3.Up);
			AddTriangle(surface, PlaneMath.ToWorld(a, bottom), PlaneMath.ToWorld(b, bottom), PlaneMath.ToWorld(c, bottom), Vector3.Down);
		}

		// Which way the outline winds decides which side of each edge is the outside.
		float area = 0f;
		for (int i = 0; i < outline.Length; i++)
			area += outline[i].Cross(outline[(i + 1) % outline.Length]);
		float outward = area >= 0f ? 1f : -1f;

		// Side walls.
		for (int i = 0; i < outline.Length; i++)
		{
			Vector2 a = outline[i];
			Vector2 b = outline[(i + 1) % outline.Length];
			Vector2 edge = b - a;
			Vector3 normal = PlaneMath.ToWorld(new Vector2(edge.Y, -edge.X).Normalized() * outward);

			Vector3 aTop = PlaneMath.ToWorld(a, top), bTop = PlaneMath.ToWorld(b, top);
			Vector3 aBottom = PlaneMath.ToWorld(a, bottom), bBottom = PlaneMath.ToWorld(b, bottom);
			AddTriangle(surface, aTop, bTop, bBottom, normal);
			AddTriangle(surface, aTop, bBottom, aBottom, normal);
		}

		return surface.Commit();
	}

	/// <summary>Adds one triangle that faces along <paramref name="normal"/>, whichever order its corners came in.</summary>
	private static void AddTriangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
	{
		// Godot treats a triangle as facing you when its corners run clockwise.
		if ((c - a).Cross(b - a).Dot(normal) < 0f)
			(b, c) = (c, b);

		surface.SetNormal(normal);
		surface.AddVertex(a);
		surface.SetNormal(normal);
		surface.AddVertex(b);
		surface.SetNormal(normal);
		surface.AddVertex(c);
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
