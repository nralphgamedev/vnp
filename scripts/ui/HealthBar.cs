using Godot;

/// <summary>A small health bar drawn just below the VNP on screen. At low health it pulses.</summary>
public partial class HealthBar : Control
{
	// Sizes in pixels at 1920 by 1080.
	private static readonly Vector2 BarSize = new(106f, 12f);
	private const float BelowVnp = 56f; // From the VNP's center down to the bar.

	public Fight Fight;

	private float _pulseTime;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Hud;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public override void _Process(double delta)
	{
		_pulseTime += (float)delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Fight == null || Fight.Vnp.IsDestroyed)
			return;

		Vnp vnp = Fight.Vnp;
		Vector2 onScreen = Fight.Camera.UnprojectPosition(vnp.GlobalPosition);
		var bar = new Rect2(onScreen + new Vector2(-BarSize.X * 0.5f, BelowVnp), BarSize);
		float filled = vnp.Health / vnp.MaxHealth;

		// Green when healthy, through yellow, to red when nearly destroyed.
		Color fill = filled > 0.5f
			? new Color(0.9f, 0.85f, 0.2f).Lerp(new Color(0.3f, 0.9f, 0.4f), (filled - 0.5f) * 2f)
			: new Color(0.95f, 0.25f, 0.2f).Lerp(new Color(0.9f, 0.85f, 0.2f), filled * 2f);

		// Low health: the bar swells and glows red, over and over.
		if (vnp.IsLowHealth)
		{
			float period = Mathf.Max(0.1f, Tuning.Feedback.LowHealthPulseSeconds);
			float pulse = 0.5f + 0.5f * Mathf.Cos(_pulseTime / period * Mathf.Tau);
			Vector2 center = bar.GetCenter();
			bar = new Rect2(center - bar.Size * (0.5f + 0.1f * pulse), bar.Size * (1f + 0.2f * pulse));
			DrawRect(bar.Grow(5f + 4f * pulse), new Color(1f, 0.15f, 0.1f, 0.35f * pulse));
			fill = fill.Lerp(Colors.White, 0.35f * pulse);
		}

		DrawRect(bar.Grow(2.5f), new Color(0f, 0f, 0f, 0.7f));
		DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * filled, bar.Size.Y)), fill);
	}
}
