#if TOOLS
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Draws run review maps from fight logs, then quits (docs/RUN_REVIEW_SPEC.md). Run headless:
///
///   godot --headless --path . res://scripts/cheats/playtesting/draw_maps.tscn -- log=PATH [log=PATH ...] out=FOLDER
///
///   log=PATH        a fight log (absolute, or res://); repeat for several. Each can end in @STRIPS to pick its
///                   strips, such as log=a.json@0,3 or log=a.json@none; all of them otherwise
///   out=FOLDER      where the PNGs go (default: a maps folder beside each log)
///   overview=no     skip the overview maps
///
/// Prints each file it writes as "MAP: path". Editor only.
/// </summary>
public partial class DrawMaps : Node
{
	public override void _Ready()
	{
		string folder = null;
		bool overview = true;
		var logs = new List<(string Path, HashSet<int> Strips)>();
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			string[] parts = argument.Split('=', 2);
			string value = parts.Length > 1 ? parts[1] : "";
			switch (parts[0])
			{
				case "log":
				{
					string[] pick = value.Split('@', 2);
					HashSet<int> strips = null;
					if (pick.Length > 1)
						strips = pick[1] == "none" ? new HashSet<int>() : pick[1].Split(',').Select(int.Parse).ToHashSet();
					logs.Add((ProjectSettings.GlobalizePath(pick[0]), strips));
					break;
				}
				case "out": folder = ProjectSettings.GlobalizePath(value); break;
				case "overview": overview = value != "no"; break;
				default: GD.PushWarning($"DrawMaps: unknown argument {argument}"); break;
			}
		}
		foreach (var (path, strips) in logs)
		{
			string outFolder = folder ?? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), "maps");
			try
			{
				var watch = System.Diagnostics.Stopwatch.StartNew();
				foreach (string file in MapDrawer.DrawLog(path, outFolder, overview, strips))
					GD.Print($"MAP: {file}");
				GD.Print($"DRAWN in {watch.ElapsedMilliseconds} ms: {path}");
			}
			catch (System.Exception e)
			{
				GD.PushError($"DrawMaps: could not draw {path}: {e.Message}");
			}
		}
		ShutdownCleanup.Run();
		GetTree().Quit();
	}
}
#endif
