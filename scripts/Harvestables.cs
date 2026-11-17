using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Everything that can be harvested: asteroids, which sit in asteroid fields at set places in the
/// arena, and the debris that destroyed enemies leave. Each has a mass. Harvest beams draw mass
/// out of it and it shrinks until nothing is left. Nothing here disappears on its own.
/// </summary>
public partial class Harvestables : Node3D
{
	/// <summary>An asteroid field: a cluster or ring of asteroids that may drift or turn as one.</summary>
	public struct Field
	{
		public Vector2 Center;
		public Vector2 Velocity;   // Zero unless the field drifts.
		public float Angle;        // How far it has turned, in radians.
		public float TurnSpeed;    // Radians per second. Zero unless the field turns.
		public bool IsRing;
	}

	private struct Item
	{
		public int Id;             // Stays the same for its whole life, unlike its place in the array.
		public Vector2 Position;
		public float Mass;
		public float Yaw;
		public Basis Shape;        // An asteroid's own fixed turn and stretch in 3D.
		public int Variant;        // Which rock shape an asteroid is drawn with.
		public bool IsAsteroid;    // An asteroid, as opposed to debris.
		public int Field;          // Its asteroid field, or -1 for a lone asteroid or debris.
		public Vector2 Offset;     // Position relative to its field's center, before the field turns.
		public float Glow;         // Seconds left of the tint that shows it is being harvested.
		public Vector3 TumbleAxis; // Axis a tumbling asteroid turns around. Zero if it does not tumble.
		public float TumbleSpeed;  // Radians per second.
		public float TumblePhase;  // Starting tumble angle, so tumblers are not in step.
	}

	// Placeholder silhouettes: asteroids are rounded lumps, debris is a jagged shard.
	private static readonly Vector2[] DebrisOutline =
	{
		new(-0.9f, -0.3f), new(-0.2f, -0.5f), new(0.1f, -1.0f), new(0.4f, -0.3f), new(1.0f, -0.1f),
		new(0.3f, 0.3f), new(0.5f, 0.9f), new(-0.2f, 0.4f), new(-0.7f, 0.6f), new(-0.5f, 0.1f),
	};

	private const float DebrisThickness = 0.45f;

	// Rough radius of each outline at scale 1. Used for harvest reach and spacing.
	private const float AsteroidShapeRadius = 0.9f;
	private const float DebrisShapeRadius = 0.8f;

	// How long the being-harvested tint lingers after a beam lets go.
	private const float GlowTime = 0.15f;

	public Fight Fight;

	/// <summary>How far a harvest beam reaches past the edge of what it harvests.
	/// Shared by the VNP and its drones, so one upgrade can change both.</summary>
	public float BeamRange = Tuning.HarvestBeamRange;

	public int AsteroidCount { get; private set; }
	public int DebrisCount { get; private set; }
	public int FieldCount => _fieldCount;
	public Field GetField(int index) => _fields[index];

	private Item[] _items = new Item[512];
	private int _count;
	private Field[] _fields = new Field[16];
	private int _fieldCount;
	private readonly HashSet<(int X, int Y)> _filledCells = new();
	private int _lastId;
	private float _maxRadius;
	private readonly SpatialGrid _grid = new(8f);
	private InstanceBatch[] _asteroidBatches;
	private int[] _variantCounts;
	private InstanceBatch _debrisBatch;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Harvestables;

		// Each rock shape is its own batch. The seeds are fixed so the shapes are the same every fight.
		int variants = Mathf.Max(1, Tuning.AsteroidShapeVariants);
		_asteroidBatches = new InstanceBatch[variants];
		_variantCounts = new int[variants];
		Material asteroidMaterial = Placeholder.InstanceMaterial();
		for (int v = 0; v < variants; v++)
			_asteroidBatches[v] = new InstanceBatch(this, $"AsteroidShapes{v}",
				Placeholder.Rock(1000 + v * 37, Tuning.AsteroidBumpiness), asteroidMaterial);
		_debrisBatch = new InstanceBatch(this, "DebrisShapes",
			Placeholder.ExtrudedShape(DebrisOutline, DebrisThickness), Placeholder.InstanceMaterial());

