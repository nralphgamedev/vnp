#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Records what the run review's maps are drawn from (docs/RUN_REVIEW_SPEC.md): the VNP's track through the
/// whole fight, what it was doing and the events along the way, for the overview map; and snapshots of the
/// fight around the VNP about a second apart, kept for the last few seconds, so that when a detector fires a
/// strip of what led up to it, and a little after, can be saved for a strip map.
///
/// Editor only. It only reads the fight and draws no random numbers, so a fight plays the same with it.
/// </summary>
public class ReviewRecorder
{
	/// <summary>What the VNP was doing, judged from what can be seen, so it works for the designer too.</summary>
	public enum Activity { Travelling, Harvesting, Fighting, MovingAway }

	private class Strip
	{
		public string Reason;
		public double Time;
		public readonly List<Dictionary<string, object>> Frames = new();
		public int AfterLeft;
		/// <summary>Seconds between its pictures, when not the settings' StripSpacing.</summary>
		public double? Spacing;
		/// <summary>False for a stretch of the fight asked for as FROM-TO, which has no single moment to highlight.</summary>
		public bool HasMoment = true;
	}

	/// <summary>A strip asked for before the fight, by the scenario harness's strip= option, for replaying a pilot
	/// fight. Since it is known ahead, its pictures are taken on its own schedule rather than from the snapshots
	/// kept for detectors: centred exactly on its moment, or across a stretch, at any spacing.</summary>
	public class StripRequest
	{
		public double Time;
		public double? End;
		public double? Spacing;

		/// <summary>Reads one of strip='s comma-separated entries: T (a moment), T:S (a moment, pictures S seconds
		/// apart), FROM-TO (a stretch) or FROM-TO:S.</summary>
		public static StripRequest Parse(string text)
		{
			var culture = System.Globalization.CultureInfo.InvariantCulture;
			var request = new StripRequest();
			string[] parts = text.Split(':');
			if (parts.Length > 1)
				request.Spacing = Math.Max(1.0 / 60, double.Parse(parts[1], culture));
			string[] range = parts[0].Split('-');
			request.Time = double.Parse(range[0], culture);
			if (range.Length > 1)
				request.End = double.Parse(range[1], culture);
			return request;
		}
	}

	private class Scheduled
	{
		public Strip Strip;
		public double Next, Last, Spacing;
		public bool EventNoted;
	}

	private readonly Fight _fight;
	private readonly ReviewSettings _settings;
	private readonly int[] _enemies = new int[1024];
	private readonly int[] _material = new int[512];
	private readonly List<string> _enemyTypes = new();
	private readonly List<Dictionary<string, object>> _enemyTypeInfo = new();

	private readonly List<float[]> _track = new();
	private readonly List<Dictionary<string, object>> _events = new();
	private double _nextTrack;
	private float _lastHealth = -1;
	private int _lastLevel = 1;
	private int _lastDronesLost;

	private readonly Queue<Dictionary<string, object>> _recent = new();
	private double _nextSnapshot;
	private readonly List<Strip> _pending = new();
	private readonly List<Strip> _strips = new();
	private readonly List<Scheduled> _scheduled = new();
	private bool _endTaken;

	/// <summary>Most pictures in one asked-for strip, so a long stretch at a fine spacing can't make a huge image.</summary>
	private const int MaxRequestedFrames = 48;

	public ReviewRecorder(Fight fight, ReviewSettings settings)
	{
		_fight = fight;
		_settings = settings;
	}

