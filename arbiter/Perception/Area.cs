using System;
using System.Numerics;

namespace Arbiter
{
	/// <summary>The shape of an area on the plane.</summary>
	public enum AreaShape
	{
		/// <summary>Everything within <see cref="Area.Radius"/> of <see cref="Area.Position"/>.</summary>
		Circle,
		/// <summary>Everything within <see cref="Area.Radius"/> of the segment from <see cref="Area.Position"/>
		/// along <see cref="Area.Extent"/>: a line with width, such as a charge or a beam.</summary>
		Capsule,
		/// <summary>A wedge from <see cref="Area.Position"/> along <see cref="Area.Extent"/>, whose length is its
		/// reach, opening by <see cref="Area.Inner"/> radians either side: a spread or a blast.</summary>
		Cone,
		/// <summary>Everything between <see cref="Area.Inner"/> and <see cref="Area.Radius"/> from
		/// <see cref="Area.Position"/>: a band, such as an expanding wave or an orbit.</summary>
		Ring,
	}

	/// <summary>
	/// An area on the plane that may be moving: a shape at a position, carried along at a velocity. Hazards and
	/// zones are both areas. Distances are negative inside the area, so a caller can ask both whether a point is
	/// inside and how close it comes.
	/// </summary>
	public struct Area
	{
		public AreaShape Shape;
		/// <summary>The centre of a circle or ring, the start of a capsule, or the point of a cone.</summary>
		public Vector2 Position;
		/// <summary>How the whole area moves, in units a second.</summary>
		public Vector2 Velocity;
		/// <summary>A capsule: from its start to its end. A cone: its direction, with its reach as length.</summary>
		public Vector2 Extent;
		/// <summary>A circle's or ring's outer radius, or a capsule's half-width.</summary>
		public float Radius;
		/// <summary>A ring's inner radius, or a cone's half-angle in radians.</summary>
		public float Inner;

		public static Area Circle(Vector2 centre, float radius, Vector2 velocity = default) =>
			new() { Shape = AreaShape.Circle, Position = centre, Radius = radius, Velocity = velocity };

		public static Area Capsule(Vector2 from, Vector2 to, float halfWidth, Vector2 velocity = default) =>
			new() { Shape = AreaShape.Capsule, Position = from, Extent = to - from, Radius = halfWidth, Velocity = velocity };

		public static Area Cone(Vector2 apex, Vector2 directionWithReach, float halfAngle, Vector2 velocity = default) =>
			new() { Shape = AreaShape.Cone, Position = apex, Extent = directionWithReach, Inner = halfAngle, Velocity = velocity };

		public static Area Ring(Vector2 centre, float inner, float outer, Vector2 velocity = default) =>
			new() { Shape = AreaShape.Ring, Position = centre, Inner = inner, Radius = outer, Velocity = velocity };

		/// <summary>Where its <see cref="Position"/> is after <paramref name="seconds"/>.</summary>
		public readonly Vector2 PositionAt(float seconds) => Position + Velocity * seconds;

		/// <summary>The same area moved by an offset, such as one declared around the origin for an agent that
		/// carries it with it.</summary>
		public readonly Area Moved(Vector2 offset)
		{
			Area moved = this;
			moved.Position += offset;
			return moved;
		}

		/// <summary>How far <paramref name="point"/> is from the area after <paramref name="seconds"/>: negative
		/// inside, as deep as it is inside.</summary>
		public readonly float DistanceAt(Vector2 point, float seconds)
		{
			Vector2 at = PositionAt(seconds);
			switch (Shape)
			{
				case AreaShape.Circle:
					return Vector2.Distance(point, at) - Radius;
				case AreaShape.Capsule:
					return DistanceToSegment(point, at, at + Extent) - Radius;
				case AreaShape.Ring:
				{
					float d = Vector2.Distance(point, at);
					return MathF.Max(Inner - d, d - Radius);
				}
				case AreaShape.Cone:
					return ConeDistance(point - at, Extent, Inner);
				default:
					return float.PositiveInfinity;
			}
		}

		/// <summary>Whether <paramref name="point"/>, or anything within <paramref name="pad"/> of it, is inside
		/// the area after <paramref name="seconds"/>.</summary>
		public readonly bool Reaches(Vector2 point, float seconds, float pad = 0f) => DistanceAt(point, seconds) <= pad;

		/// <summary>A distance that bounds the whole area from its <see cref="Position"/>, for quick rejection.</summary>
		public readonly float Bound => Shape switch
		{
			AreaShape.Capsule => Extent.Length() + Radius,
			AreaShape.Cone => Extent.Length(),
			_ => Radius,
		};

		public static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
		{
			Vector2 along = to - from;
			float lengthSquared = along.LengthSquared();
			if (lengthSquared < 1e-8f)
				return Vector2.Distance(point, from);
			float t = Math.Clamp(Vector2.Dot(point - from, along) / lengthSquared, 0f, 1f);
			return Vector2.Distance(point, from + along * t);
		}

		/// <summary>Distance from a point, relative to the cone's point, to a cone along <paramref name="axis"/>
		/// opening by <paramref name="halfAngle"/> either side. Negative inside.</summary>
		private static float ConeDistance(Vector2 point, Vector2 axis, float halfAngle)
		{
			float reach = axis.Length();
			if (reach < 1e-6f)
				return point.Length();
			Vector2 direction = axis / reach;
			float distance = point.Length();
			float angle = distance < 1e-6f ? 0f : MathF.Acos(Math.Clamp(Vector2.Dot(point / distance, direction), -1f, 1f));
			if (angle <= halfAngle && distance <= reach)
			{
				// Inside: as deep as the nearer of the far edge and the sides.
				float toSide = distance * MathF.Sin(MathF.Min(MathF.PI / 2f, halfAngle - angle));
				return -MathF.Min(reach - distance, toSide);
			}
			// Outside: the nearest of the two sides and the far edge.
			Vector2 left = Rotate(direction, halfAngle) * reach;
			Vector2 right = Rotate(direction, -halfAngle) * reach;
			float best = MathF.Min(DistanceToSegment(point, Vector2.Zero, left), DistanceToSegment(point, Vector2.Zero, right));
			if (angle <= halfAngle)
				best = MathF.Min(best, distance - reach);
			return best;
		}

		private static Vector2 Rotate(Vector2 v, float radians)
		{
			float c = MathF.Cos(radians), s = MathF.Sin(radians);
			return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
		}
	}
}
