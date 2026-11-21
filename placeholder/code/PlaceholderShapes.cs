using Godot;
using System.Collections.Generic;

/// <summary>
/// Builds the placeholder shapes that are made in code. PlaceholderBaker uses ExtrudedShape to make the
/// unit meshes once and save them as files. The asteroid rocks are made by Rock each time a fight starts,
/// because their shape follows tuning values.
/// </summary>
public static class PlaceholderShapes
{
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

	/// <summary>
	/// Builds a low-poly lumpy rock: a sphere of small flat faces, pushed in and out by smooth noise.
	/// Its farthest point is 1 from the centre. Different seeds give different rocks.
	/// <paramref name="bumpiness"/> 0 is a plain ball; 0.5 is very lumpy.
	/// </summary>
	public static ArrayMesh Rock(int seed, float bumpiness)
	{
		// Start from an icosahedron: 12 corners, 20 faces.
		float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
		var corners = new List<Vector3>
		{
			new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
			new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
			new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
		};
		for (int i = 0; i < corners.Count; i++)
			corners[i] = corners[i].Normalized();
		var faces = new List<(int A, int B, int C)>
		{
			(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11),
			(1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
			(3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9),
			(4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1),
		};

		// Split every face into four, once: 80 faces, enough to read as a rock and still look faceted.
		var middles = new Dictionary<(int, int), int>();
		int Middle(int a, int b)
		{
			var key = a < b ? (a, b) : (b, a);
			if (!middles.TryGetValue(key, out int index))
			{
				index = corners.Count;
				corners.Add(((corners[a] + corners[b]) * 0.5f).Normalized());
				middles[key] = index;
			}
			return index;
		}
		var split = new List<(int A, int B, int C)>();
		foreach (var (a, b, c) in faces)
		{
			int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
			split.Add((a, ab, ca));
			split.Add((b, bc, ab));
			split.Add((c, ca, bc));
			split.Add((ab, bc, ca));
		}

		// Push each corner in or out, then scale so the farthest point is 1.
		var noise = new FastNoiseLite { Seed = seed, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.9f };
		float farthest = 0f;
		for (int i = 0; i < corners.Count; i++)
		{
			corners[i] *= 1f + bumpiness * noise.GetNoise3Dv(corners[i] * 1.7f);
			farthest = Mathf.Max(farthest, corners[i].Length());
		}

		var surface = new SurfaceTool();
		surface.Begin(Mesh.PrimitiveType.Triangles);
		foreach (var (a, b, c) in split)
		{
			Vector3 pa = corners[a] / farthest, pb = corners[b] / farthest, pc = corners[c] / farthest;
			// Every face points away from the centre.
			Vector3 normal = (pb - pa).Cross(pc - pa).Normalized();
			if (normal.Dot(pa + pb + pc) < 0f)
				normal = -normal;
			AddTriangle(surface, pa, pb, pc, normal);
		}
		return surface.Commit();
	}

	/// <summary>
	/// Builds a smooth lumpy blob: a finely divided sphere pushed in and out by gentle noise, shaded smoothly
	/// rather than in flat faces, so it reads as soft rather than rocky. Its farthest point is 1 from the centre.
	/// </summary>
	public static ArrayMesh Blob(int seed, float lumpiness)
	{
		float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
		var corners = new List<Vector3>
		{
			new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
			new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
			new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
		};
		for (int i = 0; i < corners.Count; i++)
			corners[i] = corners[i].Normalized();
		var faces = new List<(int A, int B, int C)>
		{
			(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11),
			(1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
			(3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9),
			(4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1),
		};

		// Split every face into four, three times over: 1280 faces, smooth enough to look soft.
		for (int pass = 0; pass < 3; pass++)
		{
			var middles = new Dictionary<(int, int), int>();
			int Middle(int a, int b)
			{
				var key = a < b ? (a, b) : (b, a);
				if (!middles.TryGetValue(key, out int index))
				{
					index = corners.Count;
					corners.Add(((corners[a] + corners[b]) * 0.5f).Normalized());
					middles[key] = index;
				}
				return index;
			}
			var split = new List<(int A, int B, int C)>();
			foreach (var (a, b, c) in faces)
			{
				int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
				split.Add((a, ab, ca));
				split.Add((b, bc, ab));
				split.Add((c, ca, bc));
				split.Add((ab, bc, ca));
			}
			faces = split;
		}

		// A few broad lumps, from low-frequency noise, then scale so the farthest point is 1.
		var noise = new FastNoiseLite { Seed = seed, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.6f };
		float farthest = 0f;
		for (int i = 0; i < corners.Count; i++)
		{
			corners[i] *= 1f + lumpiness * noise.GetNoise3Dv(corners[i] * 1.5f);
			farthest = Mathf.Max(farthest, corners[i].Length());
		}
		for (int i = 0; i < corners.Count; i++)
			corners[i] /= farthest;

		// Corners are shared between faces, and each takes the average facing of the faces around it,
		// which is what makes the shading smooth.
		var normals = new Vector3[corners.Count];
		var indices = new List<int>();
		foreach (var (a, b, c) in faces)
		{
			// The face's outward facing, weighted by its size.
			Vector3 normal = (corners[b] - corners[a]).Cross(corners[c] - corners[a]);
			if (normal.Dot(corners[a] + corners[b] + corners[c]) < 0f)
				normal = -normal;
			normals[a] += normal;
			normals[b] += normal;
			normals[c] += normal;
			// Godot treats a triangle as facing you when its corners run clockwise.
			if ((corners[c] - corners[a]).Cross(corners[b] - corners[a]).Dot(normal) < 0f)
				indices.AddRange(new[] { a, c, b });
			else
				indices.AddRange(new[] { a, b, c });
		}
		for (int i = 0; i < normals.Length; i++)
			normals[i] = normals[i].Normalized();

		using var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = corners.ToArray();
		arrays[(int)Mesh.ArrayType.Normal] = normals;
		arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		return mesh;
	}

	/// <summary>
	/// A flat ring lying on the plane, seen from above and below. <paramref name="inner"/> and
	/// <paramref name="outer"/> are its radii; scale it to grow the ring.
	/// </summary>
	public static ArrayMesh FlatRing(float inner, float outer, int segments)
	{
		var surface = new SurfaceTool();
		surface.Begin(Mesh.PrimitiveType.Triangles);
		for (int i = 0; i < segments; i++)
		{
			float a0 = i * Mathf.Tau / segments;
			float a1 = (i + 1) * Mathf.Tau / segments;
			var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
			var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
			Vector3 i0 = d0 * inner, o0 = d0 * outer, i1 = d1 * inner, o1 = d1 * outer;
			foreach (Vector3 normal in new[] { Vector3.Up, Vector3.Down })
			{
				AddTriangle(surface, i0, o0, o1, normal);
				AddTriangle(surface, i0, o1, i1, normal);
			}
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
}
