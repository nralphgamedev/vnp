using Godot;

/// <summary>
/// Makes every placeholder mesh, material, model and sound file in res://placeholder/ and saves it, overwriting
/// what is there. The shapes and colours are written here, so change them here and bake again rather
/// than editing the files by hand. To bake, open res://placeholder/code/Bake.tscn and press F6
/// (Run Current Scene), or run Godot with that scene from the command line. It quits when done.
///
/// Not part of the fight: nothing in the game runs this.
/// </summary>
public partial class PlaceholderBaker : Node
{
	private const string Root = "res://placeholder";

	// Outlines for the flat-topped unit shapes. Points are (x, y) on the plane, with -y as forward.

	// Drone: a small kite.
	private static readonly Vector2[] DroneOutline =
	{
		new(0f, -0.75f), new(0.45f, 0.05f), new(0f, 0.55f), new(-0.45f, 0.05f),
	};

	// Enemy: two prongs forward, like a pincer.
	private static readonly Vector2[] EnemyOutline =
	{
		new(-1.3f, -1.1f), new(0f, -0.15f), new(1.3f, -1.1f),
		new(0.85f, 0.9f), new(0f, 0.5f), new(-0.85f, 0.9f),
	};

	// Orbital Shard: a long thin diamond.
	private static readonly Vector2[] ShardOutline =
	{
		new(0f, -1f), new(0.32f, 0f), new(0f, 1f), new(-0.32f, 0f),
	};

	// Debris: a broken plate of hull, with a few torn edges. It tumbles, so its thickness shows.
	// Its farthest point from the centre is about 1, which Harvestables.DebrisExtent relies on.
	private static readonly Vector2[] DebrisOutline =
	{
		new(-0.85f, -0.5f), new(0.15f, -0.7f), new(0.4f, -0.4f), new(0.9f, -0.45f), new(0.7f, 0.25f),
		new(0.85f, 0.55f), new(0.1f, 0.7f), new(-0.25f, 0.45f), new(-0.8f, 0.55f), new(-0.6f, 0.05f),
	};

	// Fragment: a small jagged triangle that destroyed enemies and used-up material burst into.
	private static readonly Vector2[] FragmentOutline =
	{
		new(0f, -0.5f), new(0.4f, 0.35f), new(-0.05f, 0.2f), new(-0.4f, 0.4f),
	};

	// Seeker missile: a dart with small fins at the back.
	private static readonly Vector2[] MissileOutline =
	{
		new(0f, -0.9f), new(0.2f, -0.35f), new(0.2f, 0.55f), new(0.42f, 0.9f), new(-0.42f, 0.9f), new(-0.2f, 0.55f),
		new(-0.2f, -0.35f),
	};

	// Mine: a squat disc with eight short spikes.
	private static readonly Vector2[] MineOutline = MineShape();

	private static Vector2[] MineShape()
	{
		var points = new Vector2[16];
		for (int i = 0; i < 16; i++)
		{
			float angle = i * Mathf.Tau / 16f;
			float radius = i % 2 == 0 ? 1f : 0.62f;
			points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
		}
		return points;
	}

	private static readonly Color VnpHullColor = new(0.62f, 0.7f, 0.9f);
	private static readonly Color VnpCoreColor = new(1.0f, 0.72f, 0.2f);
	private static readonly Color GatewayColor = new(0.2f, 0.95f, 0.85f);
	private static readonly Color PulseLaserColor = new(0.55f, 0.95f, 1.0f);
	private static readonly Color ExperienceColor = new(0.72f, 0.45f, 1.0f);

	// The gateway model is made at this radius. Gateway.cs scales it to the radius in fight.tres.
	private const float GatewayRadius = 6f;

	public override void _Ready()
	{
		Bake();
		GD.Print("Placeholder assets baked.");
		GetTree().Quit();
	}

	private static void Bake()
	{
		foreach (string folder in new[] { "meshes", "materials", "models" })
			DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath($"{Root}/{folder}"));

