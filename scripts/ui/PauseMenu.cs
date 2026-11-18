using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// The pause menu. Esc or Start pauses the fight. The main page has Resume and Settings, with the fight
/// upgrades taken so far listed on the right of the screen. Settings opens its own page, grouped by category; Sound has the
/// Master and Effects volumes. Played one-handed: W/S or the stick picks a row, A/D changes a volume,
/// Space or A picks. Esc or Start resumes from the main page and goes back from Settings. The mouse works too.
/// </summary>
public partial class PauseMenu : Control
{
	// Sizes in pixels at 1920 by 1080.
	private static readonly Vector2 RowSize = new(620f, 64f);
	private const float VolumeStep = 0.1f;

	private static readonly Color Background = new(0.07f, 0.09f, 0.15f, 0.96f);
	private static readonly Color Border = new(0.3f, 0.34f, 0.45f);
	private static readonly Color Hint = new(1f, 1f, 1f, 0.5f);

	/// <summary>One line in a menu page: a button, or a volume with a bar.</summary>
	private sealed class Row
	{
		public PanelContainer Panel;
		public ProgressBar Bar;
		public Label Value;
		public Action Activate;
		public Func<float> Get;
		public Action<float> Set;
		public bool IsVolume => Bar != null;
	}

	private enum Page { Main, Settings }

	public Fight Fight;

	public bool IsOpen => Visible;

	private UpgradeList _upgrades;
	private bool _openedThisFrame;
	private Page _page;
	private int _selected;
	private VBoxContainer _mainPage;
	private VBoxContainer _settingsPage;
	private readonly List<Row> _mainRows = new();
	private readonly List<Row> _settingsRows = new();
	private StyleBoxFlat _normalStyle;
	private StyleBoxFlat _selectedStyle;

	private List<Row> Rows => _page == Page.Main ? _mainRows : _settingsRows;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;

		_normalStyle = Ui.Panel(Background, Border);
		_selectedStyle = Ui.Panel(new Color(0.13f, 0.11f, 0.22f, 0.98f), Tuning.Look.ExperienceColor);

