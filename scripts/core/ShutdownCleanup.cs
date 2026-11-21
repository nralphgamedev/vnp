using System;

/// <summary>
/// Cleans up .NET's leftover garbage before the game quits, while Godot is still running.
///
/// Godot's C# layer disposes every Godot array still in use when it shuts down. Arrays that are already garbage,
/// waiting for .NET to clean them up, slip past that, and if .NET happens to clean them up during the shutdown,
/// after Godot's side is gone, the game crashes as it quits. Long fights leave more of that garbage. Found when
/// pilot fights that had ended crashed on quitting, every time for some seeds. Cleaning up first prevents it.
/// </summary>
public static class ShutdownCleanup
{
	public static void Run()
	{
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
	}
}
