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
		public bool IsAsteroid;    // An asteroid, as opposed to debris.
		public int Field;          // Its asteroid field, or -1 for a lone asteroid or debris.
		public Vector2 Offset;     // Position relative to its field's center, before the field turns.
		public float Glow;         // Seconds left of the tint that shows it is being harvested.
	}

	// Placeholder silhouettes: asteroids are rounded lumps, debris is a jagged shard.
	private static readonly float[] AsteroidLumps = { 1.0f, 0.84f, 0.95f, 0.8f, 1.0f, 0.9f, 0.76f, 0.97f, 0.86f };
	private static readonly Vector2[] DebrisOutline =
	{
		new(-0.9f, -0.3f), new(-0.2f, -0.5f), new(0.1f, -1.0f), new(0.4f, -0.3f), new(1.0f, -0.1f),
		new(0.3f, 0.3f), new(0.5f, 0.9f), new(-0.2f, 0.4f), new(-0.7f, 0.6f), new(-0.5f, 0.1f),
	};

	private const float AsteroidThickness = 0.8f;
	private const float DebrisThickness = 0.45f;

	// Rough radius of each outline at scale 1. Used for harvest reach and spacing.
	private const float AsteroidShapeRadius = 0.9f;
	private const float DebrisShapeRadius = 0.8f;

	// How long the being-harvested tint lingers after a beam lets go.
	private const float GlowTime = 0.15f;

	public Fight Fight;

	/// <summary>How far a harvest beam reaches past the edge of what it harvests.
	/// Shared by the VNP and its probes, so one upgrade can change both.</summary>
	public float BeamRange = Tuning.HarvestBeamRange;

	public int AsteroidCount { get; private set; }
	public int DebrisCount { get; private set; }
	public int FieldCount => _fields.Length;
	public Field GetField(int index) => _fields[index];

	private Item[] _items = new Item[512];
	private int _count;
	private Field[] _fields = Array.Empty<Field>();
	private int _lastId;
	private float _maxRadius;
	private readonly SpatialGrid _grid = new(8f);
	private InstanceBatch _asteroidBatch;
	private InstanceBatch _debrisBatch;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Harvestables;

		var asteroidOutline = new Vector2[AsteroidLumps.Length];
		for (int i = 0; i < AsteroidLumps.Length; i++)
		{
			float angle = i * Mathf.Tau / AsteroidLumps.Length;
			asteroidOutline[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * AsteroidLumps[i];
		}
		_asteroidBatch = new InstanceBatch(this, "AsteroidShapes",
			Placeholder.ExtrudedShape(asteroidOutline, AsteroidThickness), Placeholder.InstanceMaterial());
		_debrisBatch = new InstanceBatch(this, "DebrisShapes",
			Placeholder.ExtrudedShape(DebrisOutline, DebrisThickness), Placeholder.InstanceMaterial());

		_maxRadius = Mathf.Max(
			AsteroidShapeRadius * Tuning.AsteroidSize * Mathf.Sqrt(Tuning.AsteroidMassMax),
			DebrisShapeRadius * Tuning.DebrisSize * Mathf.Sqrt(Tuning.DebrisMassMax));

		CreateFields();
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

		for (int f = 0; f < _fields.Length; f++)
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
		_asteroidBatch.Begin(AsteroidCount);
		_debrisBatch.Begin(DebrisCount);
		int asteroids = 0;
		int debris = 0;

		for (int i = 0; i < _count; i++)
		{
			ref Item item = ref _items[i];
			bool isAsteroid = item.IsAsteroid;
			float scale = (isAsteroid ? Tuning.AsteroidSize : Tuning.DebrisSize) * Mathf.Sqrt(item.Mass);
			float thickness = isAsteroid ? AsteroidThickness : DebrisThickness;

			// Keep the top just below the units, so ships, shots and beams pass over it.
			float height = -0.3f - thickness * scale * 0.5f;

			Color color = isAsteroid ? Tuning.AsteroidColor : Tuning.DebrisColor;
			if (item.Glow > 0f)
				color = color.Lerp(Tuning.ExperienceColor, 0.6f * item.Glow / GlowTime);

			if (isAsteroid)
			{
				// Asteroids in a turning field turn with it. Turning a vector by an angle on the plane
				// is the opposite turn as a yaw.
				float yaw = item.Field >= 0 ? item.Yaw - _fields[item.Field].Angle : item.Yaw;
				_asteroidBatch.Set(asteroids++, item.Position, height, yaw, scale, color);
			}
			else
			{
				_debrisBatch.Set(debris++, item.Position, height, item.Yaw, scale, color);
			}
		}

		_asteroidBatch.Commit(asteroids);
		_debrisBatch.Commit(debris);
	}

	// ───────────── Building the fields ─────────────

	/// <summary>Places the asteroid fields at random spots around the gateway, keeping them apart.</summary>
	private void CreateFields()
	{
		int wanted = Rng.Int(Tuning.AsteroidFieldsMin, Tuning.AsteroidFieldsMax);
		var fields = new List<Field>();

		for (int attempt = 0; fields.Count < wanted && attempt < wanted * 50; attempt++)
		{
			Vector2 center = Tuning.GatewayPosition
				+ Rng.Direction() * Rng.Range(Tuning.AsteroidFieldDistanceMin, Tuning.AsteroidFieldDistanceMax);

			bool tooClose = false;
			foreach (Field other in fields)
				tooClose |= other.Center.DistanceTo(center) < Tuning.AsteroidFieldSpacing;
			if (tooClose)
				continue;

			var field = new Field { Center = center, IsRing = Rng.Value() < Tuning.AsteroidRingChance };
			float motion = Rng.Value();
			if (motion < Tuning.AsteroidFieldMovingChance)
				field.Velocity = Rng.Direction() * Tuning.AsteroidFieldDriftSpeed;
			else if (motion < Tuning.AsteroidFieldMovingChance + Tuning.AsteroidFieldTurningChance)
				field.TurnSpeed = Mathf.DegToRad(Tuning.AsteroidFieldTurnSpeed) * Rng.Sign();

			fields.Add(field);
			FillField(fields.Count - 1, field);
		}

		_fields = fields.ToArray();
		CreateLoneAsteroids();
	}

	/// <summary>Scatters single asteroids around the gateway, clear of the fields and of each other.</summary>
	private void CreateLoneAsteroids()
	{
		int first = _count;
		int placed = 0;
		for (int attempt = 0; placed < Tuning.LoneAsteroidCount && attempt < Tuning.LoneAsteroidCount * 50; attempt++)
		{
			Vector2 position = Tuning.GatewayPosition
				+ Rng.Direction() * Rng.Range(Tuning.LoneAsteroidDistanceMin, Tuning.LoneAsteroidDistanceMax);

			bool blocked = false;
			foreach (Field field in _fields)
				blocked |= field.Center.DistanceTo(position) < Tuning.LoneAsteroidFieldClearance;
			for (int i = first; i < _count && !blocked; i++)
				blocked |= _items[i].Position.DistanceTo(position) < Tuning.LoneAsteroidSpacing;
			if (blocked)
				continue;

			float mass = Tuning.LoneAsteroidMassMin
				+ (Tuning.LoneAsteroidMassMax - Tuning.LoneAsteroidMassMin) * Mathf.Pow(Rng.Value(), Tuning.AsteroidSmallBias);
			Add(position, mass, isAsteroid: true, field: -1, offset: default);
			placed++;
		}
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

		_items[_count++] = new Item
		{
			Id = ++_lastId,
			Position = position,
			Mass = mass,
			Yaw = Rng.Range(0f, Mathf.Tau),
			IsAsteroid = isAsteroid,
			Field = field,
			Offset = offset,
		};
	}
}
