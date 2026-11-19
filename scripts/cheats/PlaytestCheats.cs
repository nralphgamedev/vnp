#if TOOLS
using Godot;

/// <summary>
/// Cheats for playtesting from the editor. They can never reach an exported game, debug or release, for
/// two reasons:
/// - VNP.csproj compiles the scripts/cheats/ folder only in Godot's Debug configuration, the one the
///   editor runs. Exports build as ExportDebug or ExportRelease, which leave this file out.
/// - This file, and every line that uses it, is inside #if TOOLS, a symbol Godot defines only in Debug.
/// An export that somehow kept a use of it would fail to compile rather than ship a cheat.
/// </summary>
public static class PlaytestCheats
{
	/// <summary>While Alt is held, rerolling a level-up offer is free, so it never runs out.</summary>
	public static bool FreeRerolls => Input.IsKeyPressed(Key.Alt);
}
#endif
