using Godot;
using System;
using System.Linq;
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
		public bool Active;        // Within ActiveRadius of the VNP: kept up to date, findable and drawn.
		public bool IsMimic;       // A sleeping asteroid mimic: an asteroid in every way until it wakes.
	}

	// The asteroid mimic's type, if the fight has one (any entry in enemy_spawning.tres's weights, normally at 0), for
	// hiding mimics in fields and waking them.
	private static AsteroidMimicDefinition Mimic =>
		_mimic ??= Tuning.EnemySpawning.GroupWeights?.Keys.OfType<AsteroidMimicDefinition>().FirstOrDefault();
	private static AsteroidMimicDefinition _mimic;

	// Mimics woken this frame, spawned at the start of the next update, so an enemy is never added while the enemies
	// are updating (a Devourer can wake one by touching it).
	private readonly System.Collections.Generic.List<(Vector2 At, bool Hurt)> _wakes = new();
	private readonly int[] _nearDrones = new int[1];

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
	// Active asteroids and debris this frame, for drawing; and where the slice of activation checks has reached.
	private int _activeAsteroids, _activeDebris;
	private int _activationCursor;
	private AsteroidAmountDefinition _amount;
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

		// Debris can grow past this, from Grey Goo that absorbed many drones. AddDebris widens it when it does.
		_maxRadius = AsteroidShapeRadius * Tuning.Asteroids.Size * Mathf.Sqrt(Tuning.Asteroids.MassMax);

		// The fight's asteroid amount scales how many fields and lone asteroids there are. The fight picks it as it
		// enters the tree, before this is ready.
		_amount = GetParentOrNull<Fight>()?.AsteroidAmount;
		FillStartingArea();	}

#if TOOLS
	/// <summary>Playtest cheat, editor only: removes every asteroid and places them again for another asteroid
	/// amount, as the fight starts, before any debris.</summary>
	public void PlaceAgain(AsteroidAmountDefinition amount)
	{
		_amount = amount;
		_count = 0;
		_fieldCount = 0;
		_filledCells.Clear();
		FillStartingArea();
	}
#endif

	/// <summary>A count from asteroids.tres scaled by the fight's asteroid amount, to the nearest whole number.</summary>
	private static int Scaled(int count, float scale) => Mathf.Max(0, Mathf.FloorToInt(count * scale + 0.5f));

	private float FieldScale => _amount?.FieldScale ?? 1f;
	private float LoneScale => _amount?.LoneScale ?? 1f;

	/// <summary>Leaves a piece of debris at a point on the plane.</summary>
	public void AddDebris(Vector2 position, float mass)
	{
		_maxRadius = Mathf.Max(_maxRadius, DebrisShapeRadius * Tuning.Harvesting.DebrisSize * Mathf.Sqrt(mass));
		Add(position, mass, isAsteroid: false, field: -1, offset: default);
	}

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

	/// <summary>Whether it is an asteroid, rather than an enemy's debris.</summary>
	public bool IsAsteroidOf(int index) => _items[index].IsAsteroid;

	/// <summary>Mass left to harvest, which sets how much fight experience it still gives.</summary>
	public float MassOf(int index) => _items[index].Mass;

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

#if RECORDING
	/// <summary>Fight time the VNP itself last harvested, by its beam or a weapon such as Orbital Shards, so the
	/// run review can tell harvesting apart from travelling whichever way it is done.</summary>
	public double LastVnpHarvestTime { get; private set; } = -1;
