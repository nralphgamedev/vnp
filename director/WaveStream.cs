using Godot;

/// <summary>
/// One kind of wave in a fight, as it plays out: how many have been sent, and when the next is due, by its
/// timing (WavesDefinition). The Fight Director keeps one for the large waves and one for the small.
/// </summary>
public sealed class WaveStream
{
	public WavesDefinition Definition { get; }

	/// <summary>Waves sent so far.</summary>
	public int Sent { get; private set; }

	/// <summary>Fight time, in seconds, when the next wave is due.</summary>
	public float NextTime { get; private set; }

	/// <summary>Fight time, in seconds, when the last wave was due (one interval before the first, before any).</summary>
	public float LastTime { get; private set; }

	/// <summary>How many groups the next wave has.</summary>
	public int NextGroups => Definition.Groups + Definition.GroupsAddedPerWave * (Sent / Mathf.Max(1, Definition.WavesPerGroupAdded));

	// The shortest gap between waves, except for Steady, which keeps the formula fights had before plans.
	private const float MinGap = 1f;

	// For Irregular gaps: a generator of the director's own, so the timing follows the seed alone.
	private readonly System.Random _random;
	// For SpeedingUp: the gap after the next wave.
	private float _gap;

	public WaveStream(WavesDefinition definition, System.Random random)
	{
		Definition = definition;
		_random = random;
		NextTime = definition.FirstSeconds;
		LastTime = definition.FirstSeconds - definition.IntervalSeconds;
		_gap = definition.IntervalSeconds;
	}

	/// <summary>Counts a wave as sent, and works out when the next is due.</summary>
	public void Advance()
	{
		WavesDefinition d = Definition;
		LastTime = NextTime;
		Sent++;
		switch (d.Timing)
		{
			case WaveTiming.Steady:
				NextTime = d.FirstSeconds + Sent * d.IntervalSeconds;
				break;
			case WaveTiming.SpeedingUp:
				_gap = Mathf.Max(Mathf.Max(MinGap, d.MinIntervalSeconds), _gap);
				NextTime = LastTime + _gap;
				_gap *= d.IntervalScalePerWave;
				break;
			case WaveTiming.Irregular:
				float stray = d.IntervalSpread * (2f * (float)_random.NextDouble() - 1f);
				NextTime = LastTime + Mathf.Max(MinGap, d.IntervalSeconds * (1f + stray));
				break;
			case WaveTiming.Surge:
				int perSurge = Mathf.Max(1, d.WavesPerSurge);
				NextTime = d.FirstSeconds + Sent / perSurge * Mathf.Max(MinGap, d.IntervalSeconds)
					+ Sent % perSurge * Mathf.Max(MinGap, d.SecondsWithinSurge);
				break;
		}
	}
}
