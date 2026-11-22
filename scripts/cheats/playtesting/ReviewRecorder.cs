#if TOOLS
using Godot;
using System;
using System.Collections.Generic;

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
	}

	private readonly Fight _fight;
	private readonly ReviewSettings _settings;
	private readonly int[] _enemies = new int[1024];
	private readonly int[] _material = new int[512];
	private readonly List<string> _enemyTypes = new();

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

	/// <summary>Fight times at which to save a strip as well as when detectors fire, such as a moment Claude
	/// spotted on an overview map. Set by the scenario harness's strip= option.</summary>
	public readonly List<double> ExtraStripTimes = new();

	public ReviewRecorder(Fight fight, ReviewSettings settings)
	{
		_fight = fight;
		_settings = settings;
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

		foreach (double time in ExtraStripTimes)
		{
			if (time <= now && time > now - 1.0 / 30 && !_strips.Exists(x => x.Time == time) && !_pending.Exists(x => x.Time == time))
				Flag("requested", time);
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

	/// <summary>A detector fired (or a strip was asked for): note it on the track, and start a strip from the
	/// last few seconds of snapshots, finished over the next few.</summary>
	public void Flag(string reason, double now)
	{
		Event(now, "detector", reason);
		if (_strips.Count + _pending.Count >= _settings.MaxStripsPerFight && reason != "requested")
			return;
		var strip = new Strip { Reason = reason, Time = Math.Round(now, 2), AfterLeft = Math.Max(1, _settings.StripAfter) };
		strip.Frames.AddRange(_recent);
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
			enemyList.Add(new[] { Round(p.X), Round(p.Y), Round(enemies.RadiusOf(e)), TypeIndex(enemies.TypeIdOf(e)) });
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

	private float TypeIndex(string id)
	{
		int i = _enemyTypes.IndexOf(id);
		if (i < 0)
		{
			_enemyTypes.Add(id);
			i = _enemyTypes.Count - 1;
		}
		return i;
	}

	private static float Round(float value) => (float)Math.Round(value, 1);

	/// <summary>The track, events and strips, for the fight log.</summary>
	public Dictionary<string, object> ToLog()
	{
		var strips = new List<object>();
		foreach (Strip strip in _strips)
			strips.Add(new Dictionary<string, object> { ["reason"] = strip.Reason, ["time"] = strip.Time, ["frames"] = strip.Frames });
		// Copies of everything that keeps growing, since the log may be written in the background while the fight
		// carries on. A strip still waiting for its last frames when the fight ended is kept as far as it got.
		foreach (Strip strip in _pending)
			strips.Add(new Dictionary<string, object> { ["reason"] = strip.Reason, ["time"] = strip.Time,
				["frames"] = new List<Dictionary<string, object>>(strip.Frames) });
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
			["enemyTypes"] = new List<string>(_enemyTypes),
			["strips"] = strips,
		};
	}
}
#endif
