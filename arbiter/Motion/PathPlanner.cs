using System;
using System.Numerics;

namespace Arbiter
{
	/// <summary>
	/// How an agent moves: it steers by a control, a direction with a strength from 0 to 1, as a thumbstick would.
	/// Its velocity follows the control times its top speed at its acceleration, or at its deceleration once the
	/// control is let go. The control itself can take time to swing round (<see cref="ControlRate"/>).
	/// </summary>
	public struct Mover
	{
		public float MaxSpeed;
		/// <summary>Units a second, each second, while steering.</summary>
		public float Acceleration;
		/// <summary>Units a second, each second, once the control is let go.</summary>
		public float Deceleration;
		/// <summary>How fast the control can change, in full lengths a second; 0 changes it at once.</summary>
		public float ControlRate;
	}

	/// <summary>Where an agent is and how it is moving, as a path starts.</summary>
	public struct Motion
	{
		public Vector2 Position;
		public Vector2 Velocity;
		/// <summary>The control it is giving now.</summary>
		public Vector2 Control;
	}

	/// <summary>
	/// Plans short paths on a plane: builds candidate paths, each a steady control held for the look-ahead (a
	/// direction at one of a few strengths, or letting go), predicts where each takes the agent with its own
	/// movement, and assesses each against the hazards in a situation. A game then scores the paths with its own
	/// terms, through a <see cref="Decision"/>, and follows the best until it decides again.
	/// </summary>
	public sealed class PathPlanner
	{
		/// <summary>Seconds each path looks ahead.</summary>
		public float Horizon = 1f;
		/// <summary>Points along each path, evenly in time.</summary>
		public int Steps = 10;
		/// <summary>Directions tried, evenly round the circle.</summary>
		public int Directions = 16;
		/// <summary>Control strengths tried in each direction.</summary>
		public float[] Strengths = { 1f, 0.5f, 0.2f };
		/// <summary>Whether letting go of the control is a candidate too.</summary>
		public bool IncludeStop = true;
		/// <summary>The most hazards it pays attention to, nearest first, as a player can only track so much at
		/// once. It also bounds the cost of assessing in a crowd. 0 for no limit.</summary>
		public int Attention = 64;

		private Vector2[] _points = Array.Empty<Vector2>();
		private Vector2[] _controls = Array.Empty<Vector2>();
		private float[] _harm = Array.Empty<float>();
		private float[] _closeness = Array.Empty<float>();
		private float[] _allyDanger = Array.Empty<float>();
		// Each path's bounding circle, from its start to its end, for skipping hazards that can't come near it.
		private Vector2[] _boundCentre = Array.Empty<Vector2>();
		private float[] _boundRadius = Array.Empty<float>();
		// The hazards worth assessing, nearest first, and the distances of the last area measured, which the next
		// hazard reuses when it has the same area (a body that both hurts by touch and drains allies).
		private int[] _picked = Array.Empty<int>();
		private float[] _pickedKey = Array.Empty<float>();
		private float[] _distances = Array.Empty<float>();
		private bool[] _measured = Array.Empty<bool>();
		private Motion _start;
		private Mover _mover;

		public int Count { get; private set; }
		public Motion Start => _start;
		public float StepSeconds => Horizon / Math.Max(1, Steps);
		/// <summary>The farthest the agent could go in the look-ahead at top speed, for scaling distances.</summary>
		public float MaxTravel => Math.Max(0.001f, _mover.MaxSpeed * Horizon);

		/// <summary>The control a path holds.</summary>
		public Vector2 Control(int path) => _controls[path];
		/// <summary>Where a path has the agent after <paramref name="step"/> + 1 steps.</summary>
		public Vector2 Point(int path, int step) => _points[path * Steps + step];
		public Vector2 End(int path) => _points[path * Steps + Steps - 1];