#endif

	/// <summary>Draws up to <paramref name="amount"/> mass out and passes it to the fight as harvest.
	/// Returns how much was drawn out, which is less if not that much was left. <paramref name="byVnp"/> marks
	/// harvesting by the VNP itself (its beam or its weapons) rather than its drones, for the run review.</summary>
	public float Harvest(int index, float amount, bool byVnp = false)
	{
		ref Item item = ref _items[index];
		if (item.IsMimic)
		{
			// Trying to harvest a mimic wakes it, hurts it a little, and gives nothing.
			Wake(ref item, hurt: true);
			return 0f;
		}
		float taken = Mathf.Min(amount, item.Mass);
		if (taken <= 0f)
			return 0f;

		item.Mass -= taken;
		item.Glow = GlowTime;
#if RECORDING
		if (byVnp)
			LastVnpHarvestTime = Fight.Elapsed;
#endif
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

	/// <summary>Draws up to <paramref name="amount"/> mass out for an enemy that consumes material, such as a
	/// Devourer: none of it is fight experience. Returns how much was drawn out.</summary>
	public float Consume(int index, float amount)
	{
		ref Item item = ref _items[index];
		if (item.IsMimic)
		{
			Wake(ref item, hurt: false);
			return 0f;
		}
		float taken = Mathf.Min(amount, item.Mass);
		if (taken <= 0f)
			return 0f;
		item.Mass -= taken;
		item.Glow = GlowTime;
		// Used up: it pops. It is cleared away at the start of the next update.
		if (item.Mass <= UsedUp && item.Mass + taken > UsedUp)
			Fight.Effects.MaterialPop(item.Position, item.IsAsteroid ? Tuning.Asteroids.Color : Tuning.Harvesting.DebrisColor,
				item.IsAsteroid ? Tuning.Asteroids.Color : Tuning.Harvesting.DebrisColor);
		return taken;
	}

	// ───────────── Asteroid mimics ─────────────

	/// <summary>Whether the VNP or a drone is within a sleeping mimic's aggro range.</summary>
	private bool WakesUp(Vector2 at)
	{
		float range = Mimic.AggroRange;
		return (!Fight.Vnp.IsDestroyed && Fight.Vnp.PlanePosition.DistanceTo(at) <= range)
			|| Fight.Drones.CollectNear(at, range, _nearDrones) > 0;
	}

	/// <summary>The rock is gone at once, and the mimic appears in its place at the start of the next update.</summary>
	private void Wake(ref Item item, bool hurt)
	{
		item.IsMimic = false;
		item.Mass = 0f; // Cleared away with no pop, and no longer found.
		_wakes.Add((item.Position, hurt));
	}

	private void SpawnWoken()
	{
		foreach ((Vector2 at, bool hurt) in _wakes)
		{
			// Sudden: the rock bursts, and the goo inside is left.
			Fight.Effects.MaterialPop(at, Tuning.Asteroids.Color, Mimic.Color);
			Fight.Effects.EnemyDeath(at, Mimic.Color, 0.8f);
			Fight.Sounds.Play(Sound.MimicWake);
			Fight.Enemies.SpawnAwake(Mimic, at, hurt ? 1f - Mimic.WakeDamageFraction : 1f);
		}
		_wakes.Clear();
	}

	/// <summary>
	/// Maybe hides mimics among a field's asteroids, from <paramref name="first"/> on: with the mimic's FieldChance,
	/// PerFieldMin to PerFieldMax of them. Picked by a generator of its own, seeded from where the field is, so the
	/// fight's own random numbers, and so every fight without mimics, are unchanged.
	/// </summary>
	private void HideMimics(int first, Vector2 center)
	{
		AsteroidMimicDefinition mimic = Mimic;
		int asteroids = _count - first;
		if (mimic == null || mimic.FieldChance <= 0f || asteroids <= 0)
			return;
		var random = new Random(unchecked((int)(center.X * 1009f) * 7919 ^ (int)(center.Y * 1013f) ^ 0x51A7));
		if (random.NextDouble() >= mimic.FieldChance)
			return;
		int hide = Math.Min(asteroids, random.Next(mimic.PerFieldMin, mimic.PerFieldMax + 1));
		for (int h = 0; h < hide; h++)
		{
			// Pick from those not yet mimics.
			int pick = random.Next(asteroids - h);
			for (int i = first; i < _count; i++)
			{
				if (_items[i].IsMimic)
					continue;
				if (pick-- == 0)
				{
					_items[i].IsMimic = true;
					break;
				}
			}
		}
	}

	// ───────────── Each frame ─────────────

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (Fight != null)
		{
			FillArenaAround(Fight.Vnp.PlanePosition);
			SpawnWoken();
		}

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

		// A slice of every asteroid and piece of debris is checked against ActiveRadius each frame. Only active
		// ones are moved with their field, found by queries and drawn; the rest wait, and are placed correctly
		// with their field when they wake. All of the arena's material still counts in AsteroidCount and
		// DebrisCount.
		Vector2 anchor = Anchor;
		ActivateSome(anchor);
		AsteroidCount = 0;
		DebrisCount = 0;
		_activeAsteroids = 0;
		_activeDebris = 0;
		_grid.Clear();
		for (int i = 0; i < _count; i++)
		{
			ref Item item = ref _items[i];
			if (item.IsAsteroid)
				AsteroidCount++;
			else
				DebrisCount++;
			if (!item.Active)
				continue;
			if (item.Field >= 0)
			{
				Field field = _fields[item.Field];
				item.Position = field.Center + item.Offset.Rotated(field.Angle);
			}
			if (item.IsMimic && Fight != null && WakesUp(item.Position))
			{
				Wake(ref item, hurt: false);
				continue;
			}
			if (item.IsAsteroid)
				_activeAsteroids++;
			else
				_activeDebris++;
			item.Glow = Mathf.Max(0f, item.Glow - dt);
			_grid.Insert(i, item.Position);
		}

		Draw();
	}

	/// <summary>Where activity centres: the VNP, or the gateway before the fight has one.</summary>
	private Vector2 Anchor => Fight?.Vnp != null ? Fight.Vnp.PlanePosition : Tuning.Fight.GatewayPosition;

	/// <summary>Whether a spot is within ActiveRadius of the VNP.</summary>
	private bool NearAnchor(Vector2 position) =>
		position.DistanceSquaredTo(Anchor) <= Tuning.Asteroids.ActiveRadius * Tuning.Asteroids.ActiveRadius;

	/// <summary>Checks the next slice of asteroids and debris against ActiveRadius, so all are checked every
	/// ActivationFrames frames.</summary>
	private void ActivateSome(Vector2 anchor)
	{
		if (_count == 0)
			return;
		float radiusSquared = Tuning.Asteroids.ActiveRadius * Tuning.Asteroids.ActiveRadius;
		int slice = Mathf.Max(1, Mathf.CeilToInt(_count / (float)Mathf.Max(1, Tuning.Asteroids.ActivationFrames)));
		for (int k = 0; k < slice && k < _count; k++)
		{
			if (_activationCursor >= _count)
				_activationCursor = 0;
			ref Item item = ref _items[_activationCursor++];
			Vector2 position = item.Position;
			if (item.Field >= 0)
			{
				Field field = _fields[item.Field];
				position = field.Center + item.Offset.Rotated(field.Angle);
			}
			item.Active = position.DistanceSquaredTo(anchor) <= radiusSquared;
		}
	}

	private void Draw()
	{
		for (int v = 0; v < _asteroidBatches.Length; v++)
		{
			_asteroidBatches[v].Begin(_activeAsteroids);
			_variantCounts[v] = 0;
		}
		_debrisBatch.Begin(_activeDebris);
		int debris = 0;
		float time = (float)Fight.Elapsed;

		for (int i = 0; i < _count; i++)
		{
			ref Item item = ref _items[i];
			if (!item.Active)
				continue;
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
		PlaceFields(Rng.Int(Scaled(Tuning.Asteroids.FieldsMin, FieldScale), Scaled(Tuning.Asteroids.FieldsMax, FieldScale)),
			() => gateway + Rng.Direction() * Rng.Range(Tuning.Asteroids.FieldDistanceMin, Tuning.Asteroids.FieldDistanceMax),
			_ => true);
		PlaceLoneAsteroids(Scaled(Tuning.Asteroids.LoneCount, LoneScale),
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

		PlaceFields(Rng.Int(Scaled(Tuning.Asteroids.FieldsPerCellMin, FieldScale), Scaled(Tuning.Asteroids.FieldsPerCellMax, FieldScale)),
			Pick, OutsideStartingArea);
		PlaceLoneAsteroids(Scaled(Tuning.Asteroids.LonePerCell, LoneScale), Pick, OutsideStartingArea);
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
		HideMimics(first, field.Center);
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
			Active = NearAnchor(position),
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
