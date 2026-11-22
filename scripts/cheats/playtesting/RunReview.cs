#if TOOLS
using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// The run review's detectors (docs/dev/RUN_REVIEW_SPEC.md): each watches the fight for one kind of moment the
/// designer reported while watching pilots, such as the VNP touching a Goo, and records when it happens. They
/// work from what happens in the fight, not from a pilot's reasoning, so they judge the designer's fights and a
/// pilot's the same way. The fight log runs them every frame and writes what they found.
///
/// Editor only. Detectors only read the fight and draw no random numbers, so a pilot fight plays out exactly
/// the same with them as without.
/// </summary>
public class RunReview
{
	/// <summary>One time a detector fired.</summary>
	public record Moment(double Time, string Detail);

	/// <summary>One kind of moment to watch for. Fires once when its condition has held long enough, then waits
	/// for the condition to be false for RearmSeconds before it can fire again, so one long moment counts once.</summary>
	public class Detector
	{
		public string Id;
		public string Name;
		public readonly List<Moment> Moments = new();
		public double HeldSince = -1;
		public double FalseSince = -1;
		public bool Fired;
	}

	public readonly List<Detector> Detectors = new();

	/// <summary>Told when a detector fires, so it can save a strip of the moment.</summary>
	public ReviewRecorder Recorder;

	private readonly Fight _fight;
	private readonly ReviewSettings _settings;
	private readonly int[] _enemies = new int[1024];
	private readonly int[] _material = new int[256];
	private readonly Detector _touchedGoo, _throughGroup, _closedIn, _indecision, _creeping, _noDrones, _idle, _burst,
		_leftFighting, _notRebuilt;

	// Flew through a group: each enemy passed close, by id, and when.
	private readonly Dictionary<int, double> _passed = new();
	// Indecision: the last direction checked, and when each reversal happened.
	private Vector2 _lastDirection;
	private double _nextDirectionCheck;
	private readonly Queue<double> _reversals = new();
	// Drones lost in a burst: drones lost so far, and when each was lost.
	private int _burstFiredAt;
	private int _dronesLostSoFar;
	private readonly Queue<double> _losses = new();

	public RunReview(Fight fight, ReviewSettings settings)
	{
		_fight = fight;
		_settings = settings;
		_touchedGoo = Add("touched_by_contact", "Touched a contact enemy");
		_throughGroup = Add("through_group", "Flew through a group");
		_closedIn = Add("closed_in", "Closed in on a near enemy");
		_indecision = Add("indecision", "Indecision");
		_creeping = Add("creeping", "Creeping");
		_noDrones = Add("no_drones_fighting", "Fighting without drones");
		_idle = Add("idle", "Idle");
		_burst = Add("drones_lost_burst", "Drones lost in a burst");
		_leftFighting = Add("drones_left_fighting", "Drones left fighting");
		_notRebuilt = Add("drones_not_rebuilt", "Drones not rebuilt");
	}

	private Detector Add(string id, string name)
	{
		var detector = new Detector { Id = id, Name = name };
		Detectors.Add(detector);
		return detector;
	}