		/// <summary>Harm the agent would take on a path, from the hazards that hit it (see <see cref="Assess"/>).</summary>
		public float HarmOf(int path) => _harm[path];
		/// <summary>How close a path comes to hazards that could hurt the agent, added over hazards, each from 0
		/// (no nearer than the keep-clear distance) to 1 (touching).</summary>
		public float ClosenessOf(int path) => _closeness[path];
		/// <summary>How much harm reaches the agent's allies along a path, taken as riding within the ally radius of
		/// it: each hazard's harm times how much it overlaps them, averaged over the look-ahead.</summary>
		public float AllyDangerOf(int path) => _allyDanger[path];

		/// <summary>Builds the candidate paths from where the agent is, with how it moves.</summary>
		public void Build(in Motion start, in Mover mover)
		{
			_start = start;
			_mover = mover;
			Count = Directions * Strengths.Length + (IncludeStop ? 1 : 0);
			if (_controls.Length < Count)
			{
				_controls = new Vector2[Count];
				_harm = new float[Count];
				_closeness = new float[Count];
				_allyDanger = new float[Count];
				_boundCentre = new Vector2[Count];
				_boundRadius = new float[Count];
			}
			if (_points.Length < Count * Steps)
			{
				_points = new Vector2[Count * Steps];
				_distances = new float[Count * Steps];
				_measured = new bool[Count];
			}

			int path = 0;
			for (int d = 0; d < Directions; d++)
			{
				float angle = MathF.Tau * d / Directions;
				var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
				foreach (float strength in Strengths)
					Simulate(path++, direction * strength);
			}
			if (IncludeStop)
				Simulate(path, Vector2.Zero);
		}

		private void Simulate(int path, Vector2 control)
		{
			_controls[path] = control;
			float dt = StepSeconds;
			Vector2 position = _start.Position, velocity = _start.Velocity, stick = _start.Control;
			for (int k = 0; k < Steps; k++)
			{
				stick = _mover.ControlRate > 0f ? MoveToward(stick, control, _mover.ControlRate * dt) : control;
				Vector2 wanted = Limit(stick, 1f) * _mover.MaxSpeed;
				float rate = stick.LengthSquared() < 1e-8f ? _mover.Deceleration : _mover.Acceleration;
				velocity = MoveToward(velocity, wanted, rate * dt);
				position += velocity * dt;
				_points[path * Steps + k] = position;
			}
			// A circle round the whole path, start included.
			Vector2 min = _start.Position, max = _start.Position;
			for (int k = 0; k < Steps; k++)
			{
				min = Vector2.Min(min, _points[path * Steps + k]);
				max = Vector2.Max(max, _points[path * Steps + k]);
			}
			_boundCentre[path] = (min + max) * 0.5f;
			_boundRadius[path] = Vector2.Distance(min, max) * 0.5f;
		}

