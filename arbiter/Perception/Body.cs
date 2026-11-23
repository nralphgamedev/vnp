using System.Numerics;

namespace Arbiter
{
	/// <summary>
	/// Something around the agent that matters to it, such as an opponent: where it is, how it moves, how big it
	/// is, and tags saying what it does. The tags are bits whose meaning the game decides (for example, "hurts by
	/// touch" or "drains allies"); Arbiter passes them on to the game's own scoring terms without reading them.
	/// What a body can do to the agent is declared separately, as hazards.
	/// </summary>
	public struct Body
	{
		public Vector2 Position;
		/// <summary>Units a second.</summary>
		public Vector2 Velocity;
		public float Radius;
		/// <summary>The game's ID for it, which stays the same for its life.</summary>
		public int Id;
		/// <summary>The game's index for its kind, such as an enemy type.</summary>
		public int Kind;
		/// <summary>What it does, as bits the game defines.</summary>
		public ulong Tags;

		public readonly bool Has(ulong tag) => (Tags & tag) != 0;

		/// <summary>Where it will be after <paramref name="seconds"/>, if it keeps going as it is.</summary>
		public readonly Vector2 PositionAt(float seconds) => Position + Velocity * seconds;
	}
}
