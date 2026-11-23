using System;
using System.Numerics;

namespace Arbiter
{
	/// <summary>Whom a hazard can hurt: the agent that is deciding, its allies (whatever follows or fights beside
	/// it), or both.</summary>
	[Flags]
	public enum Endangers
	{
		None = 0,
		Agent = 1,
		Allies = 2,
		Both = Agent | Allies,
	}

	/// <summary>
	/// An area that will hurt what is in it: a body that hurts by touch, a shot in flight, or an attack that is
	/// about to land. A game declares one as soon as a player could see it coming, and no sooner: an attack whose
	/// warning hasn't shown yet isn't declared, so an agent knows no more than a player would.
	/// </summary>
	public struct Hazard
	{
		/// <summary>Where it is, its shape and how it moves.</summary>
		public Area Area;
		/// <summary>Seconds until it starts to hurt: 0 for something that hurts now, more for an attack that is
		/// still being readied.</summary>
		public float LandsIn;
		/// <summary>Seconds it hurts once it has landed. <see cref="float.PositiveInfinity"/> for as long as it
		/// exists, such as a body that hurts by touch.</summary>
		public float Lasts;
		/// <summary>How much it hurts what it reaches: once, on contact, or each second while inside if
		/// <see cref="PerSecond"/>.</summary>
		public float Harm;
		public bool PerSecond;
		public Endangers Endangers;
		/// <summary>The game's ID for what declared it, such as the body it belongs to; 0 for none.</summary>
		public int Source;

		/// <summary>Whether it hurts at <paramref name="seconds"/> from now.</summary>
		public readonly bool ActiveAt(float seconds) => seconds >= LandsIn && seconds <= LandsIn + Lasts;

		/// <summary>Whether it reaches <paramref name="point"/> (padded by <paramref name="pad"/>, such as the size
		/// of whatever is there) at <paramref name="seconds"/> from now, while it is hurting.</summary>
		public readonly bool Reaches(Vector2 point, float seconds, float pad = 0f) =>
			ActiveAt(seconds) && Area.Reaches(point, seconds, pad);

		/// <summary>Something that hurts from now on, for as long as it exists, such as a body that hurts by touch.</summary>
		public static Hazard Lasting(Area area, float harm, Endangers endangers, int source = 0, bool perSecond = false) =>
			new() { Area = area, Harm = harm, Endangers = endangers, Source = source, PerSecond = perSecond, Lasts = float.PositiveInfinity };

		/// <summary>Something that hurts for a while, starting in <paramref name="landsIn"/> seconds, such as a shot
		/// in flight (0, until it fizzles) or an attack being readied.</summary>
		public static Hazard Timed(Area area, float landsIn, float lasts, float harm, Endangers endangers, int source = 0, bool perSecond = false) =>
			new() { Area = area, LandsIn = landsIn, Lasts = lasts, Harm = harm, Endangers = endangers, Source = source, PerSecond = perSecond };
	}
}
