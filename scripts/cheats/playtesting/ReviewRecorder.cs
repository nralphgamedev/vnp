#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Records what the run review's maps are drawn from (docs/dev/RUN_REVIEW_SPEC.md): the VNP's track through the
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
	private int _lastWaves;
	private int _lastEscalation;

	private readonly Queue<Dictionary<string, object>> _recent = new();
	private double _nextSnapshot;
	private readonly List<Strip> _pending = new();
	private readonly List<Strip> _strips = new();
	private readonly List<Scheduled> _scheduled = new();

	/// <summary>Record the picture around the VNP all fight, every RecordSpacing seconds, for a fight that can't be
	/// replayed (the designer's), so any moment can be drawn afterwards. Set by the fight log.</summary>
	public bool RecordWholeFight;
	private readonly List<Dictionary<string, object>> _recording = new();
	private double _nextRecord;
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

		RecordChanges(now);

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

		if (RecordWholeFight && now >= _nextRecord)
		{
			_nextRecord = now + Math.Max(0.1, _settings.RecordSpacing);
			_recording.Add(Snapshot(now));
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
		// The last frame's changes, which Update never sees once the VNP is destroyed, since the fight pauses for
		// the result screen in the same frame: the hit that destroyed it, and any drones lost with it.
		RecordChanges(now);
		if (_endTaken || !_fight.Vnp.IsDestroyed)
			return;
		_endTaken = true;
		Dictionary<string, object> last = Snapshot(now);
		if (RecordWholeFight)
			_recording.Add(last);
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

	/// <summary>Events from changes since the last frame: hits taken, drones lost and level-ups.</summary>
	private void RecordChanges(double now)
	{
		Vnp vnp = _fight.Vnp;
		float health = Mathf.Max(0f, vnp.Health);
		if (_lastHealth >= 0 && health < _lastHealth - 0.01f)
			Event(now, "hit", Math.Round(_lastHealth - health, 1));
		_lastHealth = health;
		int lost = _fight.Stats.DronesDestroyedTotal + _fight.Stats.DronesAbsorbedTotal;
		if (lost > _lastDronesLost)
			Event(now, "drones_lost", lost - _lastDronesLost);
		_lastDronesLost = lost;
		if (_fight.Level > _lastLevel)
			Event(now, "level", _fight.Level);
		_lastLevel = _fight.Level;
		// Each wave, numbered, with how many groups it sent, and each escalation step past the time limit, so a
		// review can tell what arrived when, and how much harder enemies had just become.
		int waves = _fight.Enemies.WavesSent;
		if (waves > _lastWaves)
			Event(now, "wave", waves, "groups", _fight.Enemies.LastWaveGroups);
		_lastWaves = waves;
		int escalation = _fight.EscalationSteps;
		if (escalation > _lastEscalation)
			Event(now, "escalation", escalation);
		_lastEscalation = escalation;
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

	private void Event(double now, string kind, object value, string extraKey = null, object extraValue = null)
	{
		var entry = new Dictionary<string, object>
		{
			["time"] = Math.Round(now, 2), ["kind"] = kind, ["value"] = value,
			["x"] = Round(_fight.Vnp.PlanePosition.X), ["y"] = Round(_fight.Vnp.PlanePosition.Y),
		};
		if (extraKey != null)
			entry[extraKey] = extraValue;
		_events.Add(entry);
	}

	/// <summary>Whether the VNP itself is harvesting: its beam is on something, or it or one of its weapons, such as
	/// Orbital Shards, harvested within HarvestingWithin seconds. Drones' harvesting doesn't count.</summary>
	public static bool VnpHarvesting(Fight fight, ReviewSettings settings) => fight.VnpHarvester.Beaming
		|| (fight.Harvestables.LastVnpHarvestTime >= 0
			&& fight.Elapsed - fight.Harvestables.LastVnpHarvestTime <= settings.HarvestingWithin);

	private Activity ActivityNow()
	{
		Vnp vnp = _fight.Vnp;
		if (VnpHarvesting(_fight, _settings))
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
			Vector2 v = enemies.VelocityOf(e);
			// Position, size, type, velocity, ID (for drones' targets) and how fast it drains a drone (0 for most).
			enemyList.Add(new[] { Round(p.X), Round(p.Y), Round(enemies.RadiusOf(e)), TypeIndex(enemies, e),
				Round(v.X), Round(v.Y), enemies.IdOf(e), Round(enemies.DroneDrainOf(e)) });
		}

		var droneList = new List<float[]>();
		DroneSwarm drones = _fight.Drones;
		for (int i = 0; i < drones.Count; i++)
		{
			Vector2 p = drones.PositionOf(i);
			// Position, health as a fraction of a new drone's, and the ID of the enemy it is attacking (0 for none).
			if (p.DistanceSquaredTo(me) <= reach * reach)
				droneList.Add(new[] { Round(p.X), Round(p.Y), (float)Math.Round(Math.Max(0f, drones.HealthFractionOf(i)), 2), drones.TargetIdOf(i) });
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

		// Every shot in the fight's projectiles, enemies' and the VNP's and drones' alike: position, velocity, and
		// what fired it as an index into the log's weapon types, or -1 for an enemy's. So any weapon that fires
		// through the projectiles is recorded with no code of its own.
		var shotList = new List<float[]>();
		Projectiles projectiles = _fight.Projectiles;
		for (int i = 0; i < projectiles.Count; i++)
		{
			Vector2 p = projectiles.PositionOf(i);
			if (p.DistanceSquaredTo(me) > reach * reach)
				continue;
			Vector2 v = projectiles.VelocityOf(i);
			DamageSource source = projectiles.SourceOf(i);
			float kind = projectiles.IsEnemyShot(i) || source == null ? -1f : WeaponIndex(source.Id, source.Name, source.Color);
			shotList.Add(new[] { Round(p.X), Round(p.Y), Round(v.X), Round(v.Y), kind });
		}
		// Fragile enemy shots, such as a Quiller's spikes, are enemy shots on the maps too.
		FragileShots fragile = _fight.FragileShots;
		for (int i = 0; i < fragile.Count; i++)
		{
			if (!fragile.IsFlying(i))
				continue;
			Vector2 p = fragile.PositionOf(i);
			if (p.DistanceSquaredTo(me) > reach * reach)
				continue;
			Vector2 v = fragile.VelocityOf(i);
			shotList.Add(new[] { Round(p.X), Round(p.Y), Round(v.X), Round(v.Y), -1f });
		}

		// What the weapons have out (VnpWeapon and DroneAttack CollectPlaced): position, the radius it acts over,
		// its weapon, its shape (PlacedShape) and, for a line, where it ends. And the loadout: each VNP Weapon with
		// the mods it has, and the Drone Weapon in use.
		var placedList = new List<float[]>();
		var loadout = new List<object>();
		void AddPlaced(float kind)
		{
			foreach (PlacedObject placed in _placed)
			{
				if (placed.Position.DistanceSquaredTo(me) <= reach * reach || placed.End.DistanceSquaredTo(me) <= reach * reach)
					placedList.Add(new[] { Round(placed.Position.X), Round(placed.Position.Y), Round(placed.Radius), kind,
						(float)(int)placed.Shape, Round(placed.End.X), Round(placed.End.Y) });
			}
		}
		foreach (VnpWeapon weapon in _fight.Weapons)
		{
			_placed.Clear();
			weapon.CollectPlaced(_placed);
			float kind = WeaponIndex(weapon.Definition.Id, weapon.Definition.Name, weapon.Definition.Color);
			AddPlaced(kind);
			loadout.Add(new object[] { kind, weapon.Features.Select(f => f.ToString()).ToArray() });
		}
		DroneWeaponDefinition droneWeapon = _fight.Drones.Weapon;
		float droneKind = -1f;
		if (droneWeapon != null)
		{
			droneKind = WeaponIndex(droneWeapon.Id, droneWeapon.Name, droneWeapon.Color);
			_placed.Clear();
			_fight.Drones.CollectPlaced(_placed);
			AddPlaced(droneKind);
		}

		// What a pilot perceives (Arbiter, docs/dev/PILOT_SPEC.md), as the enemies, the projectiles and the VNP's
		// weapons declare it, so maps show exactly what a pilot reads. Hazards: shape (Arbiter.AreaShape), position,
		// velocity, extent, radius, inner, when it lands, how long it lasts (-1 for as long as it exists), harm, and
		// flags (1 hurts the VNP, 2 drones, 4 harm is per second). Zones: kind (Arbiter.ZoneKind), shape, position,
		// radius, inner, 1 if it follows the VNP (its position then relative to the VNP), and its weapon.
		_situation.Clear();
		_fight.Enemies.DeclareThreats(_situation, me, reach);
		_fight.Projectiles.DeclareThreats(_situation, me, reach);
		_fight.FragileShots.DeclareThreats(_situation, me, reach);
		foreach (VnpWeapon weapon in _fight.Weapons)
			weapon.DeclareZones(_situation, (int)WeaponIndex(weapon.Definition.Id, weapon.Definition.Name, weapon.Definition.Color));
		var hazardList = new List<float[]>();
		foreach (Arbiter.Hazard h in _situation.Hazards)
		{
			Arbiter.Area a = h.Area;
			float flags = ((h.Endangers & Arbiter.Endangers.Agent) != 0 ? 1 : 0) + ((h.Endangers & Arbiter.Endangers.Allies) != 0 ? 2 : 0) + (h.PerSecond ? 4 : 0);
			hazardList.Add(new[] { (float)(int)a.Shape, Round(a.Position.X), Round(a.Position.Y), Round(a.Velocity.X), Round(a.Velocity.Y),
				Round(a.Extent.X), Round(a.Extent.Y), Round(a.Radius), Round(a.Inner), Round(h.LandsIn),
				float.IsPositiveInfinity(h.Lasts) ? -1f : Round(h.Lasts), Round(h.Harm), flags });
		}
		var zoneList = new List<float[]>();
		foreach (Arbiter.Zone z in _situation.Zones)
		{
			zoneList.Add(new[] { (float)(int)z.Kind, (float)(int)z.Area.Shape, Round(z.Area.Position.X), Round(z.Area.Position.Y),
				Round(z.Area.Radius), Round(z.Area.Inner), z.FollowsAgent ? 1f : 0f, z.Source });
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
			["placed"] = placedList,
			["hazards"] = hazardList,
			["zones"] = zoneList,
			["loadout"] = loadout,
			["droneWeapon"] = droneKind,
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

	private readonly List<PlacedObject> _placed = new();
	private readonly Arbiter.Situation _situation = new();
	private readonly List<string> _weaponIds = new();
	private readonly List<Dictionary<string, object>> _weaponInfo = new();

	/// <summary>A VNP Weapon or Drone Weapon as an index into the log's weapon types, adding it the first time
	/// with its name and colour, so maps draw each weapon's objects and shots in its colour with no list of
	/// weapons of their own.</summary>
	private float WeaponIndex(string id, string name, Color color)
	{
		int i = _weaponIds.IndexOf(id);
		if (i < 0)
		{
			_weaponIds.Add(id);
			_weaponInfo.Add(new Dictionary<string, object>
			{
				["id"] = id, ["name"] = name ?? id, ["color"] = new[] { color.R, color.G, color.B },
			});
			i = _weaponIds.Count - 1;
		}
		return i;
	}

	/// <summary>The whole-fight recording, for its own file beside the log, or null if this fight isn't recorded.
	/// A copy, since the log may be written in the background while the fight carries on.</summary>
	public Dictionary<string, object> Recording() => RecordWholeFight
		? new Dictionary<string, object>
		{
			["spacing"] = _settings.RecordSpacing,
			["frames"] = new List<Dictionary<string, object>>(_recording),
		}
		: null;

	/// <summary>The track, events and strips, for the fight log.</summary>
	public Dictionary<string, object> ToLog()
	{
		// Copies of everything that keeps growing, since the log may be written in the background while the fight
		// carries on. A strip still waiting for its last frames when the fight ended is kept as far as it got.
		var strips = new List<object>();
		foreach (Strip strip in _strips.Concat(_pending))
		{
			// A recorded fight's strips are drawn from its recording, which holds every moment more finely, so its
			// log keeps only when and why; that took the designer's logs from about 1.8 MB to under 1 MB.
			var entry = new Dictionary<string, object> { ["reason"] = strip.Reason, ["time"] = strip.Time };
			if (RecordWholeFight)
				entry["fromRecording"] = true;
			else
				entry["frames"] = new List<Dictionary<string, object>>(strip.Frames);
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
			["stripBefore"] = _settings.StripBefore,
			["stripAfter"] = _settings.StripAfter,
			["activities"] = Enum.GetNames(typeof(Activity)),
			["track"] = new List<float[]>(_track),
			["events"] = new List<Dictionary<string, object>>(_events),
			["gateway"] = new[] { Round(gateway.X), Round(gateway.Y) },
			["enemyTypes"] = new List<Dictionary<string, object>>(_enemyTypeInfo),
			["weaponTypes"] = new List<Dictionary<string, object>>(_weaponInfo),
			["strips"] = strips,
		};
	}
}
#endif
