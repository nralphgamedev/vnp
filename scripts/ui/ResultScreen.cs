using Godot;

/// <summary>
/// Shown when the fight ends: win, loss or retreat, with kills, time and fight experience harvested.
/// Space or the A button starts a new fight straight away.
/// </summary>
public partial class ResultScreen : Control
{
	public Fight Fight;

	public bool IsOpen => Visible;

	private Label _title;
	private Label _subtitle;
	private Label _stats;
	private Label _upgrades;
	private double _shownAt;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;

		VBoxContainer column = Ui.DimmedColumn(this, 0.75f, 24);
		_title = Ui.Label("", 80, Colors.White);
		column.AddChild(_title);
		_subtitle = Ui.Label("", 28, new Color(1f, 1f, 1f, 0.75f));
		column.AddChild(_subtitle);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(620f, 0f) };
		panel.AddThemeStyleboxOverride("panel", Ui.Panel(new Color(0.07f, 0.09f, 0.15f, 0.96f), new Color(0.3f, 0.34f, 0.45f)));
		column.AddChild(panel);
		var inside = new VBoxContainer();
		inside.AddThemeConstantOverride("separation", 18);
		panel.AddChild(inside);
		_stats = Ui.Label("", 32, Colors.White);
		inside.AddChild(_stats);
		_upgrades = Ui.Label("", 22, new Color(1f, 1f, 1f, 0.6f));
		_upgrades.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_upgrades.CustomMinimumSize = new Vector2(570f, 0f);
		inside.AddChild(_upgrades);

		column.AddChild(Ui.Label("Space or A for a new fight", 30, Tuning.Look.GatewayColor));
	}

	public void Show(Fight.Outcome outcome)
	{
		(string title, string subtitle, Color color) = outcome switch
		{
			Fight.Outcome.Win => ("VICTORY", "Kill target reached", new Color(0.4f, 1f, 0.55f)),
			Fight.Outcome.Retreat => ("RETREATED", "Left through the gateway", Tuning.Look.GatewayColor),
			_ => ("VNP DESTROYED", "The fight is lost", Tuning.EnemySpawning.Enemy.Color),
		};
		_title.Text = title;
		_title.AddThemeColorOverride("font_color", color);
		_subtitle.Text = subtitle;

		_stats.Text =
			$"Kills   {Fight.Kills} / {Tuning.Fight.KillTarget}\n" +
			$"Time   {Ui.Clock(Fight.Elapsed)}\n" +
			$"Fight experience harvested   {Fight.Experience:0}\n" +
			$"Level reached   {Fight.Level}";
		_upgrades.Text = "Upgrades:  " + PauseMenu.Describe(Fight.TakenUpgrades).Replace("\n", ",  ");

		Visible = true;
		_shownAt = Time.GetTicksMsec() / 1000.0;
	}

	public override void _Process(double delta)
	{
		if (!Visible)
			return;

		// A short delay, so a button already being pressed when the fight ended does not skip the screen.
		if (Time.GetTicksMsec() / 1000.0 - _shownAt < 0.6)
			return;

		if (Input.IsActionJustPressed("menu_select"))
		{
			SceneTree tree = GetTree();
			tree.Paused = false;
			tree.ReloadCurrentScene();
		}
	}
}
