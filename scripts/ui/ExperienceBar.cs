using Godot;

/// <summary>The current level and the fight experience toward the next one, along the bottom of the screen.</summary>
public partial class ExperienceBar : Control
{
	// Sizes in pixels at 1920 by 1080.
	private const float Margin = 40f;
	private const float BarHeight = 14f;
	private const int FontSize = 26;

	public Fight Fight;

	// Drawn every frame, but made again only when the numbers shown change, so the bar makes no garbage most frames.
	private int _levelShown = -1;
	private string _levelText = "";
	private long _experienceShown = long.MinValue;
	private string _experienceText = "";

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Hud;
		// Keep redrawing while a level-up offer has the fight paused, so the bar shows the new level.
		ProcessMode = ProcessModeEnum.Always;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		if (Fight == null)
			return;

		var bar = new Rect2(Margin, Size.Y - Margin - BarHeight, Size.X - Margin * 2f, BarHeight);
		float filled = Mathf.Clamp(Fight.ExperienceIntoLevel / Fight.ExperienceForNextLevel, 0f, 1f);

		DrawRect(bar.Grow(3f), new Color(0f, 0f, 0f, 0.6f));
		DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * filled, bar.Size.Y)), Tuning.Look.ExperienceColor);

		Font font = ThemeDB.FallbackFont;
		if (Fight.Level != _levelShown)
		{
			_levelShown = Fight.Level;
			_levelText = $"Level {Fight.Level}";
		}
		DrawString(font, bar.Position + new Vector2(0f, -12f), _levelText,
			HorizontalAlignment.Left, -1, FontSize, Tuning.Look.ExperienceColor);
		// The two numbers as they are shown, rounded to whole numbers, packed into one key.
		long experienceKey = (long)System.Math.Round(Fight.ExperienceIntoLevel) * 1_000_000_000L
			+ (long)System.Math.Round(Fight.ExperienceForNextLevel);
		if (experienceKey != _experienceShown)
		{
			_experienceShown = experienceKey;
			_experienceText = $"{Fight.ExperienceIntoLevel:0} / {Fight.ExperienceForNextLevel:0}";
		}
		DrawString(font, bar.Position + new Vector2(0f, -12f), _experienceText,
			HorizontalAlignment.Right, bar.Size.X, FontSize - 6, new Color(1f, 1f, 1f, 0.6f));
	}
}
