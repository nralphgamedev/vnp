using Godot;

/// <summary>A small health bar drawn just below the VNP on screen.</summary>
public partial class HealthBar : Control
{
	private static readonly Vector2 BarSize = new(64f, 7f);
	private const float BelowVnp = 34f; // Pixels from the VNP's center down to the bar.

	public Fight Fight;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Hud;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public override void _Process(double delta) => QueueRedraw();

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

		DrawRect(bar.Grow(1.5f), new Color(0f, 0f, 0f, 0.7f));
		DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * filled, bar.Size.Y)), fill);
	}
}