		// ───────────── Meshes ─────────────
		Save(PlaceholderShapes.ExtrudedShape(DroneOutline, 0.45f), "meshes/drone.tres");
		Save(PlaceholderShapes.ExtrudedShape(EnemyOutline, 0.7f), "meshes/enemy.tres");
		Save(PlaceholderShapes.ExtrudedShape(ShardOutline, 0.4f), "meshes/orbital_shard.tres");
		Save(PlaceholderShapes.ExtrudedShape(DebrisOutline, 0.5f), "meshes/debris.tres");
		Save(PlaceholderShapes.ExtrudedShape(MissileOutline, 0.3f), "meshes/missile.tres");
		Save(PlaceholderShapes.ExtrudedShape(MineOutline, 0.35f), "meshes/mine.tres");
		// A thin flat ring, outer radius 1, for the Shockwave.
		Save(PlaceholderShapes.FlatRing(0.9f, 1f, 72), "meshes/shockwave_ring.tres");
		// A 1 by 1 by 1 box, stretched into beams.
		Save(new BoxMesh { Size = Vector3.One }, "meshes/unit_box.tres");
		Save(new BoxMesh { Size = new Vector3(0.4f, 0.4f, 1.3f) }, "meshes/projectile.tres");
		// Effects: pieces of a burst, and a flat ring that spreads out. Sparks use the unit box.
		Save(PlaceholderShapes.ExtrudedShape(FragmentOutline, 0.3f), "meshes/fragment.tres");
		Save(PlaceholderShapes.FlatRing(0.8f, 1f, 40), "meshes/ring.tres");
		// The sheet the ground grid is drawn on. It moves with the camera.
		Save(new PlaneMesh { Size = new Vector2(800f, 800f) }, "meshes/ground_sheet.tres");

		Mesh vnpHull = Save(new PrismMesh { Size = new Vector3(2.2f, 3.0f, 0.8f) }, "meshes/vnp_hull.tres");
		Mesh vnpPod = Save(new BoxMesh { Size = new Vector3(0.5f, 0.6f, 1.5f) }, "meshes/vnp_pod.tres");
		Mesh vnpCore = Save(new SphereMesh { Radius = 0.45f, Height = 0.9f }, "meshes/vnp_core.tres");

		float r = GatewayRadius;
		Mesh gatewayRing = Save(new TorusMesh { InnerRadius = r - 0.4f, OuterRadius = r }, "meshes/gateway_ring.tres");
		Mesh gatewayDisc = Save(new CylinderMesh { TopRadius = r - 0.4f, BottomRadius = r - 0.4f, Height = 0.05f },
			"meshes/gateway_disc.tres");
		Mesh gatewayPylon = Save(new BoxMesh { Size = new Vector3(0.9f, 2.6f, 0.9f) }, "meshes/gateway_pylon.tres");
		float inner = r * 0.5f;
		Mesh gatewayInnerRing = Save(new TorusMesh { InnerRadius = inner - 0.2f, OuterRadius = inner },
			"meshes/gateway_inner_ring.tres");
		Mesh gatewayMarker = Save(new BoxMesh { Size = new Vector3(0.6f, 0.6f, 0.6f) }, "meshes/gateway_marker.tres");

		// ───────────── Materials ─────────────
		// Shapes drawn many at once, where each copy brings its own colour.
		Save(InstanceMaterial(unshaded: false), "materials/instanced_lit.tres");
		Save(InstanceMaterial(unshaded: true), "materials/instanced_unshaded.tres");
		// Glowing effects: colours add to what is behind them, so fading to black fades to nothing.
		StandardMaterial3D glow = InstanceMaterial(unshaded: true);
		glow.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
		glow.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		glow.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
		Save(glow, "materials/instanced_glow.tres");

		Material hull = Save(Solid(VnpHullColor, glow: 0.1f), "materials/vnp_hull.tres");
		Material core = Save(Solid(VnpCoreColor, glow: 0.8f), "materials/vnp_core.tres");
		Material gatewaySolid = Save(Solid(GatewayColor, glow: 0.3f), "materials/gateway_solid.tres");
		Material gatewayFaint = Save(Solid(GatewayColor, glow: 0.15f, alpha: 0.12f), "materials/gateway_faint.tres");