		/// <summary>
		/// Assesses every path against the hazards in <paramref name="situation"/> that the agent noticed: the harm
		/// it would take where a hazard reaches it while hurting (within <paramref name="pad"/>, the agent's own size),
		/// once for a hit, or for each second inside for harm per second; how close it comes, against
		/// <paramref name="keepClear"/>; and the danger to allies riding within <paramref name="allyRadius"/> of it.
		/// Only hazards that could come within reach in the look-ahead are looked at, the nearest up to its
		/// <see cref="Attention"/>, and each only against the paths it could come near, so the cost follows what is
		/// near, not everything there is.
		/// </summary>
		public void Assess(Situation situation, float pad, float keepClear, float allyRadius)
		{
			Array.Clear(_harm, 0, Count);
			Array.Clear(_closeness, 0, Count);
			Array.Clear(_allyDanger, 0, Count);
			float dt = StepSeconds;
			float margin = Math.Max(pad + keepClear, allyRadius);
			float reach = MaxTravel + margin;
			ReadOnlySpan<Hazard> hazards = situation.Hazards;

			// The hazards that could come within reach, nearest first, up to its attention.
			if (_picked.Length < hazards.Length)
			{
				_picked = new int[hazards.Length * 2];
				_pickedKey = new float[hazards.Length * 2];
			}
			int picked = 0;
			for (int h = 0; h < hazards.Length; h++)
			{
				if (hazards[h].Overlooked)
					continue;
				Area area = hazards[h].Area;
				float far = Vector2.Distance(_start.Position, area.Position) - area.Bound - area.Velocity.Length() * Horizon;
				if (far > reach)
					continue;
				_picked[picked] = h;
				_pickedKey[picked] = far;
				picked++;
			}
			if (Attention > 0 && picked > Attention)
			{
				Array.Sort(_pickedKey, _picked, 0, picked);
				picked = Attention;
			}
			// Back in the order they were declared, so hazards sharing an area stay next to each other.
			Array.Sort(_picked, 0, picked);

			bool haveLast = false;
			Area last = default;
			for (int i = 0; i < picked; i++)
			{
				ref readonly Hazard hazard = ref hazards[_picked[i]];
				Area area = hazard.Area;
				bool reuse = haveLast && SameArea(area, last);
				if (!reuse)
					Array.Clear(_measured, 0, Count);
				last = area;
				haveLast = true;
				bool toAgent = (hazard.Endangers & Endangers.Agent) != 0;
				bool toAllies = (hazard.Endangers & Endangers.Allies) != 0 && allyRadius > 0f;
				// Where the hazard can be over the look-ahead, as one circle, to skip paths it can't come near.
				Vector2 sweepCentre = area.Position + area.Velocity * (Horizon * 0.5f);
				float sweepRadius = area.Velocity.Length() * Horizon * 0.5f + area.Bound;
				for (int p = 0; p < Count; p++)
				{
					if (Vector2.Distance(_boundCentre[p], sweepCentre) - _boundRadius[p] - sweepRadius > margin)
						continue;
					if (!_measured[p])
					{
						for (int k = 0; k < Steps; k++)
							_distances[p * Steps + k] = area.DistanceAt(_points[p * Steps + k], (k + 1) * dt);
						_measured[p] = true;
					}
					float nearest = float.PositiveInfinity;
					float inside = 0f;
					float allies = 0f;
					for (int k = 0; k < Steps; k++)
					{
						if (!hazard.ActiveAt((k + 1) * dt))
							continue;
						float distance = _distances[p * Steps + k];
						if (toAgent)
						{
							nearest = Math.Min(nearest, distance);
							if (distance <= pad)
								inside += dt;
						}
						if (toAllies)
							allies += Math.Clamp(1f - Math.Max(0f, distance) / allyRadius, 0f, 1f) * dt;
					}
					if (toAgent)
					{
						if (inside > 0f)
							_harm[p] += hazard.PerSecond ? hazard.Harm * inside : hazard.Harm;
						if (keepClear > 0f && !float.IsPositiveInfinity(nearest))
							_closeness[p] += Math.Clamp(1f - Math.Max(0f, nearest - pad) / keepClear, 0f, 1f);
					}
					if (toAllies)
						_allyDanger[p] += hazard.Harm * allies / Horizon;
				}
			}
		}

		private static bool SameArea(in Area a, in Area b) =>
			a.Shape == b.Shape && a.Position == b.Position && a.Velocity == b.Velocity && a.Extent == b.Extent
			&& a.Radius == b.Radius && a.Inner == b.Inner;

		/// <summary>A path's control as text, for explanations: its heading in degrees and its strength.</summary>
		public string Describe(int path)
		{
			Vector2 c = _controls[path];
			if (c.LengthSquared() < 1e-8f)
				return "stop";
			float degrees = MathF.Atan2(c.Y, c.X) * 180f / MathF.PI;
			return $"{(degrees < 0 ? degrees + 360 : degrees):0}deg x{c.Length():0.0}";
		}

		private static Vector2 MoveToward(Vector2 from, Vector2 to, float delta)
		{
			Vector2 difference = to - from;
			float length = difference.Length();
			return length <= delta || length < 1e-8f ? to : from + difference / length * delta;
		}

		private static Vector2 Limit(Vector2 v, float length)
		{
			float l = v.Length();
			return l > length ? v / l * length : v;
		}
	}
}
