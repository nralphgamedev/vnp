#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

/// <summary>
/// Draws the run review's maps (docs/RUN_REVIEW_SPEC.md) from a fight log, as PNG files: an overview map of the
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
	private static readonly Color[] EnemyColors = { new(0.90f, 0.28f, 0.30f), new(0.70f, 0.50f, 0.95f), new(0.95f, 0.80f, 0.30f) };
	private static readonly Color Hit = new(1f, 0.25f, 0.25f);
	private static readonly Color DroneLost = new(1f, 0.55f, 0.80f);
	private static readonly Color Level = new(0.95f, 0.85f, 0.30f);
	private static readonly Color Drone = new(0.35f, 0.85f, 0.95f);
	private static readonly Color Asteroid = new(0.55f, 0.57f, 0.62f);
	private static readonly Color Debris = new(0.65f, 0.48f, 0.30f);
	private static readonly Color Shot = new(1f, 0.6f, 0.2f);
	private static readonly Color Gateway = new(0.95f, 0.85f, 0.30f);

	/// <summary>One letter per detector, used to mark its moments on overview maps.</summary>
	private static readonly Dictionary<string, string> DetectorLetters = new()
	{
		["touched_goo"] = "G", ["through_group"] = "T", ["closed_in"] = "C", ["indecision"] = "I",
		["creeping"] = "S", ["no_drones_fighting"] = "N", ["idle"] = "Z", ["drones_lost_burst"] = "B", ["requested"] = "R",
	};

	/// <summary>
	/// Draws a fight log's maps into <paramref name="folder"/>: its overview if <paramref name="overview"/>, and
	/// the strips whose indices are in <paramref name="strips"/> (null for all). Returns the files written.
	/// </summary>
	public static List<string> DrawLog(string logPath, string folder, bool overview, ICollection<int> strips)
	{
		var written = new List<string>();
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(logPath));
		JsonElement log = document.RootElement;
		if (!log.TryGetProperty("maps", out JsonElement maps) || maps.ValueKind != JsonValueKind.Object)
			return written;
		Directory.CreateDirectory(folder);
		string name = Path.GetFileNameWithoutExtension(logPath);
		if (overview)
		{
			string file = Path.Combine(folder, $"{name}_overview.png");
			DrawOverview(log, maps).SavePng(file);
			written.Add(file);
		}
		JsonElement stripList = maps.GetProperty("strips");
		for (int i = 0; i < stripList.GetArrayLength(); i++)
		{
			if (strips != null && !strips.Contains(i))
				continue;
			JsonElement strip = stripList[i];
			string reason = strip.GetProperty("reason").GetString();
			double time = strip.GetProperty("time").GetDouble();
			string file = Path.Combine(folder, $"{name}_strip{i}_{reason}_{(int)time}s.png");
			DrawStrip(log, maps, strip).SavePng(file);
			written.Add(file);
		}
		return written;
	}

	// ───────────── Overview ─────────────

	private static Image DrawOverview(JsonElement log, JsonElement maps)
	{
		const int Width = 1000, MapSize = 1000, Header = 56, StripHeight = 64, Strips = 4, Footer = 26;
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
		Text(image, gate.X + 9, gate.Y - 6, "GATEWAY", Gateway, 2);
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
					Text(image, at.X + 11, at.Y - 7, DetectorLetters.GetValueOrDefault(id, "?"), Ink, 2);
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
		Text(image, 8, 40, "DETECTORS: G GOO  T GROUP  C CLOSE IN  I INDECISION  S CREEPING  N NO DRONES  Z IDLE  " +
			"B DRONE BURST  R REQUESTED", Dim, 1);
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
		const int Panel = 320, Columns = 4, Header = 40, Gap = 6;
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
		float spacing = maps.TryGetProperty("stripSpacing", out JsonElement spacingValue) ? (float)spacingValue.GetDouble() : 1f;
		float scale = Panel / view;
		double moment = strip.GetProperty("time").GetDouble();
		string reason = strip.GetProperty("reason").GetString();
		JsonElement track = maps.GetProperty("track");

		// Which frame is the moment: the last one at or before it.
		int momentFrame = 0;
		for (int f = 0; f < n; f++)
		{
			if (frames[f].GetProperty("time").GetDouble() <= moment + 0.01)
				momentFrame = f;
		}

		Text(image, Gap, 8, $"{ReasonName(reason)} AT {Ui.Clock(moment)} (CLOCK {RunReview.OnScreenClock(moment)})  " +
			$"{StripWho(log)}  VIEW {view:0} UNITS ACROSS, PICTURES {spacing:0.#}S APART", Ink, 2);
		Text(image, Gap, 28, "VNP WHITE WITH ITS PATH, RED BASIC ENEMY, VIOLET GOO, CYAN DRONES, GREY ASTEROID, BROWN DEBRIS, ORANGE SHOTS", Dim, 1);

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
				int type = Math.Clamp((int)e[3].GetDouble(), 0, EnemyColors.Length - 1);
				int radius = Math.Max(3, (int)(e[2].GetDouble() * scale));
				Disc(image, at, radius, EnemyColors[type]);
			}
			foreach (JsonElement d in frame.GetProperty("droneDots").EnumerateArray())
				Disc(image, P(new Vector2((float)d[0].GetDouble(), (float)d[1].GetDouble())), 2, Drone);
			foreach (JsonElement s in frame.GetProperty("shots").EnumerateArray())
			{
				Vector2 at = new((float)s[0].GetDouble(), (float)s[1].GetDouble());
				Vector2 v = new((float)s[2].GetDouble(), (float)s[3].GetDouble());
				Line(image, P(at), P(at + v * 0.12f), Shot, 2);
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
			Text(image, px + 6, py + 6, $"{(offset >= 0 ? "+" : "")}{offset:0.0}S  {Ui.Clock(t)} ({RunReview.OnScreenClock(t)})", Ink, 1);
			Text(image, px + 6, py + 18, $"HEALTH {frame.GetProperty("health").GetDouble():0}  DRONES {frame.GetProperty("drones").GetInt32()}  " +
				$"LEVEL {frame.GetProperty("level").GetInt32()}", Dim, 1);
		}
		return image;
	}

	private static string StripWho(JsonElement log)
	{
		JsonElement s = log.GetProperty("settings");
		return s.GetProperty("player").GetString() == "designer" ? "DESIGNER" : $"{s.GetProperty("pilot").GetString()} SEED {s.GetProperty("seed").GetInt64()}";
	}

	private static string ReasonName(string reason) => reason switch
	{
		"touched_goo" => "TOUCHED A GOO", "through_group" => "FLEW THROUGH A GROUP", "closed_in" => "CLOSED IN ON A NEAR ENEMY",
		"indecision" => "INDECISION", "creeping" => "CREEPING", "no_drones_fighting" => "FIGHTING WITHOUT DRONES",
		"idle" => "IDLE", "drones_lost_burst" => "DRONES LOST IN A BURST", "requested" => "REQUESTED MOMENT",
		_ => reason.ToUpperInvariant(),
	};

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