		_maxRadius = Mathf.Max(
			AsteroidShapeRadius * Tuning.AsteroidSize * Mathf.Sqrt(Tuning.AsteroidMassMax),
			DebrisShapeRadius * Tuning.DebrisSize * Mathf.Sqrt(Tuning.DebrisMassMax));

		FillStartingArea();
	}

	/// <summary>Leaves a piece of debris at a point on the plane.</summary>
	public void AddDebris(Vector2 position, float mass) => Add(position, mass, isAsteroid: false, field: -1, offset: default);

	// ───────────── Used by harvest beams ─────────────

	public Vector2 PositionOf(int index) => _items[index].Position;

	/// <summary>How fast an asteroid is moving with its field. Debris stays put.</summary>
	public Vector2 VelocityOf(int index)
	{
		ref Item item = ref _items[index];
		if (item.Field < 0)
			return Vector2.Zero;

		Field field = _fields[item.Field];
		Vector2 fromCenter = item.Offset.Rotated(field.Angle);
		return field.Velocity + new Vector2(-fromCenter.Y, fromCenter.X) * field.TurnSpeed;
	}

	/// <summary>A number that identifies an asteroid or piece of debris for its whole life.
	/// Use it to remember a harvest target between frames.</summary>
	public int IdOf(int index) => _items[index].Id;

	/// <summary>Current size. It shrinks as mass is drawn out.</summary>
	public float RadiusOf(int index)
	{
		ref Item item = ref _items[index];
		float shape = item.IsAsteroid
			? AsteroidShapeRadius * Tuning.AsteroidSize
			: DebrisShapeRadius * Tuning.DebrisSize;
		return shape * Mathf.Sqrt(item.Mass);
	}

	/// <summary>Distance from a point to the edge of an asteroid or piece of debris.</summary>
	public float EdgeDistance(int index, Vector2 point) => _items[index].Position.DistanceTo(point) - RadiusOf(index);

	/// <summary>
	/// Fills <paramref name="results"/> with everything whose edge is within <paramref name="reach"/>
	/// of a point and still has mass left, and returns how many. Stops when <paramref name="results"/> is full.
	/// </summary>
	public int CollectNear(Vector2 center, float reach, int[] results)
	{
		int candidates = _grid.Query(center, reach + _maxRadius, results);
		int kept = 0;
		for (int c = 0; c < candidates; c++)
		{
			int i = results[c];
			if (_items[i].Mass > 0f && EdgeDistance(i, center) <= reach)
				results[kept++] = i;
		}
		return kept;
	}

	/// <summary>Draws up to <paramref name="amount"/> mass out and passes it to the fight as harvest.
	/// Returns how much was drawn out, which is less if not that much was left.</summary>
	public float Harvest(int index, float amount)
	{
		ref Item item = ref _items[index];
		float taken = Mathf.Min(amount, item.Mass);
		if (taken <= 0f)
			return 0f;

		item.Mass -= taken;
		item.Glow = GlowTime;
		Fight.AddHarvest(taken);
		return taken;
	}

	// ───────────── Each frame ─────────────

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (Fight != null)
			FillArenaAround(Fight.Vnp.PlanePosition);

		for (int f = 0; f < _fieldCount; f++)
		{
			_fields[f].Center += _fields[f].Velocity * dt;
			_fields[f].Angle += _fields[f].TurnSpeed * dt;
		}

		// Clear away whatever was fully harvested last frame.
		for (int i = _count - 1; i >= 0; i--)
		{
			if (_items[i].Mass <= 0.0001f)
				_items[i] = _items[--_count]; // Fill the gap with the last one.
		}

		AsteroidCount = 0;
		DebrisCount = 0;
		_grid.Clear();
		for (int i = 0; i < _count; i++)
		{
			ref Item item = ref _items[i];
			if (item.Field >= 0)
			{
				Field field = _fields[item.Field];
				item.Position = field.Center + item.Offset.Rotated(field.Angle);
			}
			if (item.IsAsteroid)
				AsteroidCount++;
			else
				DebrisCount++;
			item.Glow = Mathf.Max(0f, item.Glow - dt);
			_grid.Insert(i, item.Position);
		}

		Draw();
	}

	private void Draw()
	{
		for (int v = 0; v < _asteroidBatches.Length; v++)
		{
			_asteroidBatches[v].Begin(AsteroidCount);
			_variantCounts[v] = 0;
		}
		_debrisBatch.Begin(DebrisCount);
		int debris = 0;
		float time = (float)Fight.Elapsed;

		for (int i = 0; i < _count; i++)
		{
			ref Item item = ref _items[i];
			Color color = item.IsAsteroid ? Tuning.AsteroidColor : Tuning.DebrisColor;
			if (item.Glow > 0f)
				color = color.Lerp(Tuning.ExperienceColor, 0.6f * item.Glow / GlowTime);

			if (item.IsAsteroid)
			{
				float scale = Tuning.AsteroidSize * Mathf.Sqrt(item.Mass);

				// Its own turn and stretch, then turning with its field (turning a vector by an angle on
				// the plane is the opposite turn as a yaw), then any tumble on top.
				Basis basis = item.Shape;
				if (item.Field >= 0)
					basis = new Basis(Vector3.Up, -_fields[item.Field].Angle) * basis;
				if (item.TumbleSpeed != 0f)
					basis = new Basis(item.TumbleAxis, item.TumblePhase + item.TumbleSpeed * time) * basis;
				basis = basis.Scaled(Vector3.One * scale);

				// A rock can point any side up, so sit it far enough down that its top stays just
				// below the units, so ships, shots and beams pass over it.
				float height = -0.3f - scale * Tuning.AsteroidStretchMax;
				int v = item.Variant;
				_asteroidBatches[v].SetBasis(_variantCounts[v]++, item.Position, height, basis, color);
			}
			else
			{
				float scale = Tuning.DebrisSize * Mathf.Sqrt(item.Mass);
				float height = -0.3f - DebrisThickness * scale * 0.5f;
				_debrisBatch.Set(debris++, item.Position, height, item.Yaw, scale, color);
			}
		}

		for (int v = 0; v < _asteroidBatches.Length; v++)
			_asteroidBatches[v].Commit(_variantCounts[v]);
		_debrisBatch.Commit(debris);
	}

	// ───────────── Filling the arena ─────────────
	// The area around the gateway is filled when the fight starts. Beyond it, the arena is filled
	// in square cells as the VNP comes within reach of each one, long before it is on screen.
	// Once placed, nothing moves to follow the VNP and nothing respawns.

	/// <summary>Radius of the area around the gateway that is filled when the fight starts.</summary>
	private static float StartingAreaRadius => Mathf.Max(Tuning.AsteroidFieldDistanceMax, Tuning.LoneAsteroidDistanceMax);

	/// <summary>Places fields and lone asteroids at random spots around the gateway.</summary>
	private void FillStartingArea()
	{
		Vector2 gateway = Tuning.GatewayPosition;
		PlaceFields(Rng.Int(Tuning.AsteroidFieldsMin, Tuning.AsteroidFieldsMax),
			() => gateway + Rng.Direction() * Rng.Range(Tuning.AsteroidFieldDistanceMin, Tuning.AsteroidFieldDistanceMax),
			_ => true);
		PlaceLoneAsteroids(Tuning.LoneAsteroidCount,
			() => gateway + Rng.Direction() * Rng.Range(Tuning.LoneAsteroidDistanceMin, Tuning.LoneAsteroidDistanceMax),
			_ => true);
	}

	/// <summary>Fills every cell within reach of a point that has not been filled yet.</summary>
	private void FillArenaAround(Vector2 point)
	{
		float size = Tuning.AsteroidCellSize;
		float reach = Tuning.AsteroidFillDistance;
		int minX = Mathf.FloorToInt((point.X - reach) / size);
		int maxX = Mathf.FloorToInt((point.X + reach) / size);
		int minY = Mathf.FloorToInt((point.Y - reach) / size);
		int maxY = Mathf.FloorToInt((point.Y + reach) / size);

		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				if (_filledCells.Add((x, y)))
					FillCell(x, y);
			}
		}
	}

	/// <summary>Places fields and lone asteroids in one cell, leaving out any part inside the starting area.</summary>
	private void FillCell(int cellX, int cellY)
	{
		float size = Tuning.AsteroidCellSize;
		Vector2 corner = new(cellX * size, cellY * size);
		float startingArea = StartingAreaRadius;

		Vector2 Pick() => corner + new Vector2(Rng.Value(), Rng.Value()) * size;
		bool OutsideStartingArea(Vector2 spot) => spot.DistanceTo(Tuning.GatewayPosition) > startingArea;

		PlaceFields(Rng.Int(Tuning.AsteroidFieldsPerCellMin, Tuning.AsteroidFieldsPerCellMax), Pick, OutsideStartingArea);
		PlaceLoneAsteroids(Tuning.LoneAsteroidsPerCell, Pick, OutsideStartingArea);
	}

	/// <summary>
	/// Tries to place <paramref name="wanted"/> fields at spots from <paramref name="pick"/>, keeping them apart.
	/// A spot that <paramref name="allowed"/> rejects is skipped rather than retried, so a cell that is
	/// partly inside the starting area gets fewer fields, not the same number squeezed into less space.
	/// </summary>
	private void PlaceFields(int wanted, Func<Vector2> pick, Func<Vector2, bool> allowed)
	{
		for (int n = 0; n < wanted; n++)
		{
			for (int attempt = 0; attempt < 30; attempt++)
			{
				Vector2 center = pick();
				if (!allowed(center))
					break;
				if (NearAField(center, Tuning.AsteroidFieldSpacing))
					continue;
				AddField(center);
				break;
			}
		}
	}

	/// <summary>Like PlaceFields, for lone asteroids: clear of fields and of each other.</summary>
	private void PlaceLoneAsteroids(int wanted, Func<Vector2> pick, Func<Vector2, bool> allowed)
	{
		int first = _count;
		for (int n = 0; n < wanted; n++)
		{
			for (int attempt = 0; attempt < 30; attempt++)
			{
				Vector2 position = pick();
				if (!allowed(position))
					break;

				bool blocked = NearAField(position, Tuning.LoneAsteroidFieldClearance);
				for (int i = first; i < _count && !blocked; i++)
					blocked = _items[i].Position.DistanceTo(position) < Tuning.LoneAsteroidSpacing;
				if (blocked)
					continue;

				float mass = Tuning.LoneAsteroidMassMin
					+ (Tuning.LoneAsteroidMassMax - Tuning.LoneAsteroidMassMin) * Mathf.Pow(Rng.Value(), Tuning.AsteroidSmallBias);
				Add(position, mass, isAsteroid: true, field: -1, offset: default);
				break;
			}
		}
	}

	private bool NearAField(Vector2 point, float distance)
	{
		for (int f = 0; f < _fieldCount; f++)
		{
			if (_fields[f].Center.DistanceTo(point) < distance)
				return true;
		}
		return false;
	}

	/// <summary>Creates a cluster or ring at a point, chooses how it moves, and fills it with asteroids.</summary>
	private void AddField(Vector2 center)
	{
		var field = new Field { Center = center, IsRing = Rng.Value() < Tuning.AsteroidRingChance };
		float motion = Rng.Value();
		if (motion < Tuning.AsteroidFieldMovingChance)
			field.Velocity = Rng.Direction() * Tuning.AsteroidFieldDriftSpeed;
		else if (motion < Tuning.AsteroidFieldMovingChance + Tuning.AsteroidFieldTurningChance)
			field.TurnSpeed = Mathf.DegToRad(Tuning.AsteroidFieldTurnSpeed) * Rng.Sign();

		if (_fieldCount == _fields.Length)
			Array.Resize(ref _fields, _fields.Length * 2);
		_fields[_fieldCount] = field;
		FillField(_fieldCount, field);
		_fieldCount++;
	}

	private void FillField(int fieldIndex, Field field)
	{
		int wanted = field.IsRing
			? Rng.Int(Tuning.AsteroidsPerRingMin, Tuning.AsteroidsPerRingMax)
			: Rng.Int(Tuning.AsteroidsPerClusterMin, Tuning.AsteroidsPerClusterMax);
		float clusterRadius = Rng.Range(Tuning.AsteroidClusterRadiusMin, Tuning.AsteroidClusterRadiusMax);
		float ringRadius = Rng.Range(Tuning.AsteroidRingRadiusMin, Tuning.AsteroidRingRadiusMax);
		int first = _count;

		for (int n = 0; n < wanted; n++)
		{
			float mass = Tuning.AsteroidMassMin
				+ (Tuning.AsteroidMassMax - Tuning.AsteroidMassMin) * Mathf.Pow(Rng.Value(), Tuning.AsteroidSmallBias);
			float radius = AsteroidShapeRadius * Tuning.AsteroidSize * Mathf.Sqrt(mass);

			// Try a few spots, and keep the first that does not overlap an asteroid already placed.
			for (int attempt = 0; attempt < 12; attempt++)
			{
				Vector2 offset;
				if (field.IsRing)
				{
					float angle = (n + Rng.Range(-0.3f, 0.3f)) / wanted * Mathf.Tau;
					float distance = ringRadius + Rng.Range(-0.5f, 0.5f) * Tuning.AsteroidRingWidth;
					offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
				}
				else
				{
					offset = Rng.InsideCircle(clusterRadius);
				}

				if (!Overlaps(first, offset, radius))
				{
					Add(field.Center + offset, mass, isAsteroid: true, fieldIndex, offset);
					break;
				}
			}
		}
	}

	private bool Overlaps(int first, Vector2 offset, float radius)
	{
		for (int i = first; i < _count; i++)
		{
			if (_items[i].Offset.DistanceTo(offset) < RadiusOf(i) + radius + 0.4f)
				return true;
		}
		return false;
	}

	private void Add(Vector2 position, float mass, bool isAsteroid, int field, Vector2 offset)
	{
		if (_count == _items.Length)
			Array.Resize(ref _items, _items.Length * 2);

		bool tumbles = isAsteroid && Rng.Range(0f, 1f) < Tuning.AsteroidTumbleChance;
		_items[_count++] = new Item
		{
			TumbleAxis = tumbles ? RandomAxis() : Vector3.Zero,
			TumbleSpeed = tumbles ? Mathf.DegToRad(Rng.Range(Tuning.AsteroidTumbleSpeedMin, Tuning.AsteroidTumbleSpeedMax)) * Rng.Sign() : 0f,
			TumblePhase = Rng.Range(0f, Mathf.Tau),
			Shape = isAsteroid ? RandomShape() : Basis.Identity,
			Variant = isAsteroid ? Rng.Int(0, _asteroidBatches.Length - 1) : 0,
			Id = ++_lastId,
			Position = position,
			Mass = mass,
			Yaw = Rng.Range(0f, Mathf.Tau),
			IsAsteroid = isAsteroid,
			Field = field,
			Offset = offset,
		};
	}

	/// <summary>A random turn in 3D, with a random stretch along each of the rock's own axes.</summary>
	private static Basis RandomShape()
	{
		var stretch = new Vector3(
			Rng.Range(Tuning.AsteroidStretchMin, Tuning.AsteroidStretchMax),
			Rng.Range(Tuning.AsteroidStretchMin, Tuning.AsteroidStretchMax),
			Rng.Range(Tuning.AsteroidStretchMin, Tuning.AsteroidStretchMax));
		return new Basis(RandomAxis(), Rng.Range(0f, Mathf.Tau)) * Basis.FromScale(stretch);
	}

	/// <summary>A random direction in 3D, spread evenly over the sphere.</summary>
	private static Vector3 RandomAxis()
	{
		float y = Rng.Range(-1f, 1f);
		Vector2 flat = Rng.Direction() * Mathf.Sqrt(1f - y * y);
		return new Vector3(flat.X, y, flat.Y);
	}
}
