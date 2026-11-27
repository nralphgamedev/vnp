using Godot;

/// <summary>
/// The fight's own readouts: the clock and kill count at the top, the retreat prompt on the
/// gateway, the wave warning and banner, and the red edge flash with a "!" at each escalation step
/// past the time limit.
/// </summary>
public partial class FightHud : Control
{
	private static readonly Color Warning = new(1f, 0.6f, 0.2f);
	private static readonly Color Danger = new(1f, 0.22f, 0.2f);

	public Fight Fight;

	private float _alertLeft;

	// The clock and kill count are drawn every frame but change rarely, so their text is made again only when what
	// it shows changes, rather than every frame.
	private long _clockKey = long.MinValue;
	private string _clock = "";
	private long _killsKey = long.MinValue;
	private string _kills = "";

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Hud;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>Starts the red edge flash and the "!".</summary>
	public void ShowEscalationAlert() => _alertLeft = Tuning.Difficulty.EscalationAlertSeconds;

	public override void _Process(double delta)
	{
		_alertLeft = Mathf.Max(0f, _alertLeft - (float)delta);
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Fight == null)
			return;

		Font font = ThemeDB.FallbackFont;
		float width = Size.X;

		// Clock: counts down to the limit, then counts up the time past it.
		double left = Tuning.Fight.TimeLimitSeconds - Fight.Elapsed;
		Color clockColor = left > 0 ? (left <= 30 ? Warning : Colors.White) : Danger;
		// Whole seconds shown, negative past the limit, where the clock counts up.
		long clockKey = left > 0 ? (long)System.Math.Ceiling(left) : -1 - (long)(-left);
		if (clockKey != _clockKey)
		{
			_clockKey = clockKey;
			_clock = left > 0 ? Ui.Clock(System.Math.Ceiling(left)) : $"PAST LIMIT  +{Ui.Clock(-left)}";
		}
		DrawString(font, new Vector2(0f, 104f), _clock, HorizontalAlignment.Center, width, 48, clockColor);
		// The target too, since the plan cheat can change it as the fight starts.
		long killsKey = (Fight.Kills * 2L + (Fight.IsEndless ? 1 : 0)) * 100000L + Fight.KillTarget;
		if (killsKey != _killsKey)
		{
			_killsKey = killsKey;
			_kills = Fight.IsEndless ? $"Kills {Fight.Kills}  ·  Endless" : $"Kills {Fight.Kills} / {Fight.KillTarget}";
		}
		DrawString(font, new Vector2(0f, 146f), _kills,
			HorizontalAlignment.Center, width, 28, new Color(1f, 1f, 1f, 0.85f));

		// Retreat prompt, while on the gateway.
		if (Fight.OnGateway && !Fight.IsOver)
		{
			string prompt = Fight.CanRetreat
				? "Press Space or A to retreat"
				: $"Stay on the gateway to retreat:  {System.Math.Ceiling(Tuning.Fight.RetreatHoldSeconds - Fight.GatewayTime):0}";
			DrawString(font, new Vector2(0f, Size.Y - 110f), prompt, HorizontalAlignment.Center, width,
				Fight.CanRetreat ? 36 : 28, Tuning.Look.GatewayColor);
		}

		// Wave warning before a wave, then its number for a moment after it arrives.
		double toWave = Fight.Director.NextWaveTime - Fight.Elapsed;
		double sinceWave = Fight.Elapsed - Fight.Director.LastWaveTime;
		if (!Fight.IsOver && toWave <= Tuning.EnemySpawning.WaveWarningSeconds)
		{
			float blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin((float)toWave * Mathf.Pi * 2f));
			DrawString(font, new Vector2(0f, 214f), "WAVE INCOMING", HorizontalAlignment.Center, width, 44,
				new Color(Warning, blink));
		}
		else if (!Fight.IsOver && Fight.Director.WavesSent > 0 && sinceWave < Tuning.EnemySpawning.WaveBannerSeconds)
		{
			DrawString(font, new Vector2(0f, 214f), $"WAVE {Fight.Director.WavesSent}", HorizontalAlignment.Center, width, 44,
				new Color(Danger, 1f - (float)(sinceWave / Tuning.EnemySpawning.WaveBannerSeconds)));
		}

		if (_alertLeft > 0f)
			DrawEscalationAlert(font);
	}

	/// <summary>Red bands along every edge, fading inward, and a large "!" in the middle. Pulses twice.</summary>
	private void DrawEscalationAlert(Font font)
	{
		float progress = 1f - _alertLeft / Tuning.Difficulty.EscalationAlertSeconds;
		float strength = Mathf.Abs(Mathf.Sin(progress * Mathf.Pi * 2f)) * (1f - progress * 0.5f);

		const int Bands = 14;
		const float BandWidth = 9f;
		for (int b = 0; b < Bands; b++)
		{
			float alpha = strength * 0.55f * (1f - (float)b / Bands);
			var color = new Color(Danger, alpha);
			float inset = b * BandWidth;
			DrawRect(new Rect2(inset, inset, Size.X - inset * 2f, BandWidth), color);
			DrawRect(new Rect2(inset, Size.Y - inset - BandWidth, Size.X - inset * 2f, BandWidth), color);
			DrawRect(new Rect2(inset, inset + BandWidth, BandWidth, Size.Y - (inset + BandWidth) * 2f), color);
			DrawRect(new Rect2(Size.X - inset - BandWidth, inset + BandWidth, BandWidth, Size.Y - (inset + BandWidth) * 2f), color);
		}

		DrawString(font, new Vector2(0f, Size.Y * 0.5f + 90f), "!", HorizontalAlignment.Center, Size.X, 260,
			new Color(Danger, Mathf.Min(1f, strength * 1.4f)));
	}
}
