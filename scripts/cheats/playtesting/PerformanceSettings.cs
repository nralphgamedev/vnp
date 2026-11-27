#if TOOLS
using Godot;

/// <summary>
/// The performance requirement (CLAUDE.md, Technical rules) and how the scenario harness's perf= option judges
/// it. Values live in res://scripts/cheats/playtesting/performance.tres. Editor only, like every testing aid.
/// </summary>
[GlobalClass]
public partial class PerformanceSettings : Resource
{
	/// <summary>The whole frame's budget: 8.33 ms is 120 frames a second. The slowest 1% of frames (SlowestShare)
	/// must take no longer, measured by the clock with vsync off and nothing extra measuring.</summary>
	[Export] public float FrameBudgetMs = 8.33f;

	/// <summary>The share of frames the budget is judged on: 0.01 is the slowest 1%.</summary>
	[Export] public float SlowestShare = 0.01f;

	/// <summary>No single frame may take longer than this, outside loading and menus: a hitch a player feels.</summary>
	[Export] public float HitchMs = 16.7f;

	/// <summary>Game logic's budget for its slowest 1% of frames, so weaker computers than the designer's, which
	/// suffer most from CPU work, keep headroom. Judged in perf=detail, which measures logic separately.</summary>
	[Export] public float LogicBudgetMs = 4f;

	/// <summary>With perfstart=, the fight plays this many times faster until the measuring starts (Godot's time
	/// scale), so a late fight is reached in a fraction of its fight time. Uncapped frames are short, so each step
	/// stays near a normal frame's. The fast part isn't measured or judged.</summary>
	[Export] public float FastForwardScale = 10f;

	/// <summary>Seconds at the start left out as loading: the first frames compile code and build what the fight
	/// needs.</summary>
	[Export] public float SkipSeconds = 2f;

	/// <summary>The most the main thread may allocate a frame, on average over the judged frames, in kilobytes.
	/// Garbage is what makes .NET collect, and a full collection paused the game for several milliseconds, so a
	/// change that starts allocating every frame fails here when it is made rather than as a rare slow frame
	/// later. Testing code's allocations count too, so keep the harness's own work light.</summary>
	[Export] public float AllocationBudgetKb = 1f;
}
#endif