	/// <summary>Strips to take as well as when detectors fire, such as a moment Claude spotted on an overview map.
	/// A moment gets the settings' StripBefore pictures before it and StripAfter after; a stretch gets pictures
	/// from its start to its end.</summary>
	public void RequestStrips(IEnumerable<StripRequest> requests)
	{
		foreach (StripRequest r in requests)
		{
			double spacing = r.Spacing ?? Math.Max(0.1, _settings.StripSpacing);
			double from = r.End.HasValue ? Math.Min(r.Time, r.End.Value) : r.Time - Math.Max(0, _settings.StripBefore) * spacing;
			double to = r.End.HasValue ? Math.Max(r.Time, r.End.Value) : r.Time + Math.Max(1, _settings.StripAfter) * spacing;
			if ((to - from) / spacing + 1 > MaxRequestedFrames)
				spacing = (to - from) / (MaxRequestedFrames - 1);
			var strip = new Strip { Reason = "requested", Time = Math.Round(r.Time, 2), HasMoment = !r.End.HasValue,
				Spacing = r.Spacing.HasValue || spacing != _settings.StripSpacing ? spacing : null };
			_scheduled.Add(new Scheduled { Strip = strip, Next = Math.Max(0, from), Last = to + 1e-6, Spacing = spacing });
		}
	}

	/// <summary>Records one frame of the fight, at fight time <paramref name="now"/>.</summary>
	public void Update(double now)
	{
		Vnp vnp = _fight.Vnp;
		if (vnp.IsDestroyed)
			return;
		ReviewSettings s = _settings;

		// Events: hits taken, drones lost and level-ups, from changes since the last frame.
		if (_lastHealth >= 0 && vnp.Health < _lastHealth - 0.01f)
			Event(now, "hit", Math.Round(_lastHealth - vnp.Health, 1));
		_lastHealth = vnp.Health;
		int lost = _fight.Stats.DronesDestroyedTotal + _fight.Stats.DronesAbsorbedTotal;
		if (lost > _lastDronesLost)
			Event(now, "drones_lost", lost - _lastDronesLost);
		_lastDronesLost = lost;
		if (_fight.Level > _lastLevel)
			Event(now, "level", _fight.Level);
		_lastLevel = _fight.Level;

		if (now >= _nextTrack)
		{
			_nextTrack = now + Math.Max(0.05, s.TrackSeconds);
			Vector2 p = vnp.PlanePosition;
			_track.Add(new[] { (float)Math.Round(now, 2), Round(p.X), Round(p.Y), (float)(int)ActivityNow(),
				(float)Math.Round(vnp.Velocity.Length() / Mathf.Max(0.01f, vnp.MoveSpeed), 2) });
		}

		foreach (Scheduled r in _scheduled)
		{
			if (!r.EventNoted && now >= r.Strip.Time)
			{
				r.EventNoted = true;
				Event(now, "detector", "requested");
			}
			if (now < r.Next || now > r.Last)
				continue;
			if (r.Strip.Frames.Count == 0)
				_strips.Add(r.Strip);
			r.Strip.Frames.Add(Snapshot(now));
			// Kept to its own cadence, skipping any picture times a long frame jumped past.
			while (r.Next <= now)
				r.Next += r.Spacing;
		}

		if (now >= _nextSnapshot)
		{
			_nextSnapshot = now + Math.Max(0.1, s.StripSpacing);
			Dictionary<string, object> snapshot = Snapshot(now);
			_recent.Enqueue(snapshot);
			while (_recent.Count > Math.Max(0, s.StripBefore) + 1)
				_recent.Dequeue();
			for (int i = _pending.Count - 1; i >= 0; i--)
			{
				Strip strip = _pending[i];
				strip.Frames.Add(snapshot);
				if (--strip.AfterLeft <= 0)
				{
					_pending.RemoveAt(i);
					_strips.Add(strip);
				}
			}
		}
	}

	/// <summary>The fight ended: if the VNP was destroyed, one last picture of that moment for every strip still being
	/// taken, so a strip of how a fight ended reaches its end rather than stopping up to a picture's spacing short
	/// of it. Called by the fight log as the fight ends, since the fight pauses for the result screen in the same
	/// frame, before the next Update.</summary>
	public void FightEnded(double now)
	{
		if (_endTaken || !_fight.Vnp.IsDestroyed)
			return;
		_endTaken = true;
		Dictionary<string, object> last = Snapshot(now);
		foreach (Strip strip in _pending)
			AddLast(strip, last);
		foreach (Scheduled r in _scheduled)
		{
			if (r.Strip.Frames.Count > 0 && now <= r.Last)
				AddLast(r.Strip, last);
		}
	}