		VBoxContainer column = Ui.DimmedColumn(this, 0.65f, 22);
		_upgrades = new UpgradeList();
		AddChild(_upgrades);
		_mainPage = BuildMainPage();
		_settingsPage = BuildSettingsPage();
		column.AddChild(_mainPage);
		column.AddChild(_settingsPage);
	}

	public void Open()
	{
		_upgrades.ShowUpgrades(Fight);
		ShowPage(Page.Main, 0);
		Visible = true;
		_openedThisFrame = true;
		GetTree().Paused = true;
		Fight.Sounds.Play(Sound.MenuSelect);
	}

	public void Close()
	{
		Fight.Sounds.Play(Sound.MenuSelect);
		Visible = false;
		GetTree().Paused = false;
	}

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		if (!Visible)
		{
			if (Input.IsActionJustPressed("pause") && Fight.CanPause)
				Open();
			return;
		}

		if (_openedThisFrame)
		{
			_openedThisFrame = false;
			return;
		}

		if (Input.IsActionJustPressed("pause"))
		{
			if (_page == Page.Settings)
				GoBack();
			else
				Close();
			return;
		}
		if (Input.IsActionJustPressed("move_up")) Move(-1);
		if (Input.IsActionJustPressed("move_down")) Move(1);
		if (Input.IsActionJustPressed("move_left")) Adjust(-1);
		if (Input.IsActionJustPressed("move_right")) Adjust(1);
		if (Input.IsActionJustPressed("menu_select")) Rows[_selected].Activate?.Invoke();
	}

	// ───────────── Moving around ─────────────

	private void ShowPage(Page page, int selected)
	{
		_page = page;
		_mainPage.Visible = page == Page.Main;
		_settingsPage.Visible = page == Page.Settings;
		_selected = Mathf.Clamp(selected, 0, Rows.Count - 1);
		Refresh();
	}

	private void OpenSettings()
	{
		Fight.Sounds.Play(Sound.MenuSelect);
		ShowPage(Page.Settings, 0);
	}

	private void GoBack()
	{
		Fight.Sounds.Play(Sound.MenuSelect);
		ShowPage(Page.Main, _mainRows.Count - 1); // Back onto the Settings row.
	}

	private void Move(int step)
	{
		int next = Mathf.Clamp(_selected + step, 0, Rows.Count - 1);
		if (next == _selected)
			return;
		_selected = next;
		Fight.Sounds.Play(Sound.MenuMove);
		Refresh();
	}

	/// <summary>Turns the highlighted volume up or down a step. The tick plays at the new volume, so it can be heard.</summary>
	private void Adjust(int direction)
	{
		Row row = Rows[_selected];
		if (!row.IsVolume)
			return;
		SetVolume(row, Mathf.Snapped(row.Get() + direction * VolumeStep, VolumeStep));
	}

	private void SetVolume(Row row, float value)
	{
		float before = row.Get();
		row.Set(Mathf.Clamp(value, 0f, 1f));
		if (!Mathf.IsEqualApprox(before, row.Get()))
			Fight.Sounds.Play(Sound.MenuMove);
		Refresh();
	}

	private void Refresh()
	{
		for (int i = 0; i < Rows.Count; i++)
		{
			Row row = Rows[i];
			row.Panel.AddThemeStyleboxOverride("panel", i == _selected ? _selectedStyle : _normalStyle);
			if (row.IsVolume)
			{
				row.Bar.Value = row.Get();
				row.Value.Text = $"{Mathf.RoundToInt(row.Get() * 100f)}%";
			}
		}
	}

	// ───────────── Pages ─────────────

	private VBoxContainer BuildMainPage()
	{
		var page = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		page.AddThemeConstantOverride("separation", 22);
		page.AddChild(Ui.Label("PAUSED", 64, Colors.White));

		AddButton(page, _mainRows, "Resume", Close);
		AddButton(page, _mainRows, "Settings", OpenSettings);
		page.AddChild(Ui.Label("W/S to choose  ·  Space or A to pick  ·  Esc or Start to resume", 22, Hint));
		return page;
	}

	private VBoxContainer BuildSettingsPage()
	{
		var page = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Visible = false };
		page.AddThemeConstantOverride("separation", 22);
		page.AddChild(Ui.Label("SETTINGS", 64, Colors.White));

		// Each category is a heading with its settings under it. Sound is the only one so far.
		page.AddChild(Ui.Label("Sound", 30, Tuning.Look.ExperienceColor));
		AddVolume(page, _settingsRows, "Master", () => Settings.MasterVolume, v => Settings.MasterVolume = v);
		AddVolume(page, _settingsRows, "Effects", () => Settings.EffectsVolume, v => Settings.EffectsVolume = v);

		AddButton(page, _settingsRows, "Back", GoBack);
		page.AddChild(Ui.Label("W/S to choose  ·  A/D to change  ·  Esc or Start to go back", 22, Hint));
		return page;
	}

	private void AddButton(VBoxContainer page, List<Row> rows, string text, Action activate)
	{
		var row = new Row { Activate = activate };
		row.Panel = MakeRowPanel(rows, row);
		var label = Ui.Label(text, 30, Colors.White);
		label.VerticalAlignment = VerticalAlignment.Center;
		row.Panel.AddChild(label);
		AddRow(page, rows, row);
	}

	private void AddVolume(VBoxContainer page, List<Row> rows, string text, Func<float> get, Action<float> set)
	{
		var row = new Row { Get = get, Set = set };
		row.Panel = MakeRowPanel(rows, row);

		var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 20);
		row.Panel.AddChild(line);

		var name = Ui.Label(text, 30, Colors.White);
		name.HorizontalAlignment = HorizontalAlignment.Left;
		name.CustomMinimumSize = new Vector2(150f, 0f);
		line.AddChild(name);

		row.Bar = new ProgressBar
		{
			MinValue = 0, MaxValue = 1, Step = 0.01, ShowPercentage = false,
			CustomMinimumSize = new Vector2(300f, 18f),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.Bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.6f) });
		row.Bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = Tuning.Look.ExperienceColor });
		line.AddChild(row.Bar);

		row.Value = Ui.Label("", 28, new Color(1f, 1f, 1f, 0.85f));
		row.Value.HorizontalAlignment = HorizontalAlignment.Right;
		row.Value.CustomMinimumSize = new Vector2(80f, 0f);
		line.AddChild(row.Value);

		AddRow(page, rows, row);
	}

	/// <summary>A highlightable row. The mouse highlights it on hover; a click picks it, or for a volume,
	/// sets the volume to where the bar was clicked.</summary>
	private PanelContainer MakeRowPanel(List<Row> rows, Row row)
	{
		var panel = new PanelContainer { CustomMinimumSize = RowSize };
		panel.MouseEntered += () =>
		{
			_selected = rows.IndexOf(row);
			Refresh();
		};
		panel.GuiInput += input =>
		{
			if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
				return;
			if (row.IsVolume)
			{
				Rect2 bar = row.Bar.GetGlobalRect();
				Vector2 at = panel.GetGlobalTransform() * click.Position;
				if (bar.Grow(10f).HasPoint(at))
					SetVolume(row, Mathf.Snapped((at.X - bar.Position.X) / bar.Size.X, VolumeStep));
			}
			else
			{
				row.Activate?.Invoke();
			}
		};
		return panel;
	}

	private static void AddRow(VBoxContainer page, List<Row> rows, Row row)
	{
		// Centre the fixed-width row in the page.
		var holder = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
		holder.AddChild(row.Panel);
		page.AddChild(holder);
		rows.Add(row);
	}
}
