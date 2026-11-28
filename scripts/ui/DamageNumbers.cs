using Godot;

/// <summary>
/// Small numbers that rise from damaged enemies and fade. Hits on the same enemy close together are added
/// into one number, so fast beams do not bury the screen in ones. Turned off in feedback.tres.
/// </summary>
public partial class DamageNumbers : Control
{
	private struct Number
	{
		public int EnemyId;
		public Vector3 World;   // Where it started, so it stays over the spot while the camera moves.
		public float Value;
		public float Age;
		public string Text;      // Value as shown, made again only when Value changes, not every frame.
		public float TextValue;
		public bool IsLabel;     // A fixed label in its own colour, such as drones built, rather than damage.
		public Color Color;
	}

	public Fight Fight;

	private Number[] _numbers = new Number[16];
	private int _count;

	private static FeedbackTuning Feedback => Tuning.Feedback;

	public override void _Ready()
	{
		// Run after the camera has moved this frame.
		ProcessPriority = UpdateOrder.Hud;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>Shows damage dealt to an enemy at a point on the plane.</summary>
	public void Add(int enemyId, Vector2 at, float amount)
	{
		if (!Feedback.DamageNumbers || amount <= 0f)
			return;

		// Add to a recent number on the same enemy, if there is one.
		for (int i = 0; i < _count; i++)
		{
			ref Number number = ref _numbers[i];
			if (number.EnemyId == enemyId && number.Age < Feedback.DamageNumberMergeSeconds)
			{
				number.Value += amount;
				return;
			}
		}

		if (_count >= Feedback.DamageNumberMax)
			return;
		if (_count == _numbers.Length)
			System.Array.Resize(ref _numbers, _numbers.Length * 2);
		_numbers[_count++] = new Number { EnemyId = enemyId, World = PlaneMath.ToWorld(at, 1f), Value = amount };
	}

	/// <summary>Shows a label rising from a point on the plane, in its own colour, larger than damage numbers, such as
	/// drones built by a New drones upgrade taken without the menu. Shown even with damage numbers off.</summary>
	public void AddLabel(Vector2 at, string text, Color color)
	{
		if (_count == _numbers.Length)
			System.Array.Resize(ref _numbers, _numbers.Length * 2);
		_numbers[_count++] = new Number { EnemyId = -1, World = PlaneMath.ToWorld(at, 1f), Text = text, IsLabel = true, Color = color };
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		float life = Feedback.DamageNumberTime;
		for (int i = _count - 1; i >= 0; i--)
		{
			_numbers[i].Age += dt;
			if (_numbers[i].Age >= life)
				_numbers[i] = _numbers[--_count];
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Fight == null || _count == 0)
			return;

		Font font = ThemeDB.FallbackFont;
		int size = Feedback.DamageNumberSize;
		float life = Feedback.DamageNumberTime;
		CameraRig camera = Fight.Camera;
		const float Width = 120f;

		for (int i = 0; i < _count; i++)
		{
			ref Number number = ref _numbers[i];
			if (camera.IsPositionBehind(number.World))
				continue;

			float t = number.Age / life;
			// A quick pop in size, then it rises and fades over the last part of its life.
			float pop = 1f + 0.35f * Mathf.Max(0f, 1f - t * 6f);
			float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
			int fontSize = Mathf.RoundToInt(size * pop * (number.IsLabel ? 1.6f : 1f));
			Vector2 screen = camera.UnprojectPosition(number.World) + new Vector2(-Width * 0.5f, -Feedback.DamageNumberRise * number.Age);
			if (!number.IsLabel && (number.Text == null || number.TextValue != number.Value))
			{
				number.TextValue = number.Value;
				number.Text = number.Value >= 1f ? Mathf.RoundToInt(number.Value).ToString() : number.Value.ToString("0.#");
			}
			string text = number.Text;

			DrawStringOutline(font, screen, text, HorizontalAlignment.Center, Width, fontSize, 5,
				new Color(0f, 0f, 0f, 0.8f * alpha));
			DrawString(font, screen, text, HorizontalAlignment.Center, Width, fontSize,
				new Color(number.IsLabel ? number.Color : Feedback.DamageNumberColor, alpha));
		}
	}
}
