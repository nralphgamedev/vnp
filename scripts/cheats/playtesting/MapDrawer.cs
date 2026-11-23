#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Draws the run review's maps (docs/dev/RUN_REVIEW_SPEC.md) from a fight log, as PNG files: an overview map of the
/// whole fight, from its track, and strip maps of moments, from its strips. They are plain diagrams for reading
/// what happened, not the game's look. Drawn with Godot's Image, which works headless, so nothing needs
/// installing. Labels use a small built-in pixel font, since Image can't draw text.
///
/// Editor only.
/// </summary>
public static class MapDrawer
{
	// Colours, on a dark ground.
	private static readonly Color Ground = new(0.08f, 0.09f, 0.11f);
	private static readonly Color Grid = new(0.14f, 0.15f, 0.18f);
	private static readonly Color Ink = new(0.92f, 0.93f, 0.95f);
	private static readonly Color Dim = new(0.55f, 0.58f, 0.64f);
	private static readonly Color[] ActivityColors =
	{
		new(0.55f, 0.63f, 0.80f),  // travelling
		new(0.36f, 0.82f, 0.54f),  // harvesting
		new(0.95f, 0.63f, 0.25f),  // fighting
		new(0.76f, 0.50f, 0.95f),  // moving away
	};
	// Only for logs from before enemy types carried their own colour.
	private static readonly Color[] FallbackEnemyColors = { new(0.90f, 0.28f, 0.30f), new(0.70f, 0.50f, 0.95f), new(0.95f, 0.80f, 0.30f) };

	/// <summary>An enemy type as the log describes it: its name, colour, and the traits maps mark by shape, so any
	/// number of types can be read by what they do rather than by memorising colours.</summary>
	private record EnemyType(string Name, Color Color, bool AbsorbsDrones, bool DamagesByTouch);

	private static List<EnemyType> EnemyTypes(JsonElement maps)
	{
		var types = new List<EnemyType>();
		int i = 0;
		foreach (JsonElement t in maps.GetProperty("enemyTypes").EnumerateArray())
		{
			if (t.ValueKind == JsonValueKind.String)
				types.Add(new EnemyType(t.GetString(), FallbackEnemyColors[i % FallbackEnemyColors.Length], false, false));
			else
			{
				JsonElement c = t.GetProperty("color");
				types.Add(new EnemyType(t.GetProperty("name").GetString(),
					new Color((float)c[0].GetDouble(), (float)c[1].GetDouble(), (float)c[2].GetDouble()),
					t.GetProperty("absorbsDrones").GetBoolean(), t.GetProperty("damagesByTouch").GetBoolean()));
			}
			i++;
		}
		return types;
	}

	/// <summary>The fight's detectors in the log's order, each with a letter for marking its moments on overview
	/// maps, so a new detector appears on maps without changing the drawer.</summary>
	private static List<(string Id, string Name, string Letter)> Detectors(JsonElement log)
	{
		var detectors = new List<(string, string, string)>();
		if (log.TryGetProperty("review", out JsonElement review) && review.ValueKind == JsonValueKind.Object)
		{
			int i = 0;
			foreach (JsonProperty d in review.GetProperty("detectors").EnumerateObject())
				detectors.Add((d.Name, d.Value.GetProperty("name").GetString(), ((char)('A' + i++)).ToString()));
		}
		detectors.Add(("requested", "Requested moment", "R"));
		return detectors;
	}

	/// <summary>Draws one enemy: a disc in its type's colour, striped if it damages by touch, with an outer ring if
	/// it absorbs drones.</summary>
	private static void Enemy(Image image, Vector2I at, int radius, EnemyType type)
	{
		Disc(image, at, radius, type.Color);
		if (type.DamagesByTouch)
		{
			Color stripe = type.Color.Darkened(0.55f);
			for (int j = -radius; j <= radius; j++)
				for (int i = -radius; i <= radius; i++)
					if (i * i + j * j <= radius * radius && ((i + j) % 4 + 4) % 4 == 0)
						Plot(image, at.X + i, at.Y + j, stripe);
		}
		if (type.AbsorbsDrones)
			Ring(image, at, radius + 3, type.Color.Lightened(0.4f), 1);
	}
	private static readonly Color Hit = new(1f, 0.25f, 0.25f);
	// Waves arriving, on maps: a hot orange apart from enemy shots' and the hit red.
	private static readonly Color Wave = new(1f, 0.45f, 0.1f);
	private static readonly Color DroneLost = new(1f, 0.55f, 0.80f);
	private static readonly Color Level = new(0.95f, 0.85f, 0.30f);
	private static readonly Color Drone = new(0.35f, 0.85f, 0.95f);
	private static readonly Color Asteroid = new(0.55f, 0.57f, 0.62f);
	private static readonly Color Debris = new(0.65f, 0.48f, 0.30f);
	private static readonly Color Shot = new(1f, 0.6f, 0.2f);
	private static readonly Color DangerToVnp = new(1f, 0.85f, 0.15f);
	private static readonly Color DangerToDrones = new(1f, 0.45f, 0.85f);
	private static readonly Color Gateway = new(0.95f, 0.85f, 0.30f);