	/// <summary>Watches one frame of the fight, at fight time <paramref name="now"/>.</summary>
	public void Update(double now)
	{
		Vnp vnp = _fight.Vnp;
		if (vnp.IsDestroyed || _fight.IsOver && !_fight.IsEndless)
			return;
		ReviewSettings s = _settings;
		EnemySwarm enemies = _fight.Enemies;
		Vector2 me = vnp.PlanePosition;
		Vector2 velocity = vnp.Velocity;
		float topSpeed = Mathf.Max(0.01f, vnp.MoveSpeed);
		float speed = velocity.Length() / topSpeed;

		float reach = Mathf.Max(Mathf.Max(s.IdleEnemyRange, s.IndecisionRange), s.NoDronesRange);
		int count = enemies.CollectNear(me, reach + enemies.MaxRadius, _enemies);
		int nearest = -1;
		float nearestEdge = float.MaxValue;
		bool touching = false;
		int touchedEnemy = -1;
		for (int c = 0; c < count; c++)
		{
			int e = _enemies[c];
			float edge = me.DistanceTo(enemies.PositionOf(e)) - enemies.RadiusOf(e);
			if (edge < nearestEdge)
			{
				nearestEdge = edge;
				nearest = e;
			}
			if (enemies.DamagesByTouchOf(e) && edge <= Tuning.Vnp.HitRadius + s.TouchSlack)
			{
				touching = true;
				touchedEnemy = e;
			}
			if (edge <= s.GroupPassDistance)
				_passed[enemies.IdOf(e)] = now;
		}
		string state = $"health {vnp.Health:0}, drones {_fight.Drones.Count}, level {_fight.Level}";

		// Touched a Goo.
		Check(_touchedGoo, now, touching, 0,
			() => $"{(touchedEnemy >= 0 ? enemies.TypeIdOf(touchedEnemy) : "enemy")} touched the VNP; {state}");

		// Flew through a group: several different enemies passed close within a few seconds.
		var stale = new List<int>();
		foreach (var (id, time) in _passed)
		{
			if (now - time > s.GroupSeconds)
				stale.Add(id);
		}
		foreach (int id in stale)
			_passed.Remove(id);
		Check(_throughGroup, now, _passed.Count >= s.GroupCount && speed >= s.MovingSpeed, 0,
			() => $"passed within {s.GroupPassDistance:0} of {_passed.Count} enemies in {s.GroupSeconds:0}s; {state}");

		// Closed in on a near enemy: moving toward one that is already close.
		bool closing = false;
		if (nearest >= 0 && nearestEdge < s.CloseDistance)
		{
			Vector2 toward = (enemies.PositionOf(nearest) - me).Normalized();
			closing = velocity.Dot(toward) / topSpeed >= s.ClosingSpeed;
		}
		Check(_closedIn, now, closing, s.CloseSeconds,
			() => $"moving toward {enemies.TypeIdOf(nearest)} {nearestEdge:0.0} from its edge; {state}");

		// Indecision: direction reversing back and forth with enemies near.
		if (now >= _nextDirectionCheck)
		{
			_nextDirectionCheck = now + s.ReversalCheckSeconds;
			if (speed >= s.MovingSpeed)
			{
				Vector2 direction = velocity.Normalized();
				if (_lastDirection != Vector2.Zero && Mathf.RadToDeg(_lastDirection.AngleTo(direction)) is float turn
					&& Mathf.Abs(turn) >= s.ReversalDegrees && nearestEdge < s.IndecisionRange)
					_reversals.Enqueue(now);
				_lastDirection = direction;
			}
		}
		while (_reversals.Count > 0 && now - _reversals.Peek() > s.IndecisionSeconds)
			_reversals.Dequeue();
		Check(_indecision, now, _reversals.Count >= s.IndecisionReversals, 0,
			() => $"reversed direction {_reversals.Count} times in {s.IndecisionSeconds:0}s, nearest enemy {nearestEdge:0} away; {state}");

		// Creeping: moving slowly, not harvesting or easing in to harvest.
		bool beaming = _fight.VnpHarvester.Beaming;
		Harvestables harvestables = _fight.Harvestables;
		bool nearMaterial = NearestMaterialEdge(me) <= harvestables.BeamRange + s.CreepHarvestMargin;
		bool creeping = speed > s.CreepSpeedLow && speed < s.CreepSpeedHigh && !beaming && !nearMaterial;
		Check(_creeping, now, creeping, s.CreepSeconds, () => $"moving at {speed * 100:0}% of top speed; {state}");

		// Fighting without drones: staying near enemies with none left, instead of getting away.
		bool stayingNear = false;
		if (_fight.Drones.Count == 0 && nearest >= 0 && nearestEdge < s.NoDronesRange)
		{
			Vector2 away = (me - enemies.PositionOf(nearest)).Normalized();
			stayingNear = velocity.Dot(away) / topSpeed < s.NoDronesFleeSpeed;
		}
		Check(_noDrones, now, stayingNear, s.NoDronesSeconds,
			() => $"no drones, {enemies.TypeIdOf(nearest)} {nearestEdge:0} away; {state}");

		// Idle: neither harvesting nor near a fight.
		bool idle = !beaming && nearestEdge > s.IdleEnemyRange;
		Check(_idle, now, idle, s.IdleSeconds, () => $"no harvesting and no enemy within {s.IdleEnemyRange:0}; {state}");

		// Drones left fighting: an enemy within the drones' reach but away from the VNP, which is leaving it.
		bool leftFighting = false;
		int farEnemy = -1;
		float farEdge = 0f;
		if (_fight.Drones.Count >= s.LeftFightingDrones)
		{
			float dronesReach = Tuning.Drones.EngageRange;
			for (int c = 0; c < count && !leftFighting; c++)
			{
				int e = _enemies[c];
				Vector2 toEnemy = enemies.PositionOf(e) - me;
				float distance = toEnemy.Length();
				float edge = distance - enemies.RadiusOf(e);
				if (distance <= dronesReach && edge > s.LeftFightingDistance && distance > 0.001f
					&& velocity.Dot(-toEnemy / distance) / topSpeed >= s.LeftFightingSpeed)
				{
					leftFighting = true;
					farEnemy = e;
					farEdge = edge;
				}
			}
		}
		Check(_leftFighting, now, leftFighting, s.LeftFightingSeconds,
			() => $"moving away from {enemies.TypeIdOf(farEnemy)} {farEdge:0} away, within the drones' reach; {state}");

		// Drones not rebuilt: few or none for a long while.
		Check(_notRebuilt, now, now > 60 && _fight.Drones.Count <= s.NotRebuiltDrones, s.NotRebuiltSeconds,
			() => $"{_fight.Drones.Count} drones for {s.NotRebuiltSeconds:0}s; {state}");

		// Drones lost in a burst.
		int lost = _fight.Stats.DronesDestroyedTotal + _fight.Stats.DronesAbsorbedTotal;
		for (; _dronesLostSoFar < lost; _dronesLostSoFar++)
			_losses.Enqueue(now);
		while (_losses.Count > 0 && now - _losses.Peek() > s.BurstSeconds)
			_losses.Dequeue();
		bool wasFired = _burst.Fired;
		Check(_burst, now, _losses.Count >= s.BurstDrones, 0,
			() => $"{_losses.Count} drones lost in {s.BurstSeconds:0}s; {state}");
		if (_burst.Fired && !wasFired)
			_burstFiredAt = _losses.Count;
		else if (_burst.Fired && _losses.Count >= _burstFiredAt * Math.Max(1.1f, s.BurstWorsens))
		{
			// Still the same burst, but much worse than when it fired: flagged again, so its worst part gets a strip.
			_burstFiredAt = _losses.Count;
			Fire(_burst, now, $"{_losses.Count} drones lost in {s.BurstSeconds:0}s, worsening; {state}");
		}
	}

