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

	// Farthest point of the debris mesh from its centre at scale 1. Debris tumbles, so any point can face
	// up; this is used to sit it below the units.
	private const float DebrisExtent = 1f;

	// How far, in degrees, debris and its tumble axis tilt from flat. Kept well short of 90 so a piece
	// never turns edge-on to the camera.
	private const float DebrisWobble = 30f;

	// Rough radius of each outline at scale 1. Used for harvest reach and spacing.
	private const float AsteroidShapeRadius = 0.9f;
	private const float DebrisShapeRadius = 0.8f;

	// Mass below this counts as used up.
	private const float UsedUp = 0.0001f;

	// How long the being-harvested tint lingers after a beam lets go.
	private const float GlowTime = 0.15f;

	public Fight Fight;

	/// <summary>How far a harvest beam reaches past the edge of what it harvests.
	/// Shared by the VNP and its drones, so one upgrade can change both.</summary>
	public float BeamRange = Tuning.Harvesting.BeamRange;

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

		// Each rock shape is its own batch. The rocks are made in code, from fixed seeds so the shapes
		// are the same every fight.
		int variants = Mathf.Max(1, Tuning.Asteroids.ShapeVariants);
		_asteroidBatches = new InstanceBatch[variants];
		_variantCounts = new int[variants];
		Material lit = Placeholders.Material("instanced_lit");
		for (int v = 0; v < variants; v++)
			_asteroidBatches[v] = new InstanceBatch(this, $"AsteroidShapes{v}",
				PlaceholderShapes.Rock(1000 + v * 37, Tuning.Asteroids.Bumpiness), lit);
		_debrisBatch = new InstanceBatch(this, "DebrisShapes", Placeholders.Mesh("debris"), lit);

		_maxRadius = Mathf.Max(
			AsteroidShapeRadius * Tuning.Asteroids.Size * Mathf.Sqrt(Tuning.Asteroids.MassMax),
			DebrisShapeRadius * Tuning.Harvesting.DebrisSize * Mathf.Sqrt(Tuning.EnemySpawning.Enemy.DebrisMassMax));

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
			? AsteroidShapeRadius * Tuning.Asteroids.Size
			: DebrisShapeRadius * Tuning.Harvesting.DebrisSize;
		return shape * Mathf.Sqrt(item.Mass);
	}

	/// <summary>
	/// How far below the plane the centre of an asteroid or piece of debris is drawn. Things can turn any
	/// side up, so they sit low enough that their top stays just below the units, and ships, shots and
	/// beams pass over them. Harvest beams aim at this point, so they end out of sight inside.
	/// </summary>
	public float HeightOf(int index)
	{
		ref Item item = ref _items[index];
		return item.IsAsteroid
			? -0.3f - Tuning.Asteroids.Size * Mathf.Sqrt(item.Mass) * Tuning.Asteroids.StretchMax
			: -0.3f - Tuning.Harvesting.DebrisSize * Mathf.Sqrt(item.Mass) * DebrisExtent;
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
		Fight.AddHarvest(taken, fromDebris: !item.IsAsteroid);
		// Used up: it pops. It is cleared away at the start of the next update.
		if (item.Mass <= UsedUp && item.Mass + taken > UsedUp)
		{
			Fight.Effects.MaterialPop(item.Position, item.IsAsteroid ? Tuning.Asteroids.Color : Tuning.Harvesting.DebrisColor,
				Tuning.Look.ExperienceColor);
			Fight.Sounds.Play(Sound.MaterialPop);
		}
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
			if (_items[i].Mass <= UsedUp)
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
			Color color = item.IsAsteroid ? Tuning.Asteroids.Color : Tuning.Harvesting.DebrisColor;
			if (item.Glow > 0f)
				color = color.Lerp(Tuning.Look.ExperienceColor, 0.6f * item.Glow / GlowTime);

			if (item.IsAsteroid)
			{
				float scale = Tuning.Asteroids.Size * Mathf.Sqrt(item.Mass);

				// Its own turn and stretch, then turning with its field (turning a vector by an angle on
				// the plane is the opposite turn as a yaw), then any tumble on top.
				Basis basis = item.Shape;
				if (item.Field >= 0)
					basis = new Basis(Vector3.Up, -_fields[item.Field].Angle) * basis;
				if (item.TumbleSpeed != 0f)
					basis = new Basis(item.TumbleAxis, item.TumblePhase + item.TumbleSpeed * time) * basis;
				basis = basis.Scaled(Vector3.One * scale);

				int v = item.Variant;
				_asteroidBatches[v].SetBasis(_variantCounts[v]++, item.Position, HeightOf(i), basis, color);
			}
			else
			{
				// Debris always tumbles, like a broken piece drifting.
				float scale = Tuning.Harvesting.DebrisSize * Mathf.Sqrt(item.Mass);
				Basis basis = new Basis(item.TumbleAxis, item.TumblePhase + item.TumbleSpeed * time) * item.Shape;
				_debrisBatch.SetBasis(debris++, item.Position, HeightOf(i), basis.Scaled(Vector3.One * scale), color);
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
	private static float StartingAreaRadius => Mathf.Max(Tuning.Asteroids.FieldDistanceMax, Tuning.Asteroids.LoneDistanceMax);

	/// <summary>Places fields and lone asteroids at random spots around the gateway.</summary>
	private void FillStartingArea()
	{
		Vector2 gateway = Tuning.Fight.GatewayPosition;
		PlaceFields(Rng.Int(Tuning.Asteroids.FieldsMin, Tuning.Asteroids.FieldsMax),
			() => gateway + Rng.Direction() * Rng.Range(Tuning.Asteroids.FieldDistanceMin, Tuning.Asteroids.FieldDistanceMax),
			_ => true);
		PlaceLoneAsteroids(Tuning.Asteroids.LoneCount,
			() => gateway + Rng.Direction() * Rng.Range(Tuning.Asteroids.LoneDistanceMin, Tuning.Asteroids.LoneDistanceMax),
			_ => true);
	}

	/// <summary>Fills every cell within reach of a point that has not been filled yet.</summary>
	private void FillArenaAround(Vector2 point)
	{
		float size = Tuning.Asteroids.CellSize;
		float reach = Tuning.Asteroids.FillDistance;
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
		float size = Tuning.Asteroids.CellSize;
		Vector2 corner = new(cellX * size, cellY * size);
		float startingArea = StartingAreaRadius;

		Vector2 Pick() => corner + new Vector2(Rng.Value(), Rng.Value()) * size;
		bool OutsideStartingArea(Vector2 spot) => spot.DistanceTo(Tuning.Fight.GatewayPosition) > startingArea;

		PlaceFields(Rng.Int(Tuning.Asteroids.FieldsPerCellMin, Tuning.Asteroids.FieldsPerCellMax), Pick, OutsideStartingArea);
		PlaceLoneAsteroids(Tuning.Asteroids.LonePerCell, Pick, OutsideStartingArea);
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
				if (NearAField(center, Tuning.Asteroids.FieldSpacing))
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

				bool blocked = NearAField(position, Tuning.Asteroids.LoneFieldClearance);
				for (int i = first; i < _count && !blocked; i++)
					blocked = _items[i].Position.DistanceTo(position) < Tuning.Asteroids.LoneSpacing;
				if (blocked)
					continue;

				float mass = Tuning.Asteroids.LoneMassMin
					+ (Tuning.Asteroids.LoneMassMax - Tuning.Asteroids.LoneMassMin) * Mathf.Pow(Rng.Value(), Tuning.Asteroids.SmallBias);
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
		var field = new Field { Center = center, IsRing = Rng.Value() < Tuning.Asteroids.RingChance };
		float motion = Rng.Value();
		if (motion < Tuning.Asteroids.FieldMovingChance)
			field.Velocity = Rng.Direction() * Tuning.Asteroids.FieldDriftSpeed;
		else if (motion < Tuning.Asteroids.FieldMovingChance + Tuning.Asteroids.FieldTurningChance)
			field.TurnSpeed = Mathf.DegToRad(Tuning.Asteroids.FieldTurnSpeed) * Rng.Sign();

		if (_fieldCount == _fields.Length)
			Array.Resize(ref _fields, _fields.Length * 2);
		_fields[_fieldCount] = field;
		FillField(_fieldCount, field);
		_fieldCount++;
	}

	private void FillField(int fieldIndex, Field field)
	{
		int wanted = field.IsRing
			? Rng.Int(Tuning.Asteroids.RingCountMin, Tuning.Asteroids.RingCountMax)
			: Rng.Int(Tuning.Asteroids.ClusterCountMin, Tuning.Asteroids.ClusterCountMax);
		float clusterRadius = Rng.Range(Tuning.Asteroids.ClusterRadiusMin, Tuning.Asteroids.ClusterRadiusMax);
		float ringRadius = Rng.Range(Tuning.Asteroids.RingRadiusMin, Tuning.Asteroids.RingRadiusMax);
		int first = _count;

		for (int n = 0; n < wanted; n++)
		{
			float mass = Tuning.Asteroids.MassMin
				+ (Tuning.Asteroids.MassMax - Tuning.Asteroids.MassMin) * Mathf.Pow(Rng.Value(), Tuning.Asteroids.SmallBias);
			float radius = AsteroidShapeRadius * Tuning.Asteroids.Size * Mathf.Sqrt(mass);

			// Try a few spots, and keep the first that does not overlap an asteroid already placed.
			for (int attempt = 0; attempt < 12; attempt++)
			{
				Vector2 offset;
				if (field.IsRing)
				{
					float angle = (n + Rng.Range(-0.3f, 0.3f)) / wanted * Mathf.Tau;
					float distance = ringRadius + Rng.Range(-0.5f, 0.5f) * Tuning.Asteroids.RingWidth;
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

		// Some asteroids tumble; every piece of debris does.
		bool tumbles = !isAsteroid || Rng.Range(0f, 1f) < Tuning.Asteroids.TumbleChance;
		float tumbleSpeed = isAsteroid
			? Rng.Range(Tuning.Asteroids.TumbleSpeedMin, Tuning.Asteroids.TumbleSpeedMax)
			: Rng.Range(Tuning.Harvesting.DebrisTumbleSpeedMin, Tuning.Harvesting.DebrisTumbleSpeedMax);
		_items[_count++] = new Item
		{
			TumbleAxis = !tumbles ? Vector3.Zero : isAsteroid ? RandomAxis() : NearlyUpright(DebrisWobble),
			TumbleSpeed = tumbles ? Mathf.DegToRad(tumbleSpeed) * Rng.Sign() : 0f,
			TumblePhase = Rng.Range(0f, Mathf.Tau),
			Shape = isAsteroid ? RandomShape() : DebrisShape(),
			Variant = isAsteroid ? Rng.Int(0, _asteroidBatches.Length - 1) : 0,
			Id = ++_lastId,
			Position = position,
			Mass = mass,
			IsAsteroid = isAsteroid,
			Field = field,
			Offset = offset,
		};
	}

	/// <summary>A random turn in 3D, with a random stretch along each of the rock's own axes.</summary>
	private static Basis RandomShape()
	{
		var stretch = new Vector3(
			Rng.Range(Tuning.Asteroids.StretchMin, Tuning.Asteroids.StretchMax),
			Rng.Range(Tuning.Asteroids.StretchMin, Tuning.Asteroids.StretchMax),
			Rng.Range(Tuning.Asteroids.StretchMin, Tuning.Asteroids.StretchMax));
		return new Basis(RandomAxis(), Rng.Range(0f, Mathf.Tau)) * Basis.FromScale(stretch);
	}

	/// <summary>
	/// Debris lies roughly flat, turned to a random heading and tilted a little. With its tumble axis also
	/// near upright, it spins and wobbles but never turns fully edge-on, where the plate is hard to see.
	/// </summary>
	private static Basis DebrisShape()
	{
		return new Basis(NearlyUpright(DebrisWobble), Rng.Range(0f, Mathf.Tau))
			* new Basis(Vector3.Right, Mathf.DegToRad(Rng.Range(-DebrisWobble, DebrisWobble)));
	}

	/// <summary>A random direction within <paramref name="degrees"/> of straight up.</summary>
	private static Vector3 NearlyUpright(float degrees)
	{
		float tilt = Mathf.DegToRad(Rng.Range(0f, degrees));
		Vector2 flat = Rng.Direction() * Mathf.Sin(tilt);
		return new Vector3(flat.X, Mathf.Cos(tilt), flat.Y);
	}

	/// <summary>A random direction in 3D, spread evenly over the sphere.</summary>
	private static Vector3 RandomAxis()
	{
		float y = Rng.Range(-1f, 1f);
		Vector2 flat = Rng.Direction() * Mathf.Sqrt(1f - y * y);
		return new Vector3(flat.X, y, flat.Y);
	}
}