	/// <summary>
	/// Draws a fight log's maps into <paramref name="folder"/>: its overview if <paramref name="overview"/>, and
	/// the strips whose indices are in <paramref name="strips"/> (null for all). Each file's name starts with
	/// <paramref name="prefix"/>, the log's name and an underscore unless given, such as "" for maps that sit in
	/// the fight's own folder. Returns the files written.
	/// </summary>
	/// <summary>
	/// Draws strips of any moments or stretches of a fight from its whole-fight recording (the designer's fights),
	/// the way the scenario harness's strip= does for a replayed pilot fight. A moment T gets <paramref name="before"/>
	/// pictures before it and <paramref name="after"/> after, a stretch FROM-TO pictures from start to end, at the
	/// recording's spacing or a wider one asked for. Throws if the log has no recording. Returns the files written.
	/// </summary>
	public static List<string> DrawRecording(string logPath, string folder, IEnumerable<ReviewRecorder.StripRequest> requests,
		int before, int after, string prefix = null)
	{
		var written = new List<string>();
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(logPath));
		JsonElement log = document.RootElement;
		if (!log.TryGetProperty("maps", out JsonElement maps) || !maps.TryGetProperty("recording", out _))
			throw new InvalidDataException("this fight has no whole-fight recording (only the designer's fights, from now on, are recorded)");
		using JsonDocument recording = ReadRecording(logPath, maps);
		Directory.CreateDirectory(folder);
		string name = prefix ?? Path.GetFileNameWithoutExtension(logPath) + "_";

