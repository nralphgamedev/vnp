#if TOOLS
using Godot;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Draws run review maps from fight logs, then quits (docs/dev/RUN_REVIEW_SPEC.md). tools\maps.cmd runs it for a
/// fight's folder; headless, directly:
///
///   godot --headless --path . res://scripts/cheats/playtesting/draw_maps.tscn -- log=PATH [log=PATH ...] [options]
///
///   log=PATH        a fight log (absolute, or res://); repeat for several. Each can end in @STRIPS to pick its
///                   flagged moments' strips, such as log=a.json@0,3 or log=a.json@none
///   strip=T,...     strips of any moments or stretches, drawn from the fight's whole-fight recording (the
///                   designer's fights): T, T:S, FROM-TO or FROM-TO:S in fight seconds, as the scenario harness's
///                   strip= takes them. With strip=, the overview and flagged strips are left out unless asked for
///   flagged=all     every flagged moment's strip (or none, or indices such as 0,3), for logs without @STRIPS
///   overview=yes    the overview map (yes or no)
///   out=FOLDER      where the PNGs go (default: beside each log in its own fight folder, else a maps folder)
///
/// With only log=, it draws the overview and every flagged strip. Prints each file it writes as "MAP: path".
/// An argument it can't use stops it with exit code 2, as the scenario harness does. Editor only.
/// </summary>
public partial class DrawMaps : Node
{
	public override void _Ready()
	{
		string folder = null;
		bool? overview = null;
		string flagged = null;
		var requests = new List<ReviewRecorder.StripRequest>();
		var logs = new List<(string Path, HashSet<int> Strips)>();
		var errors = new List<string>();
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			string[] parts = argument.Split('=', 2);
			string value = parts.Length > 1 ? parts[1] : "";
			try
			{
				switch (parts[0])
				{
					case "log":
					{
						string[] pick = value.Split('@', 2);
						HashSet<int> strips = null;
						if (pick.Length > 1)
							strips = pick[1] == "none" ? new HashSet<int>() : pick[1].Split(',').Select(int.Parse).ToHashSet();
						string path = ProjectSettings.GlobalizePath(pick[0]);
						if (!File.Exists(path))
							errors.Add($"{argument}: no such file");
						logs.Add((path, strips));
						break;
					}
					case "strip": requests.AddRange(value.Split(',').Select(ReviewRecorder.StripRequest.Parse)); break;
					case "flagged":
						if (value != "all" && value != "none")
							value.Split(',').Select(int.Parse).ToList();
						flagged = value;
						break;
					case "overview":
						if (value != "yes" && value != "no")
							throw new System.FormatException();
						overview = value == "yes";
						break;
					case "out": folder = ProjectSettings.GlobalizePath(value); break;
					default: errors.Add($"unknown option {argument}; options: log, strip, flagged, overview, out"); break;
				}
			}
			catch (System.Exception e) when (e is System.FormatException or System.OverflowException)
			{
				errors.Add($"{argument}: a value it can't use");
			}
		}
		if (logs.Count == 0)
			errors.Add("no log= given");
		if (errors.Count > 0)
		{
			foreach (string error in errors)
				GD.PrintErr($"DRAWMAPS ERROR: {error}");
			ShutdownCleanup.Run();
			GetTree().Quit(2);
			return;
		}

		// Asking for particular moments draws only those, unless the overview or flagged strips are asked for too.
		bool drawOverview = overview ?? requests.Count == 0;
		flagged ??= requests.Count == 0 ? "all" : "none";
		HashSet<int> flaggedPick = flagged switch
		{
			"all" => null,
			"none" => new HashSet<int>(),
			_ => flagged.Split(',').Select(int.Parse).ToHashSet(),
		};
		var settings = GD.Load<ReviewSettings>("res://scripts/cheats/playtesting/review_settings.tres");
		int exitCode = 0;
		foreach (var (path, strips) in logs)
		{
			// A log in a folder of its own (the designer's fights) has its maps beside it, without its name on them.
			string logFolder = Path.GetDirectoryName(path);
			bool ownFolder = Path.GetFileName(logFolder) == Path.GetFileNameWithoutExtension(path);
			string outFolder = folder ?? (ownFolder ? logFolder : Path.Combine(logFolder, "maps"));
			string prefix = folder == null && ownFolder ? "" : null;
			try
			{
				var watch = System.Diagnostics.Stopwatch.StartNew();
				foreach (string file in MapDrawer.DrawLog(path, outFolder, drawOverview, strips ?? flaggedPick, prefix))
					GD.Print($"MAP: {file}");
				if (requests.Count > 0)
				{
					foreach (string file in MapDrawer.DrawRecording(path, outFolder, requests,
						settings?.StripBefore ?? 4, settings?.StripAfter ?? 2, prefix))
						GD.Print($"MAP: {file}");
				}
				GD.Print($"DRAWN in {watch.ElapsedMilliseconds} ms: {path}");
			}
			catch (System.Exception e)
			{
				GD.PrintErr($"DRAWMAPS ERROR: could not draw {path}: {e.Message}");
				exitCode = 1;
			}
		}
		ShutdownCleanup.Run();
		GetTree().Quit(exitCode);
	}
}
#endif
