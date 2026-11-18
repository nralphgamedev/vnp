using Godot;

/// <summary>
/// The ring that spreads from the VNP when a level-up offer appears. Drawn on the screen over the dimmed
/// fight and under the cards, and it keeps moving while the fight is paused for the offer.
/// </summary>
public partial class LevelUpPulse : Control
{
	// Sizes in pixels at 1920 by 1080.
	private const float EndRadius = 700f;
	private const float Thickness = 14f;

	private Vector2 _center;
	private float _age = float.MaxValue;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>Starts a ring at a point on the screen.</summary>
	public void Start(Vector2 center)
	{
		_center = center;
		_age = 0f;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (_age > Tuning.Feedback.LevelUpPulseTime)
			return;
		_age += (float)delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		float life = Tuning.Feedback.LevelUpPulseTime;
		if (life <= 0f || _age > life)
			return;

		// It races out and slows as it fades, with a fainter echo just behind it.
		float t = _age / life;
		float spread = 1f - (1f - t) * (1f - t);
		Color color = Tuning.Look.ExperienceColor;
		DrawArc(_center, EndRadius * spread, 0f, Mathf.Tau, 96, new Color(color, 1f - t), Thickness * (1f - 0.5f * t), true);
		DrawArc(_center, EndRadius * spread * 0.8f, 0f, Mathf.Tau, 96, new Color(color, 0.4f * (1f - t)), Thickness * 0.5f, true);
	}
}