		// Beams. The game sets their colour from tuning; these colours are only the starting point.
		Save(new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = PulseLaserColor,
		}, "materials/pulse_laser_beam.tres");
		Save(new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = ExperienceColor,
		}, "materials/harvest_beam.tres");

		var grid = new ShaderMaterial { Shader = GD.Load<Shader>($"{Root}/shaders/ground_grid.gdshader") };
		grid.SetShaderParameter("base_color", new Color(0.015f, 0.02f, 0.045f));
		grid.SetShaderParameter("line_color", new Color(0.07f, 0.11f, 0.2f));
		grid.SetShaderParameter("major_color", new Color(0.13f, 0.22f, 0.4f));
		// Spacing of the grid lines in world units. Every fifth line is brighter.
		grid.SetShaderParameter("cell_size", 5f);
		grid.SetShaderParameter("major_every", 5f);
		Save(grid, "materials/ground_grid.tres");

		// ───────────── Models ─────────────
		// VNP: an arrowhead hull with two side pods and a glowing core.
		var vnp = new Node3D { Name = "Vnp" };
		// A prism stands upright by default, so tip it forward to make the point face -Z.
		AddShape(vnp, "Hull", vnpHull, hull, rotationDegrees: new Vector3(-90f, 0f, 0f));
		AddShape(vnp, "PodLeft", vnpPod, hull, new Vector3(-1.25f, 0f, 1.0f));
		AddShape(vnp, "PodRight", vnpPod, hull, new Vector3(1.25f, 0f, 1.0f));
		AddShape(vnp, "Core", vnpCore, core, new Vector3(0f, 0.45f, 0.55f));
		SaveModel(vnp, "models/vnp.tscn");

		// Gateway: a ring marking its edge, a faint disc inside, four pylons, and a slowly turning inner
		// ring with three markers. Gateway.cs turns the node called InnerRing.
		var gateway = new Node3D { Name = "Gateway" };
		AddShape(gateway, "Ring", gatewayRing, gatewaySolid);
		AddShape(gateway, "Disc", gatewayDisc, gatewayFaint);
		for (int i = 0; i < 4; i++)
		{
			float angle = Mathf.Pi / 4f + i * Mathf.Pi / 2f;
			var offset = new Vector3(Mathf.Cos(angle) * r, 0.8f, Mathf.Sin(angle) * r);
			AddShape(gateway, $"Pylon{i + 1}", gatewayPylon, gatewaySolid, offset, new Vector3(0f, 45f, 0f));
		}
		var innerRing = new Node3D { Name = "InnerRing" };
		gateway.AddChild(innerRing);
		AddShape(innerRing, "Ring", gatewayInnerRing, gatewaySolid);
		for (int i = 0; i < 3; i++)
		{
			float angle = i * Mathf.Tau / 3f;
			AddShape(innerRing, $"Marker{i + 1}", gatewayMarker, gatewaySolid,
				new Vector3(Mathf.Cos(angle) * inner, 0f, Mathf.Sin(angle) * inner));
		}
		SaveModel(gateway, "models/gateway.tscn");

		// ───────────── Sounds ─────────────
		PlaceholderSounds.Bake($"{Root}/audio");
	}

	/// <summary>A flat-coloured material with a little self-glow so shapes stay readable against space.</summary>
	private static StandardMaterial3D Solid(Color color, float glow, float alpha = 1f)
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
	private static StandardMaterial3D InstanceMaterial(bool unshaded)
	{
		return new StandardMaterial3D
		{
			VertexColorUseAsAlbedo = true,
			VertexColorIsSrgb = true,
			Roughness = 0.7f,
			ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
		};
	}

	private static void AddShape(Node3D parent, string name, Mesh mesh, Material material,
		Vector3 position = default, Vector3 rotationDegrees = default)
	{
		parent.AddChild(new MeshInstance3D
		{
			Name = name,
			Mesh = mesh,
			MaterialOverride = material,
			Position = position,
			RotationDegrees = rotationDegrees,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		});
	}

	/// <summary>Saves a resource and points it at its file, so models refer to the file rather than a copy.</summary>
	private static T Save<T>(T resource, string path) where T : Resource
	{
		string full = $"{Root}/{path}";
		Error error = ResourceSaver.Save(resource, full);
		if (error != Error.Ok)
			GD.PushError($"Could not save {full}: {error}");
		resource.TakeOverPath(full);
		return resource;
	}

	private static void SaveModel(Node3D root, string path)
	{
		SetOwner(root, root);
		var scene = new PackedScene();
		scene.Pack(root);
		Save(scene, path);
		root.Free();
	}

	private static void SetOwner(Node node, Node owner)
	{
		foreach (Node child in node.GetChildren())
		{
			child.Owner = owner;
			SetOwner(child, owner);
		}
	}
}
