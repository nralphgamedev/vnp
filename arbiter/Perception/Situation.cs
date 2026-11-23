using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Arbiter
{
	/// <summary>
	/// Everything an agent perceives at one moment: the bodies around it, the hazards that could hurt it or its
	/// allies, and the zones where its own effects work. The game clears it and fills it in before each decision,
	/// from what a player in the agent's place could see. It is reused from one decision to the next, so filling it
	/// makes no garbage once its lists have grown.
	/// </summary>
	public sealed class Situation
	{
		private readonly List<Body> _bodies = new();
		private readonly List<Hazard> _hazards = new();
		private readonly List<Zone> _zones = new();

		public ReadOnlySpan<Body> Bodies => CollectionsMarshal.AsSpan(_bodies);
		public ReadOnlySpan<Hazard> Hazards => CollectionsMarshal.AsSpan(_hazards);
		public ReadOnlySpan<Zone> Zones => CollectionsMarshal.AsSpan(_zones);

		public void Clear()
		{
			_bodies.Clear();
			_hazards.Clear();
			_zones.Clear();
		}

		public void Add(in Body body) => _bodies.Add(body);
		public void Add(in Hazard hazard) => _hazards.Add(hazard);
		public void Add(in Zone zone) => _zones.Add(zone);

		/// <summary>
		/// Decides which hazards the agent fails to notice this time, as a less skilled player misses things: each
		/// that comes and goes (a shot, an attack being readied) is noticed with <paramref name="chance"/>, drawn from
		/// <paramref name="random"/>. Lasting hazards, such as bodies that hurt by touch, are always noticed. A chance
		/// of 1 or more draws nothing.
		/// </summary>
		public void Overlook(float chance, IRandom random)
		{
			if (chance >= 1f)
				return;
			foreach (ref Hazard hazard in CollectionsMarshal.AsSpan(_hazards))
			{
				bool fleeting = hazard.LandsIn > 0f || !float.IsPositiveInfinity(hazard.Lasts);
				hazard.Overlooked = fleeting && random.Value() >= chance;
			}
		}

		/// <summary>
		/// How much harm reaches <paramref name="point"/> at <paramref name="seconds"/> from now, for whoever
		/// <paramref name="who"/> is, from every hazard that is hurting then and reaches the point padded by
		/// <paramref name="pad"/>. Harm per second counts as one second's worth. A first, simple measure; path
		/// planning weighs hazards along a whole path.
		/// </summary>
		public float HarmAt(Vector2 point, float seconds, float pad, Endangers who)
		{
			float harm = 0f;
			foreach (ref readonly Hazard hazard in Hazards)
			{
				if ((hazard.Endangers & who) != 0 && hazard.Reaches(point, seconds, pad))
					harm += hazard.Harm;
			}
			return harm;
		}
	}
}