	/// <summary>Adds the picture of the VNP's destruction, unless a detector firing in the same frame already took one.</summary>
	private static void AddLast(Strip strip, Dictionary<string, object> last)
	{
		if (strip.Frames.Count == 0 || (double)strip.Frames[^1]["time"] < (double)last["time"])
			strip.Frames.Add(last);
	}

	/// <summary>A detector fired (or a strip was asked for): note it on the track, and start a strip from the
	/// last few seconds of snapshots, finished over the next few.</summary>
	public void Flag(string reason, double now)
	{
		Event(now, "detector", reason);
		if (_strips.Count + _pending.Count >= _settings.MaxStripsPerFight && reason != "requested")
			return;
		var strip = new Strip { Reason = reason, Time = Math.Round(now, 2), AfterLeft = Math.Max(1, _settings.StripAfter) };
		strip.Frames.AddRange(_recent);
		// A picture of the moment itself, since the last kept snapshot can be up to StripSpacing before it.
		if (_recent.Count == 0 || (double)_recent.Last()["time"] < Math.Round(now, 2))
			strip.Frames.Add(Snapshot(now));
		_pending.Add(strip);
	}

	private void Event(double now, string kind, object value) =>
		_events.Add(new Dictionary<string, object>
		{
			["time"] = Math.Round(now, 2), ["kind"] = kind, ["value"] = value,
			["x"] = Round(_fight.Vnp.PlanePosition.X), ["y"] = Round(_fight.Vnp.PlanePosition.Y),
		});

	private Activity ActivityNow()
	{
		Vnp vnp = _fight.Vnp;
		if (_fight.VnpHarvester.Beaming)
			return Activity.Harvesting;
		EnemySwarm enemies = _fight.Enemies;
		Vector2 me = vnp.PlanePosition;
		int count = enemies.CollectNear(me, _settings.FightReach + enemies.MaxRadius, _enemies);
		int nearest = -1;
		float nearestEdge = float.MaxValue;
		for (int c = 0; c < count; c++)
		{
			float edge = me.DistanceTo(enemies.PositionOf(_enemies[c])) - enemies.RadiusOf(_enemies[c]);
			if (edge < nearestEdge)
			{
				nearestEdge = edge;
				nearest = _enemies[c];
			}
		}
		if (nearest < 0 || nearestEdge > _settings.FightReach)
			return Activity.Travelling;
		Vector2 away = (me - enemies.PositionOf(nearest)).Normalized();
		return vnp.Velocity.Dot(away) / Mathf.Max(0.01f, vnp.MoveSpeed) >= _settings.MovingAwaySpeed
			? Activity.MovingAway : Activity.Fighting;
	}

