namespace Arbiter
{
	/// <summary>What a zone is for.</summary>
	public enum ZoneKind
	{
		/// <summary>Where the agent's attacks reach others, such as a weapon's range around it.</summary>
		Reach,
		/// <summary>A place that hurts others who cross it, such as a mine the agent left, worth leading them over.</summary>
		Trap,
	}

	/// <summary>
	/// Where the agent's own effects work. A zone that <see cref="FollowsAgent"/> is declared around the origin and
	/// moves with the agent, such as a weapon's reach; otherwise it stays where it is, such as a trap.
	/// </summary>
	public struct Zone
	{
		public ZoneKind Kind;
		public Area Area;
		public bool FollowsAgent;
		/// <summary>The game's ID for what declared it, such as which of the agent's weapons; 0 for none.</summary>
		public int Source;

		/// <summary>An agent's reach: a circle of <paramref name="radius"/> around it.</summary>
		public static Zone ReachAround(float radius, int source = 0) =>
			new() { Kind = ZoneKind.Reach, Area = Area.Circle(default, radius), FollowsAgent = true, Source = source };

		/// <summary>An agent's reach as a band around it, from <paramref name="inner"/> to <paramref name="outer"/>,
		/// such as an orbit.</summary>
		public static Zone ReachBand(float inner, float outer, int source = 0) =>
			new() { Kind = ZoneKind.Reach, Area = Area.Ring(default, inner, outer), FollowsAgent = true, Source = source };

		/// <summary>A trap left in place.</summary>
		public static Zone Trap(Area area, int source = 0) => new() { Kind = ZoneKind.Trap, Area = area, Source = source };
	}
}
