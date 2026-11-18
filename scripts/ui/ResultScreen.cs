using Godot;
using System.Collections.Generic;

/// <summary>
/// Shown when the fight ends: win, loss or retreat, with kills, time and fight experience harvested, and the
/// upgrades taken listed on the right.
///
/// After a win, two buttons sit side by side: New fight, and Continue (endless), which goes back into the
/// same fight with no kill target, for as long as the player lasts and with no additional rewards. A/D or
/// the stick picks, Space or A chooses, and the mouse works too. When an endless run ends, by retreating or
/// being destroyed, it is still shown as a victory. Otherwise Space or A starts a new fight.
/// </summary>
public partial class ResultScreen : Control
{
	// Sizes in pixels at 1920 by 1080.
	private static readonly Vector2 ButtonSize = new(300f, 70f);

	private static readonly Color VictoryColor = new(0.4f, 1f, 0.55f);

	public Fight Fight;

	public bool IsOpen => Visible;

	private Label _title;
	private Label _subtitle;
	private Label _stats;
	private UpgradeList _upgrades;
	private Label _newFightHint;
	private Control _choices;
	private readonly List<PanelContainer> _buttons = new();
	private int _selected;
	private bool _offerEndless;
	private double _shownAt;
	private StyleBoxFlat _normalStyle;
	private StyleBoxFlat _selectedStyle;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;

		_normalStyle = Ui.Panel(new Color(0.07f, 0.09f, 0.15f, 0.96f), new Color(0.3f, 0.34f, 0.45f));
		_selectedStyle = Ui.Panel(new Color(0.13f, 0.11f, 0.22f, 0.98f), Tuning.Look.ExperienceColor);

		VBoxContainer column = Ui.DimmedColumn(this, 0.75f, 24);
		_title = Ui.Label("", 80, Colors.White);
		column.AddChild(_title);
		_subtitle = Ui.Label("", 28, new Color(1f, 1f, 1f, 0.75f));
		column.AddChild(_subtitle);

		// Keeps its own width rather than stretching to the widest line on the screen.
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(620f, 0f), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
		panel.AddThemeStyleboxOverride("panel", Ui.Panel(new Color(0.07f, 0.09f, 0.15f, 0.96f), new Color(0.3f, 0.34f, 0.45f)));
		column.AddChild(panel);
		var inside = new VBoxContainer();
		inside.AddThemeConstantOverride("separation", 18);
		panel.AddChild(inside);
		_stats = Ui.Label("", 32, Colors.White);
		inside.AddChild(_stats);

		_newFightHint = Ui.Label("Space or A for a new fight", 30, Tuning.Look.GatewayColor);
		column.AddChild(_newFightHint);
		_choices = BuildChoices();
		column.AddChild(_choices);

		// The upgrades taken, on the right of the screen.
		_upgrades = new UpgradeList();
		AddChild(_upgrades);
	}

	public void Show(Fight.Outcome outcome)
	{
		// A win offers endless mode. Once an endless run ends, however it ends, the fight was still won.
		bool endlessOver = Fight.IsEndless;
		_offerEndless = outcome == Fight.Outcome.Win && !endlessOver;

		(string title, string subtitle, Color color) = endlessOver
			? ("VICTORY", "Endless run complete", VictoryColor)
			: outcome switch
			{
				Fight.Outcome.Win => ("VICTORY", "Kill target reached", VictoryColor),
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
		if (endlessOver)
		{
			_stats.Text +=
				$"\nKill target reached at   {Ui.Clock(Fight.VictoryTime)}\n" +
				$"Endless kills   {Fight.Kills - Fight.VictoryKills}";
		}
		_upgrades.ShowUpgrades(Fight);

		_newFightHint.Visible = !_offerEndless;
		_choices.Visible = _offerEndless;
		_selected = 0;
		RefreshButtons();

		Visible = true;
		_shownAt = Time.GetTicksMsec() / 1000.0;
		Fight.Sounds.Play(endlessOver || outcome == Fight.Outcome.Win ? Sound.Win
			: outcome == Fight.Outcome.Retreat ? Sound.Retreat
			: Sound.Loss);
	}

	public override void _Process(double delta)
	{
		if (!Visible)
			return;

		// A short delay, so a button already being pressed when the fight ended does not skip the screen.
		if (Time.GetTicksMsec() / 1000.0 - _shownAt < 0.6)
			return;

		if (_offerEndless)
		{
			if (Input.IsActionJustPressed("move_left")) Select(0);
			if (Input.IsActionJustPressed("move_right")) Select(1);
		}
		if (Input.IsActionJustPressed("menu_select"))
			Choose(_offerEndless ? _selected : 0);
	}

	private void Select(int index)
	{
		if (index == _selected)
			return;
		_selected = index;
		Fight.Sounds.Play(Sound.MenuMove);
		RefreshButtons();
	}

	/// <summary>0 starts a new fight; 1 continues this one in endless mode.</summary>
	private void Choose(int index)
	{
		if (index == 1 && _offerEndless)
		{
			Fight.Sounds.Play(Sound.MenuSelect);
			Visible = false;
			Fight.ContinueEndless();
			return;
		}

		SceneTree tree = GetTree();
		tree.Paused = false;
		tree.ReloadCurrentScene();
	}

	private void RefreshButtons()
	{
		for (int i = 0; i < _buttons.Count; i++)
			_buttons[i].AddThemeStyleboxOverride("panel", i == _selected ? _selectedStyle : _normalStyle);
	}

	/// <summary>New fight and Continue (endless) side by side, with what endless mode means under them.</summary>
	private Control BuildChoices()
	{
		var holder = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		holder.AddThemeConstantOverride("separation", 14);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 20);
		holder.AddChild(row);
		AddButton(row, "New fight", 0);
		AddButton(row, "Continue (endless)", 1);

		holder.AddChild(Ui.Label("Endless mode: keep fighting for as long as you last. No additional rewards.", 22,
			new Color(1f, 1f, 1f, 0.7f)));
		holder.AddChild(Ui.Label("A/D to choose  ·  Space or A to pick", 22, new Color(1f, 1f, 1f, 0.5f)));
		return holder;
	}

	private void AddButton(HBoxContainer row, string text, int index)
	{
		var button = new PanelContainer { CustomMinimumSize = ButtonSize };
		button.MouseEntered += () => Select(index);
		button.GuiInput += input =>
		{
			if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
				&& Time.GetTicksMsec() / 1000.0 - _shownAt >= 0.6)
				Choose(index);
		};
		Label label = Ui.Label(text, 30, Colors.White);
		label.VerticalAlignment = VerticalAlignment.Center;
		button.AddChild(label);
		row.AddChild(button);
		_buttons.Add(button);
	}
}