	/// <summary>The fight around the VNP now, within the area a strip map shows.</summary>
	private Dictionary<string, object> Snapshot(double now)
	{
		Vnp vnp = _fight.Vnp;
		Vector2 me = vnp.PlanePosition;
		float reach = _settings.StripView * 0.75f;

		var enemyList = new List<float[]>();
		EnemySwarm enemies = _fight.Enemies;
		int count = enemies.CollectNear(me, reach + enemies.MaxRadius, _enemies);
		for (int c = 0; c < count; c++)
		{
			int e = _enemies[c];
			Vector2 p = enemies.PositionOf(e);
			enemyList.Add(new[] { Round(p.X), Round(p.Y), Round(enemies.RadiusOf(e)), TypeIndex(enemies, e) });
		}

		var droneList = new List<float[]>();
		DroneSwarm drones = _fight.Drones;
		for (int i = 0; i < drones.Count; i++)
		{
			Vector2 p = drones.PositionOf(i);
			if (p.DistanceSquaredTo(me) <= reach * reach)
				droneList.Add(new[] { Round(p.X), Round(p.Y) });
		}

		var materialList = new List<float[]>();
		Harvestables harvestables = _fight.Harvestables;
		int materialCount = harvestables.CollectNear(me, reach, _material);
		for (int c = 0; c < materialCount; c++)
		{
			int m = _material[c];
			Vector2 p = harvestables.PositionOf(m);
			materialList.Add(new[] { Round(p.X), Round(p.Y), Round(harvestables.RadiusOf(m)), harvestables.IsAsteroidOf(m) ? 1f : 0f });
		}

		var shotList = new List<float[]>();
		Projectiles projectiles = _fight.Projectiles;
		for (int i = 0; i < projectiles.Count; i++)
		{
			if (!projectiles.IsEnemyShot(i))
				continue;
			Vector2 p = projectiles.PositionOf(i);
			if (p.DistanceSquaredTo(me) > reach * reach)
				continue;
			Vector2 v = projectiles.VelocityOf(i);
			shotList.Add(new[] { Round(p.X), Round(p.Y), Round(v.X), Round(v.Y) });
		}

		return new Dictionary<string, object>
		{
			["time"] = Math.Round(now, 2),
			["vnp"] = new[] { Round(me.X), Round(me.Y), Round(vnp.Velocity.X), Round(vnp.Velocity.Y) },
			["health"] = Math.Round(vnp.Health, 1),
			["drones"] = drones.Count,
			["level"] = _fight.Level,
			["enemies"] = enemyList,
			["droneDots"] = droneList,
			["material"] = materialList,
			["shots"] = shotList,
		};
	}

	/// <summary>An enemy's type as an index into the log's enemy types, adding the type the first time it is seen
	/// with what a map needs to draw it: its name, its colour and what it does that matters, such as absorbing
	/// drones or damaging by touch. So maps need no list of enemy types of their own.</summary>
	private float TypeIndex(EnemySwarm enemies, int enemy)
	{
		string id = enemies.TypeIdOf(enemy);
		int i = _enemyTypes.IndexOf(id);
		if (i < 0)
		{
			EnemyDefinition definition = enemies.DefinitionOf(enemy);
			_enemyTypes.Add(id);
			_enemyTypeInfo.Add(new Dictionary<string, object>
			{
				["id"] = id,
				["name"] = definition.Name ?? id,
				["color"] = new[] { definition.Color.R, definition.Color.G, definition.Color.B },
				["absorbsDrones"] = enemies.AbsorbsDronesOf(enemy),
				["damagesByTouch"] = enemies.DamagesByTouchOf(enemy),
			});
			i = _enemyTypes.Count - 1;
		}
		return i;
	}

	private static float Round(float value) => (float)Math.Round(value, 1);

	/// <summary>The track, events and strips, for the fight log.</summary>
	public Dictionary<string, object> ToLog()
	{
		// Copies of everything that keeps growing, since the log may be written in the background while the fight
		// carries on. A strip still waiting for its last frames when the fight ended is kept as far as it got.
		var strips = new List<object>();
		foreach (Strip strip in _strips.Concat(_pending))
		{
			var entry = new Dictionary<string, object> { ["reason"] = strip.Reason, ["time"] = strip.Time,
				["frames"] = new List<Dictionary<string, object>>(strip.Frames) };
			if (strip.Spacing.HasValue)
				entry["spacing"] = Math.Round(strip.Spacing.Value, 3);
			if (!strip.HasMoment)
				entry["moment"] = false;
			strips.Add(entry);
		}
		Vector2 gateway = _fight.Gateway.PlanePosition;
		return new Dictionary<string, object>
		{
			["trackSeconds"] = _settings.TrackSeconds,
			["stripView"] = _settings.StripView,
			["stripSpacing"] = _settings.StripSpacing,
			["activities"] = Enum.GetNames(typeof(Activity)),
			["track"] = new List<float[]>(_track),
			["events"] = new List<Dictionary<string, object>>(_events),
			["gateway"] = new[] { Round(gateway.X), Round(gateway.Y) },
			["enemyTypes"] = new List<Dictionary<string, object>>(_enemyTypeInfo),
			["strips"] = strips,
		};
	}
}
#endif
