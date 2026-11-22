#if TOOLS
using Godot;

/// <summary>
/// Settings for batches of automated fights. Values live in res://scripts/cheats/playtesting/batch_settings.tres.
/// The batch command (tools/batches/batch.py) reads that file. Editor only, like the cheats.
/// </summary>
[GlobalClass]
public partial class BatchSettings : Resource
{
	/// <summary>Seeds per combination in a quick batch: a smoke test after a change, in about a minute.</summary>
	[ExportGroup("Sizes")]
	[Export] public int QuickSeeds = 3;

	/// <summary>Seeds per combination in a standard batch: enough fights for clear averages.</summary>
	[Export] public int StandardSeeds = 50;

	/// <summary>Seeds per combination in a long batch, for overnight tuning sweeps.</summary>
	[Export] public int LongSeeds = 100;

	/// <summary>Fight time in seconds, counted from the start, at which a fight that hasn't ended is stopped and
	/// counted as having survived to the cap, so one strong build in endless mode can't hold up a batch.</summary>
	[ExportGroup("Running")]
	[Export] public float EndlessCapSeconds = 480f;

	/// <summary>Headless fights run at once, as separate game processes. 0 uses one per processor core, leaving
	/// one spare.</summary>
	[Export] public int FightsAtOnce;

	/// <summary>Rendered fights, in windows, run at once. Usually 1, to watch one at a time.</summary>
	[Export] public int RenderedAtOnce = 1;
}
#endif
