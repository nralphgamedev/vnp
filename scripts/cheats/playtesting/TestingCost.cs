#if TOOLS
using System.Diagnostics;

/// <summary>
/// The time testing code spends each frame (the scenario harness, the fight log with its recorder and detectors),
/// so the performance requirement can be judged on the game's own time: a frame over budget only because of
/// testing work is reported, tagged as testing, and doesn't fail it. Testing code wraps its per-frame work in
/// Begin and End; the harness reads and resets the total each frame. Editor only.
/// </summary>
public static class TestingCost
{
	/// <summary>Milliseconds of testing work so far this frame.</summary>
	public static double FrameMs;

	public static long Begin() => Stopwatch.GetTimestamp();

	public static void End(long started) =>
		FrameMs += (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
}
#endif