		foreach (ReviewRecorder.StripRequest r in requests)
		{
			double recorded = recording.RootElement.GetProperty("spacing").GetDouble();
			double spacing = Math.Max(recorded, r.Spacing ?? recorded);
			double from = r.End.HasValue ? Math.Min(r.Time, r.End.Value) : r.Time - before * spacing;
			double to = r.End.HasValue ? Math.Max(r.Time, r.End.Value) : r.Time + after * spacing;
			using JsonDocument strip = StripFromRecording(recording.RootElement, "requested", r.Time, from, to, spacing, !r.End.HasValue);
			string label = r.End.HasValue ? $"stretch_{from:0.#}-{to:0.#}s" : $"moment_{r.Time:0.#}s";
			string path = Path.Combine(folder, $"{name}{label}.png");
			DrawStrip(log, maps, strip.RootElement).SavePng(path);
			written.Add(path);
		}
		return written;
	}

	/// <summary>A fight's whole-fight recording, beside its log, which names it. The caller disposes it.</summary>
	private static JsonDocument ReadRecording(string logPath, JsonElement maps)
	{
		string recordingPath = Path.Combine(Path.GetDirectoryName(logPath), maps.GetProperty("recording").GetString());
		using FileStream file = File.OpenRead(recordingPath);
		using var zip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Decompress);
		return JsonDocument.Parse(zip);
	}

	/// <summary>A strip, as the log would hold one, of the recorded pictures nearest each picture time from
	/// <paramref name="from"/> to <paramref name="to"/>, at most 48. The caller disposes it.</summary>
	private static JsonDocument StripFromRecording(JsonElement recording, string reason, double time, double from, double to,
		double spacing, bool moment)
	{
		var culture = System.Globalization.CultureInfo.InvariantCulture;
		double recorded = recording.GetProperty("spacing").GetDouble();
		JsonElement frames = recording.GetProperty("frames");
		var picked = new List<string>();
		double next = from;
		for (int f = 0; f < frames.GetArrayLength() && picked.Count < 48; f++)
		{
			double t = frames[f].GetProperty("time").GetDouble();
			if (t > to + recorded / 2)
				break;
			if (t >= next - recorded / 2)
			{
				picked.Add(frames[f].GetRawText());
				while (next <= t + recorded / 2)
					next += spacing;
			}
		}
		if (picked.Count == 0)
			throw new InvalidDataException($"the recording has no pictures between {from:0.#}s and {to:0.#}s");
		string json = "{\"reason\":" + JsonSerializer.Serialize(reason) + ",\"time\":" + Math.Round(time, 2).ToString(culture) +
			",\"spacing\":" + spacing.ToString(culture) + ",\"moment\":" + (moment ? "true" : "false") +
			",\"frames\":[" + string.Join(",", picked) + "]}";
		return JsonDocument.Parse(json);
	}

	public static List<string> DrawLog(string logPath, string folder, bool overview, ICollection<int> strips, string prefix = null)
	{
		var written = new List<string>();
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(logPath));
		JsonElement log = document.RootElement;
		if (!log.TryGetProperty("maps", out JsonElement maps) || maps.ValueKind != JsonValueKind.Object)
			return written;
		Directory.CreateDirectory(folder);
		string name = prefix ?? Path.GetFileNameWithoutExtension(logPath) + "_";
		if (overview)
		{
			string file = Path.Combine(folder, $"{name}overview.png");
			DrawOverview(log, maps).SavePng(file);
			written.Add(file);
		}
		JsonElement stripList = maps.GetProperty("strips");
		JsonDocument recording = null;
		try
		{
			for (int i = 0; i < stripList.GetArrayLength(); i++)
			{
				if (strips != null && !strips.Contains(i))
					continue;
				JsonElement strip = stripList[i];
				string reason = strip.GetProperty("reason").GetString();
				double time = strip.GetProperty("time").GetDouble();
				string file = Path.Combine(folder, $"{name}strip{i}_{reason}_{(int)time}s.png");
				if (strip.TryGetProperty("fromRecording", out JsonElement fromRecording) && fromRecording.GetBoolean())
				{
					// A recorded fight keeps only the moment's time: its pictures come from the recording, at the
					// strips' usual spacing, before and after.
					recording ??= ReadRecording(logPath, maps);
					double spacing = maps.TryGetProperty("stripSpacing", out JsonElement s) ? s.GetDouble() : 1.0;
					int before = maps.TryGetProperty("stripBefore", out JsonElement b) ? b.GetInt32() : 4;
					int after = maps.TryGetProperty("stripAfter", out JsonElement a) ? a.GetInt32() : 2;
					using JsonDocument built = StripFromRecording(recording.RootElement, reason, time,
						time - before * spacing, time + after * spacing, spacing, true);
					DrawStrip(log, maps, built.RootElement).SavePng(file);
				}
				else
					DrawStrip(log, maps, strip).SavePng(file);
				written.Add(file);
			}
		}
		finally
		{
			recording?.Dispose();
		}
		return written;
	}

	// ───────────── Overview ─────────────

	private static Image DrawOverview(JsonElement log, JsonElement maps)
	{
		const int Width = 1000, MapSize = 1000, Header = 70, StripHeight = 64, Strips = 4, Footer = 26;
		var detectors = Detectors(log);
		int height = Header + MapSize + Strips * StripHeight + Footer;
		Image image = Image.CreateEmpty(Width, height, false, Image.Format.Rgba8);
		image.Fill(Ground);

		JsonElement track = maps.GetProperty("track");
		int count = track.GetArrayLength();
		double end = log.GetProperty("outcome").GetProperty("time").GetDouble();
		Vector2 gateway = Vec(maps.GetProperty("gateway"), 0);

		// Fit the whole path and the gateway, with a margin, square.
		float minX = gateway.X, maxX = gateway.X, minY = gateway.Y, maxY = gateway.Y;
		for (int i = 0; i < count; i++)
		{
			float x = (float)track[i][1].GetDouble(), y = (float)track[i][2].GetDouble();
			minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
		}
		float span = Mathf.Max(40f, Mathf.Max(maxX - minX, maxY - minY)) * 1.1f;
		Vector2 centre = new((minX + maxX) / 2, (minY + maxY) / 2);
		float scale = MapSize / span;
		Vector2I ToPixel(Vector2 p) => new((int)((p.X - centre.X) * scale + Width / 2f), (int)((p.Y - centre.Y) * scale + Header + MapSize / 2f));

		// A grid every 50 units, so distances can be read.
		float gridStep = 50f;
		for (float g = Mathf.Floor((centre.X - span / 2) / gridStep) * gridStep; g < centre.X + span / 2; g += gridStep)
			FillRect(image, ToPixel(new Vector2(g, 0)).X, Header, 1, MapSize, Grid);
		for (float g = Mathf.Floor((centre.Y - span / 2) / gridStep) * gridStep; g < centre.Y + span / 2; g += gridStep)
			FillRect(image, 0, ToPixel(new Vector2(0, g)).Y, Width, 1, Grid);

		// The path, coloured by what the VNP was doing.
		for (int i = 1; i < count; i++)
		{
			Vector2 a = new((float)track[i - 1][1].GetDouble(), (float)track[i - 1][2].GetDouble());
			Vector2 b = new((float)track[i][1].GetDouble(), (float)track[i][2].GetDouble());
			int activity = Math.Clamp((int)track[i - 1][3].GetDouble(), 0, ActivityColors.Length - 1);
			Line(image, ToPixel(a), ToPixel(b), ActivityColors[activity], 2);
		}

		// Gateway, start and end.
		Vector2I gate = ToPixel(gateway);
		FillRect(image, gate.X - 6, gate.Y - 6, 12, 12, Gateway);
		// The label goes on whichever side of the gateway has room.
		Text(image, gate.X + 9 + 84 > Width ? gate.X - 9 - 84 : gate.X + 9, gate.Y - 6, "GATEWAY", Gateway, 2);
		if (count > 0)
		{
			Vector2I start = ToPixel(new Vector2((float)track[0][1].GetDouble(), (float)track[0][2].GetDouble()));
			Ring(image, start, 7, Ink, 2);
			Vector2I last = ToPixel(new Vector2((float)track[count - 1][1].GetDouble(), (float)track[count - 1][2].GetDouble()));
			Disc(image, last, 6, Ink);
			Text(image, last.X + 9, last.Y - 18, "END", Ink, 2);
		}

		// Events along the way.
		foreach (JsonElement e in maps.GetProperty("events").EnumerateArray())
		{
			Vector2I at = ToPixel(new Vector2((float)e.GetProperty("x").GetDouble(), (float)e.GetProperty("y").GetDouble()));
			switch (e.GetProperty("kind").GetString())
			{
				case "hit":
					Disc(image, at, Mathf.Clamp((int)(2 + e.GetProperty("value").GetDouble() / 3), 2, 7), Hit);
					break;
				case "drones_lost":
					FillRect(image, at.X - 2, at.Y - 2, 4, 4, DroneLost);
					break;
				case "level":
					FillRect(image, at.X - 1, at.Y - 4, 2, 8, Level);
					break;
				case "detector":
					string id = e.GetProperty("value").GetString();
					Ring(image, at, 9, Ink, 2);
					string letter = detectors.Find(d => d.Id == id).Letter ?? "?";
					Text(image, at.X + 11, at.Y - 7, letter, Ink, 2);
					break;
				case "wave":
					Ring(image, at, 6, Wave, 2);
					Text(image, at.X - 20, at.Y - 7, $"W{e.GetProperty("value").GetInt32()}", Wave, 1);
					break;
				case "escalation":
					Ring(image, at, 6, Level, 2);
					Text(image, at.X - 20, at.Y - 7, $"E{e.GetProperty("value").GetInt32()}", Level, 1);
					break;
			}
		}

		// The fight time every 30 seconds along the path.
		double nextMark = 30;
		for (int i = 0; i < count; i++)
		{
			double t = track[i][0].GetDouble();
			if (t < nextMark)
				continue;
			nextMark += 30;
			Vector2I at = ToPixel(new Vector2((float)track[i][1].GetDouble(), (float)track[i][2].GetDouble()));
			Disc(image, at, 3, Ink);
			Text(image, at.X + 5, at.Y + 5, Ui.Clock(t), Dim, 1);
		}

		// Header: who, what, how it ended, and a key.
		JsonElement settings = log.GetProperty("settings");
		JsonElement outcome = log.GetProperty("outcome");
		string who = settings.GetProperty("player").GetString() == "designer" ? "DESIGNER"
			: $"{settings.GetProperty("pilot").GetString()} {settings.GetProperty("build").GetString()}";
		string weapons = string.Join("+", JsonStrings(settings.GetProperty("startingWeapons"))) + " " +
			settings.GetProperty("startingDroneWeapon").GetString();
		string ended = outcome.GetProperty("killTargetTime").ValueKind == JsonValueKind.Number
			? $"WON {Ui.Clock(outcome.GetProperty("killTargetTime").GetDouble())}" : "NOT WON";
		Text(image, 8, 6, $"{who}  SEED {settings.GetProperty("seed").GetInt64()}  {weapons}  {ended}  " +
			$"{outcome.GetProperty("result").GetString()} AT {Ui.Clock(end)}  LEVEL {outcome.GetProperty("level").GetInt32()}", Ink, 2);
		int keyX = 8;
		string[] keys = { "TRAVEL", "HARVEST", "FIGHT", "MOVING AWAY" };
		for (int k = 0; k < keys.Length; k++)
		{
			FillRect(image, keyX, 29, 14, 4, ActivityColors[k]);
			keyX += 18;
			keyX = Text(image, keyX, 26, keys[k], Dim, 1) + 12;
		}
		Disc(image, new Vector2I(keyX + 3, 31), 3, Hit); keyX = Text(image, keyX + 9, 26, "HIT", Dim, 1) + 12;
		FillRect(image, keyX, 29, 4, 4, DroneLost); keyX = Text(image, keyX + 7, 26, "DRONES LOST", Dim, 1) + 12;
		FillRect(image, keyX, 27, 2, 8, Level); keyX = Text(image, keyX + 5, 26, "LEVEL", Dim, 1) + 12;
		Ring(image, new Vector2I(keyX + 4, 31), 4, Wave, 1); keyX = Text(image, keyX + 11, 26, "W WAVE", Dim, 1) + 12;
		Ring(image, new Vector2I(keyX + 4, 31), 4, Level, 1); keyX = Text(image, keyX + 11, 26, "E ESCALATION STEP", Dim, 1) + 12;
		// The detectors' letters, from the log, wrapping onto a second line if needed.
		int keyLeft = 8, keyY = 40;
		keyLeft = Text(image, keyLeft, keyY, "DETECTORS:", Dim, 1) + 8;
		foreach (var d in detectors)
		{
			string entry = $"{d.Letter} {d.Name}";
			if (keyLeft + entry.Length * 6 > Width - 130)
			{
				keyLeft = 74;
				keyY += 12;
			}
			keyLeft = Text(image, keyLeft, keyY, entry, Dim, 1) + 14;
		}
		Text(image, Width - 120, 40, $"GRID {gridStep:0} UNITS", Dim, 1);

		// Strips over time: health, drones, level and speed.
		int top = Header + MapSize;
		JsonElement timeline = log.GetProperty("timeline");
		double span2 = Math.Max(end, 1);
		int X(double t) => 70 + (int)(t / span2 * (Width - 80));
		DrawSeries(image, top, StripHeight, "HEALTH", timeline, "health", "maxHealth", Hit, X);
		DrawSeries(image, top + StripHeight, StripHeight, "DRONES", timeline, "drones", null, Drone, X);
		DrawSeries(image, top + StripHeight * 2, StripHeight, "LEVEL", timeline, "level", null, Level, X);
		// Speed comes from the track.
		int y0 = top + StripHeight * 3;
		Text(image, 6, y0 + 6, "SPEED", Dim, 1);
		FillRect(image, 70, y0 + StripHeight - 4, Width - 80, 1, Grid);
		for (int i = 1; i < count; i++)
		{
			int xa = X(track[i - 1][0].GetDouble()), xb = X(track[i][0].GetDouble());
			int ya = y0 + StripHeight - 4 - (int)(Math.Min(1.0, track[i - 1][4].GetDouble()) * (StripHeight - 12));
			int yb = y0 + StripHeight - 4 - (int)(Math.Min(1.0, track[i][4].GetDouble()) * (StripHeight - 12));
			Line(image, new Vector2I(xa, ya), new Vector2I(xb, yb), Ink, 1);
		}
		// Minute marks and the time limit across all the strips.
		int bottom = top + StripHeight * Strips;
		for (int m = 0; m * 60 <= end; m++)
		{
			FillRect(image, X(m * 60), top, 1, StripHeight * Strips, Grid);
			Text(image, X(m * 60) - 8, bottom + 6, $"{m}:00", Dim, 1);
		}
		double limit = Tuning.Fight.TimeLimitSeconds;
		if (limit <= end)
		{
			FillRect(image, X(limit), top, 2, StripHeight * Strips, Level);
			Text(image, X(limit) + 4, top + 4, "5:00", Level, 1);
		}
		foreach (JsonElement e in maps.GetProperty("events").EnumerateArray())
		{
			string kind = e.GetProperty("kind").GetString();
			if (kind != "wave" && kind != "escalation")
				continue;
			int wx = X(e.GetProperty("time").GetDouble());
			Color mark = kind == "wave" ? Wave : Level;
			for (int y = top; y < top + StripHeight * Strips; y += 4)
				FillRect(image, wx, y, 1, 2, mark);
			Text(image, wx + 3, top + StripHeight * Strips - 12, $"{(kind == "wave" ? "W" : "E")}{e.GetProperty("value").GetInt32()}", mark, 1);
		}
		if (outcome.GetProperty("killTargetTime").ValueKind == JsonValueKind.Number)
		{
			int kx = X(outcome.GetProperty("killTargetTime").GetDouble());
			FillRect(image, kx, top, 2, StripHeight * Strips, ActivityColors[1]);
			Text(image, kx + 4, top + 16, "WON", ActivityColors[1], 1);
		}
		return image;
	}

	private static void DrawSeries(Image image, int y0, int height, string label, JsonElement timeline, string key,
		string maxKey, Color color, Func<double, int> x)
	{
		int n = timeline.GetArrayLength();
		double max = 1;
		for (int i = 0; i < n; i++)
			max = Math.Max(max, maxKey != null ? timeline[i].GetProperty(maxKey).GetDouble() : timeline[i].GetProperty(key).GetDouble());
		Text(image, 6, y0 + 6, label, Dim, 1);
		Text(image, 6, y0 + 18, $"MAX {max:0}", Dim, 1);
		FillRect(image, 70, y0 + height - 4, image.GetWidth() - 80, 1, Grid);
		for (int i = 1; i < n; i++)
		{
			JsonElement a = timeline[i - 1], b = timeline[i];
			int ya = y0 + height - 4 - (int)(a.GetProperty(key).GetDouble() / max * (height - 12));
			int yb = y0 + height - 4 - (int)(b.GetProperty(key).GetDouble() / max * (height - 12));
			Line(image, new Vector2I(x(a.GetProperty("time").GetDouble()), ya), new Vector2I(x(b.GetProperty("time").GetDouble()), yb), color, 1);
		}
	}

	// ───────────── Strips ─────────────

	private static Image DrawStrip(JsonElement log, JsonElement maps, JsonElement strip)
	{
		const int Panel = 320, Columns = 4, Header = 78, Gap = 6;
		List<EnemyType> types = EnemyTypes(maps);
		var detectors = Detectors(log);
		var weaponColors = new List<Color>();
		var weaponNames = new List<string>();
		if (maps.TryGetProperty("weaponTypes", out JsonElement weaponTypes))
		{
			foreach (JsonElement w in weaponTypes.EnumerateArray())
			{
				JsonElement c = w.GetProperty("color");
				weaponColors.Add(new Color((float)c[0].GetDouble(), (float)c[1].GetDouble(), (float)c[2].GetDouble()));
				weaponNames.Add(w.GetProperty("name").GetString());
			}
		}
		JsonElement frames = strip.GetProperty("frames");
		int n = frames.GetArrayLength();
		int rows = Math.Max(1, (n + Columns - 1) / Columns);
		int width = Columns * Panel + (Columns + 1) * Gap;
		int height = Header + rows * (Panel + Gap) + Gap;
		Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
		image.Fill(Ground);

		// From the log rather than the settings file, so drawing never loads a resource and can run off the main
		// thread.
		float view = maps.TryGetProperty("stripView", out JsonElement viewValue) ? (float)viewValue.GetDouble() : 64f;
		float spacing = strip.TryGetProperty("spacing", out JsonElement ownSpacing) ? (float)ownSpacing.GetDouble()
			: maps.TryGetProperty("stripSpacing", out JsonElement spacingValue) ? (float)spacingValue.GetDouble() : 1f;
		bool hasMoment = !strip.TryGetProperty("moment", out JsonElement momentValue) || momentValue.GetBoolean();
		float scale = Panel / view;
		double moment = strip.GetProperty("time").GetDouble();
		string reason = strip.GetProperty("reason").GetString();
		JsonElement track = maps.GetProperty("track");

		// Which frame is the moment: the nearest to it, since pictures land on the first game frame at or after
		// their time. A stretch asked for has none.
		int momentFrame = -1;
		double nearest = double.MaxValue;
		for (int f = 0; hasMoment && f < n; f++)
		{
			double off = Math.Abs(frames[f].GetProperty("time").GetDouble() - moment);
			if (off < nearest)
			{
				nearest = off;
				momentFrame = f;
			}
		}

		string reasonName = detectors.Find(d => d.Id == reason).Name ?? reason;
		Text(image, Gap, 8, $"{reasonName} AT {Ui.Clock(moment)} (CLOCK {RunReview.OnScreenClock(moment)})  " +
			$"{StripWho(log)}  VIEW {view:0} UNITS ACROSS, PICTURES {spacing:0.##}S APART", Ink, 2);
		// The key, on two lines so it fits the narrowest strip.
		Text(image, Gap, 28, "VNP WHITE WITH ITS PATH AND HEADING, CYAN DRONES (REDDER AS THEY LOSE HEALTH, A FAINT LINE TO THEIR TARGET), " +
			"GREY ASTEROID, BROWN DEBRIS, ORANGE ENEMY SHOTS.", Dim, 1);
		Text(image, Gap, 40, "SHOTS, BEAMS, SHARDS, MINES, MISSILES AND RINGS OF YOUR WEAPONS IN THE COLOUR OF EACH. " +
			"ENEMIES IN THEIR OWN COLOUR: STRIPED DAMAGES BY TOUCH, OUTER RING ABSORBS DRONES", Dim, 1);
		Text(image, Gap, 52, "WHAT A PILOT PERCEIVES: YELLOW OUTLINES HURT THE VNP, PINK ONES DRONES, DIM ONES NOT YET LANDED, " +
			"EACH WITH WHERE IT MOVES IN THE NEXT SECOND. FAINT RINGS: THE REACH OF YOUR WEAPONS", Dim, 1);
		// A key to the enemy types in this strip only.
		var present = new SortedSet<int>();
		for (int f = 0; f < n; f++)
			foreach (JsonElement e in frames[f].GetProperty("enemies").EnumerateArray())
				present.Add((int)e[3].GetDouble());
		int legendX = Gap;
		foreach (int t in present)
		{
			if (t < 0 || t >= types.Count)
				continue;
			Enemy(image, new Vector2I(legendX + 5, 68), 4, types[t]);
			legendX = Text(image, legendX + 14, 65, types[t].Name, Dim, 1) + 16;
		}
		// The loadout at the end of the strip: each VNP Weapon with its mods, and the Drone Weapon.
		if (n > 0 && frames[n - 1].TryGetProperty("loadout", out JsonElement loadout))
		{
			string WeaponName(double index) => index >= 0 && index < weaponNames.Count ? weaponNames[(int)index] : "?";
			var parts = new List<string>();
			foreach (JsonElement entry in loadout.EnumerateArray())
			{
				string mods = string.Join(", ", entry[1].EnumerateArray().Select(m => m.GetString().ToUpperInvariant()));
				parts.Add(WeaponName(entry[0].GetDouble()).ToUpperInvariant() + (mods == "" ? "" : $" ({mods})"));
			}
			string drones = frames[n - 1].TryGetProperty("droneWeapon", out JsonElement d) && d.GetDouble() >= 0
				? $"  -  DRONES {WeaponName(d.GetDouble()).ToUpperInvariant()}" : "";
			Text(image, legendX + 24, 65, "LOADOUT: " + string.Join(",  ", parts) + drones, Dim, 1);
		}

		for (int f = 0; f < n; f++)
		{
			JsonElement frame = frames[f];
			int px = Gap + (f % Columns) * (Panel + Gap), py = Header + (f / Columns) * (Panel + Gap);
			Vector2 me = new((float)frame.GetProperty("vnp")[0].GetDouble(), (float)frame.GetProperty("vnp")[1].GetDouble());
			Vector2I P(Vector2 world) => new((int)(px + Panel / 2f + (world.X - me.X) * scale), (int)(py + Panel / 2f + (world.Y - me.Y) * scale));

			FillRect(image, px, py, Panel, Panel, new Color(0.10f, 0.11f, 0.13f));
			float gridStep = 10f;
			for (float g = Mathf.Floor((me.X - view / 2) / gridStep) * gridStep; g <= me.X + view / 2; g += gridStep)
			{
				int gx = P(new Vector2(g, me.Y)).X;
				if (gx > px && gx < px + Panel) FillRect(image, gx, py, 1, Panel, Grid);
			}
			for (float g = Mathf.Floor((me.Y - view / 2) / gridStep) * gridStep; g <= me.Y + view / 2; g += gridStep)
			{
				int gy = P(new Vector2(me.X, g)).Y;
				if (gy > py && gy < py + Panel) FillRect(image, px, gy, Panel, 1, Grid);
			}

			Clip(px, py, Panel);
			foreach (JsonElement m in frame.GetProperty("material").EnumerateArray())
				Disc(image, P(new Vector2((float)m[0].GetDouble(), (float)m[1].GetDouble())),
					Math.Max(2, (int)(m[2].GetDouble() * scale)), m[3].GetDouble() > 0.5 ? Asteroid : Debris);
			foreach (JsonElement e in frame.GetProperty("enemies").EnumerateArray())
			{
				Vector2I at = P(new Vector2((float)e[0].GetDouble(), (float)e[1].GetDouble()));
				int type = (int)e[3].GetDouble();
				int radius = Math.Max(3, (int)(e[2].GetDouble() * scale));
				Enemy(image, at, radius, type >= 0 && type < types.Count ? types[type]
					: new EnemyType("?", FallbackEnemyColors[0], false, false));
			}
			// Each drone's target, as a faint line, where the log recorded it: enemies' IDs are their 7th value.
			var enemyAt = new Dictionary<int, Vector2>();
			foreach (JsonElement e in frame.GetProperty("enemies").EnumerateArray())
			{
				if (e.GetArrayLength() > 6)
					enemyAt[(int)e[6].GetDouble()] = new Vector2((float)e[0].GetDouble(), (float)e[1].GetDouble());
			}
			foreach (JsonElement d in frame.GetProperty("droneDots").EnumerateArray())
			{
				Vector2 at = new((float)d[0].GetDouble(), (float)d[1].GetDouble());
				if (d.GetArrayLength() > 3 && (int)d[3].GetDouble() is int target && target != 0 && enemyAt.TryGetValue(target, out Vector2 to))
					Line(image, P(at), P(to), new Color(Drone, 0.3f), 1);
			}
			foreach (JsonElement d in frame.GetProperty("droneDots").EnumerateArray())
			{
				// Redder as it loses health, so a drone being drained or shot shows it.
				float health = d.GetArrayLength() > 2 ? Mathf.Clamp((float)d[2].GetDouble(), 0f, 1f) : 1f;
				Disc(image, P(new Vector2((float)d[0].GetDouble(), (float)d[1].GetDouble())), 2, Hit.Lerp(Drone, health));
			}
			// The VNP's weapons' objects: shards and missiles as small marks, mines with the ring of their blast.
			if (frame.TryGetProperty("placed", out JsonElement placed))
			{
				foreach (JsonElement w in placed.EnumerateArray())
				{
					int kind = (int)w[3].GetDouble();
					Color color = kind >= 0 && kind < weaponColors.Count ? weaponColors[kind] : Ink;
					Vector2I at = P(new Vector2((float)w[0].GetDouble(), (float)w[1].GetDouble()));
					int radius = (int)(w[2].GetDouble() * scale);
					var shape = w.GetArrayLength() > 4 ? (PlacedShape)(int)w[4].GetDouble() : PlacedShape.Point;
					if (shape == PlacedShape.Line)
						Line(image, at, P(new Vector2((float)w[5].GetDouble(), (float)w[6].GetDouble())), new Color(color, 0.8f), 1);
					else if (shape == PlacedShape.Ring)
						Ring(image, at, Math.Max(2, radius), new Color(color, 0.7f), 1);
					else
					{
						if (radius > 6)
							Ring(image, at, radius, new Color(color, 0.5f), 1);
						Disc(image, at, 3, color);
					}
				}
			}
			// What a pilot perceives: weapons' reach, then hazards (see ReviewRecorder.Snapshot for the values).
			if (frame.TryGetProperty("zones", out JsonElement zones))
			{
				foreach (JsonElement z in zones.EnumerateArray())
				{
					if ((int)z[0].GetDouble() != 0)
						continue; // Traps, such as mines, are already drawn as the weapon's objects.
					Vector2 at = new((float)z[2].GetDouble(), (float)z[3].GetDouble());
					if (z[6].GetDouble() > 0.5)
						at += me;
					int kind = (int)z[7].GetDouble();
					Color color = new(kind >= 0 && kind < weaponColors.Count ? weaponColors[kind] : Ink, 0.25f);
					Ring(image, P(at), Math.Max(2, (int)(z[4].GetDouble() * scale)), color, 1);
					if ((int)z[1].GetDouble() == 3 && z[5].GetDouble() > 0)
						Ring(image, P(at), Math.Max(2, (int)(z[5].GetDouble() * scale)), color, 1);
				}
			}
			if (frame.TryGetProperty("hazards", out JsonElement hazards))
			{
				foreach (JsonElement h in hazards.EnumerateArray())
					Hazard(image, h, P, scale);
			}
			foreach (JsonElement s in frame.GetProperty("shots").EnumerateArray())
			{
				Vector2 at = new((float)s[0].GetDouble(), (float)s[1].GetDouble());
				Vector2 v = new((float)s[2].GetDouble(), (float)s[3].GetDouble());
				// An enemy's shot is orange; the VNP's and drones' in their weapon's colour, thinner.
				int kind = s.GetArrayLength() > 4 ? (int)s[4].GetDouble() : -1;
				if (kind < 0)
					Line(image, P(at), P(at + v * 0.12f), Shot, 2);
				else
					Line(image, P(at), P(at + v * 0.08f), kind < weaponColors.Count ? weaponColors[kind] : Ink, 1);
			}
			// The VNP's path over the last few seconds, from the track, then the VNP and where it is heading.
			double t = frame.GetProperty("time").GetDouble();
			Vector2I? previous = null;
			for (int i = 0; i < track.GetArrayLength(); i++)
			{
				double tt = track[i][0].GetDouble();
				if (tt < t - 4 || tt > t)
					continue;
				Vector2I at = P(new Vector2((float)track[i][1].GetDouble(), (float)track[i][2].GetDouble()));
				if (previous is Vector2I prev)
					Line(image, prev, at, new Color(1, 1, 1, 0.6f), 1);
				previous = at;
			}
			Vector2 velocity = new((float)frame.GetProperty("vnp")[2].GetDouble(), (float)frame.GetProperty("vnp")[3].GetDouble());
			Vector2I centre = P(me);
			Disc(image, centre, 5, Ink);
			Line(image, centre, P(me + velocity * 0.5f), Ink, 2);
			Unclip();

			Color border = f == momentFrame ? Level : Grid;
			FillRect(image, px, py, Panel, 2, border); FillRect(image, px, py + Panel - 2, Panel, 2, border);
			FillRect(image, px, py, 2, Panel, border); FillRect(image, px + Panel - 2, py, 2, Panel, border);
			double offset = t - moment;
			Text(image, px + 6, py + 6, $"{(offset >= 0 ? "+" : "")}{offset:0.00}S  {Ui.Clock(t)} ({RunReview.OnScreenClock(t)})", Ink, 1);
			Text(image, px + 6, py + 18, $"HEALTH {frame.GetProperty("health").GetDouble():0}  DRONES {frame.GetProperty("drones").GetInt32()}  " +
				$"LEVEL {frame.GetProperty("level").GetInt32()}", Dim, 1);
			// A wave or escalation step since the previous picture.
			double since = f > 0 ? frames[f - 1].GetProperty("time").GetDouble() : t - spacing;
			foreach (JsonElement e in maps.GetProperty("events").EnumerateArray())
			{
				string kind = e.GetProperty("kind").GetString();
				double et = e.GetProperty("time").GetDouble();
				if ((kind != "wave" && kind != "escalation") || et <= since || et > t)
					continue;
				string what = kind == "wave"
					? $"WAVE {e.GetProperty("value").GetInt32()} ARRIVES" + (e.TryGetProperty("groups", out JsonElement g) ? $", {g.GetInt32()} GROUPS" : "")
					: $"ESCALATION STEP {e.GetProperty("value").GetInt32()}";
				Text(image, px + 6, py + 30, what, kind == "wave" ? Wave : Level, 1);
			}
		}
		return image;
	}

	/// <summary>
	/// One hazard a pilot perceives, as recorded: its outline now, yellow if it hurts the VNP and pink if only drones,
	/// dimmer if it hasn't landed yet, and a line to where it will be in a second (or when it ends, if sooner).
	/// Every shape Arbiter has is drawn, so a new attack shows with no code here.
	/// </summary>
	private static void Hazard(Image image, JsonElement h, Func<Vector2, Vector2I> P, float scale)
	{
		int shape = (int)h[0].GetDouble();
		Vector2 at = new((float)h[1].GetDouble(), (float)h[2].GetDouble());
		Vector2 velocity = new((float)h[3].GetDouble(), (float)h[4].GetDouble());
		Vector2 extent = new((float)h[5].GetDouble(), (float)h[6].GetDouble());
		float radius = (float)h[7].GetDouble(), inner = (float)h[8].GetDouble();
		float landsIn = (float)h[9].GetDouble(), lasts = (float)h[10].GetDouble();
		int flags = (int)h[12].GetDouble();
		Color color = (flags & 1) != 0 ? DangerToVnp : DangerToDrones;
		color = new Color(color, landsIn > 0f ? 0.4f : 0.85f);
		int r = Math.Max(1, (int)(radius * scale));
		switch (shape)
		{
			case 0: // Circle. A body's own outline sits just outside the enemy, and drones' just outside that.
				if (radius > 0f)
					Ring(image, P(at), r + ((flags & 1) != 0 ? 1 : 3), color, 1);
				break;
			case 1: // Capsule: its two sides and its ends.
			{
				Vector2 side = extent.LengthSquared() > 1e-6f ? new Vector2(-extent.Y, extent.X).Normalized() * radius : Vector2.Zero;
				Line(image, P(at + side), P(at + extent + side), color, 1);
				Line(image, P(at - side), P(at + extent - side), color, 1);
				Ring(image, P(at), r, color, 1);
				Ring(image, P(at + extent), r, color, 1);
				break;
			}
			case 2: // Cone: its two edges and its far edge.
			{
				Vector2 left = extent.Rotated(inner), right = extent.Rotated(-inner);
				Line(image, P(at), P(at + left), color, 1);
				Line(image, P(at), P(at + right), color, 1);
				Line(image, P(at + left), P(at + extent), color, 1);
				Line(image, P(at + extent), P(at + right), color, 1);
				break;
			}
			case 3: // Ring: both edges.
				Ring(image, P(at), r, color, 1);
				if (inner > 0f)
					Ring(image, P(at), Math.Max(1, (int)(inner * scale)), color, 1);
				break;
		}
		float ahead = lasts >= 0f ? Math.Min(1f, landsIn + lasts) : 1f;
		if (velocity.LengthSquared() > 0.01f && ahead > 0f)
			Line(image, P(at), P(at + velocity * ahead), new Color(color, color.A * 0.6f), 1);
	}

	private static string StripWho(JsonElement log)
	{
		JsonElement s = log.GetProperty("settings");
		return s.GetProperty("player").GetString() == "designer" ? "DESIGNER" : $"{s.GetProperty("pilot").GetString()} SEED {s.GetProperty("seed").GetInt64()}";
	}

	private static IEnumerable<string> JsonStrings(JsonElement array)
	{
		foreach (JsonElement e in array.EnumerateArray())
			yield return e.GetString();
	}

	private static Vector2 Vec(JsonElement array, int at) => new((float)array[at].GetDouble(), (float)array[at + 1].GetDouble());

	// ───────────── Drawing ─────────────

	// Optional clipping to one panel, so a strip's shapes stay inside their own picture.
	[ThreadStatic] private static Rect2I? _clip;
	private static void Clip(int x, int y, int size) => _clip = new Rect2I(x, y, size, size);
	private static void Unclip() => _clip = null;

	private static void Plot(Image image, int x, int y, Color color)
	{
		if (x < 0 || y < 0 || x >= image.GetWidth() || y >= image.GetHeight())
			return;
		if (_clip is Rect2I clip && !clip.HasPoint(new Vector2I(x, y)))
			return;
		if (color.A < 0.999f)
			color = image.GetPixel(x, y).Lerp(new Color(color.R, color.G, color.B), color.A);
		image.SetPixel(x, y, color);
	}

	private static void FillRect(Image image, int x, int y, int w, int h, Color color)
	{
		for (int j = y; j < y + h; j++)
			for (int i = x; i < x + w; i++)
				Plot(image, i, j, color);
	}

	private static void Disc(Image image, Vector2I c, int r, Color color)
	{
		for (int j = -r; j <= r; j++)
			for (int i = -r; i <= r; i++)
				if (i * i + j * j <= r * r)
					Plot(image, c.X + i, c.Y + j, color);
	}

	private static void Ring(Image image, Vector2I c, int r, Color color, int thickness)
	{
		int inner = (r - thickness) * (r - thickness);
		for (int j = -r; j <= r; j++)
			for (int i = -r; i <= r; i++)
			{
				int d = i * i + j * j;
				if (d <= r * r && d >= inner)
					Plot(image, c.X + i, c.Y + j, color);
			}
	}

	private static void Line(Image image, Vector2I a, Vector2I b, Color color, int thickness)
	{
		int steps = Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
		int half = thickness / 2;
		for (int s = 0; s <= steps; s++)
		{
			float t = steps == 0 ? 0 : (float)s / steps;
			int x = (int)Mathf.Round(Mathf.Lerp(a.X, b.X, t)), y = (int)Mathf.Round(Mathf.Lerp(a.Y, b.Y, t));
			if (thickness <= 1)
				Plot(image, x, y, color);
			else
				FillRect(image, x - half, y - half, thickness, thickness, color);
		}
	}

	/// <summary>Draws text in the built-in 5 by 7 pixel font at a whole-number scale; returns where it ends.</summary>
	private static int Text(Image image, int x, int y, string text, Color color, int scale)
	{
		foreach (char raw in text.ToUpperInvariant())
		{
			if (Font.TryGetValue(raw, out string[] rows))
			{
				for (int r = 0; r < 7; r++)
					for (int c = 0; c < 5; c++)
						if (rows[r][c] == '#')
							FillRect(image, x + c * scale, y + r * scale, scale, scale, color);
			}
			x += 6 * scale;
		}
		return x;
	}

	private static readonly Dictionary<char, string[]> Font = new()
	{
		['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
		['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
		['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
		['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
		['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
		['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
		['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####" },
		['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
		['I'] = new[] { ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." },
		['J'] = new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." },
		['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
		['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
		['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
		['N'] = new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" },
		['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
		['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
		['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
		['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
		['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
		['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
		['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
		['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
		['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "#.#.#", ".#.#." },
		['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
		['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
		['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
		['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
		['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
		['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
		['3'] = new[] { "#####", "...#.", "..#..", "...#.", "....#", "#...#", ".###." },
		['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
		['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
		['6'] = new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." },
		['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
		['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
		['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." },
		[':'] = new[] { ".....", "..#..", "..#..", ".....", "..#..", "..#..", "....." },
		['.'] = new[] { ".....", ".....", ".....", ".....", ".....", ".##..", ".##.." },
		[','] = new[] { ".....", ".....", ".....", ".....", ".##..", "..#..", ".#..." },
		['+'] = new[] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." },
		['-'] = new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." },
		['/'] = new[] { "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." },
		['%'] = new[] { "##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##" },
		['('] = new[] { "...#.", "..#..", ".#...", ".#...", ".#...", "..#..", "...#." },
		[')'] = new[] { ".#...", "..#..", "...#.", "...#.", "...#.", "..#..", ".#..." },
		['_'] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "#####" },
	};
}
#endif
