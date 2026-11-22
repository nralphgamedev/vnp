#if RECORDING
using Godot;

/// <summary>How a placed object is shaped, for whatever draws it.</summary>
public enum PlacedShape
{
	/// <summary>A thing at a point, such as a shard, a mine or a missile, acting over Radius around it.</summary>
	Point,

	/// <summary>Only an outline, such as a Shockwave's ring: Position is its centre and Radius its size now.</summary>
	Ring,

	/// <summary>A line from Position to End, such as a beam.</summary>
	Line,
}

/// <summary>
/// Something a VNP Weapon or a Drone Weapon has out in the world right now, reported through their
/// CollectPlaced, for whatever needs to see it, such as the run review's recordings and maps. Read-only data.
/// </summary>
public readonly struct PlacedObject
{
	public readonly Vector2 Position;
	public readonly float Radius;
	public readonly Vector2 End;
	public readonly PlacedShape Shape;

	private PlacedObject(Vector2 position, float radius, Vector2 end, PlacedShape shape)
	{
		Position = position;
		Radius = radius;
		End = end;
		Shape = shape;
	}

	public static PlacedObject Point(Vector2 at, float radius) => new(at, radius, at, PlacedShape.Point);

	public static PlacedObject Ring(Vector2 center, float radius) => new(center, radius, center, PlacedShape.Ring);

	public static PlacedObject Line(Vector2 from, Vector2 to) => new(from, 0f, to, PlacedShape.Line);
}
#endif