	private float NearestMaterialEdge(Vector2 me)
	{
		Harvestables harvestables = _fight.Harvestables;
		int count = harvestables.CollectNear(me, harvestables.BeamRange + _settings.CreepHarvestMargin + 10f, _material);
		float nearest = float.MaxValue;
		for (int c = 0; c < count; c++)
			nearest = Mathf.Min(nearest, harvestables.EdgeDistance(_material[c], me));
		return nearest;
	}

	/// <summary>Fires the detector once its condition has held for <paramref name="holdSeconds"/>, and rearms it
	/// once the condition has been false for RearmSeconds.</summary>
	private void Check(Detector detector, double now, bool condition, float holdSeconds, Func<string> detail)
	{
		if (condition)
		{
			detector.FalseSince = -1;
			if (detector.HeldSince < 0)
				detector.HeldSince = now;
			if (!detector.Fired && now - detector.HeldSince >= holdSeconds)
			{
				detector.Fired = true;
				Fire(detector, now, detail());
			}
			return;
		}
		detector.HeldSince = -1;
		if (detector.FalseSince < 0)
			detector.FalseSince = now;
		if (detector.Fired && now - detector.FalseSince >= _settings.RearmSeconds)
			detector.Fired = false;
	}

	/// <summary>Records a moment for a detector, asks the recorder for its strip, and prints it.</summary>
	private void Fire(Detector detector, double now, string detail)
	{
		var moment = new Moment(Math.Round(now, 2), detail);
		detector.Moments.Add(moment);
		Recorder?.Flag(detector.Id, now);
		GD.Print($"REVIEW: {detector.Name} at {Ui.Clock(now)} (clock {OnScreenClock(now)}): {moment.Detail}");
	}

	/// <summary>The HUD clock at a fight time: counting down to the time limit, then "+" and the time past it.</summary>
	public static string OnScreenClock(double time)
	{
		double clock = Tuning.Fight.TimeLimitSeconds - time;
		return clock >= 0 ? Ui.Clock(Math.Ceiling(clock)) : $"+{Ui.Clock(-clock)}";
	}

	/// <summary>What the detectors found, for the fight log: for each, its name, count, rate per minute of
	/// fight and its moments.</summary>
	public Dictionary<string, object> ToLog(double fightSeconds)
	{
		var detectors = new Dictionary<string, object>();
		foreach (Detector d in Detectors)
		{
			var moments = new List<object>();
			foreach (Moment m in d.Moments)
				moments.Add(new Dictionary<string, object> { ["time"] = m.Time, ["clock"] = OnScreenClock(m.Time), ["detail"] = m.Detail });
			detectors[d.Id] = new Dictionary<string, object>
			{
				["name"] = d.Name,
				["count"] = d.Moments.Count,
				["perMinute"] = fightSeconds > 0 ? Math.Round(d.Moments.Count / fightSeconds * 60, 2) : 0,
				["moments"] = moments,
			};
		}
		return new Dictionary<string, object> { ["detectors"] = detectors };
	}
}
#endif
