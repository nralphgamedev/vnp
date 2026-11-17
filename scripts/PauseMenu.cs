using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The pause menu: Esc or Start pauses the fight and lists the fight upgrades taken so far.
/// Esc, Start, Space or the A button resumes.
/// </summary>
public partial class PauseMenu : Control
{
	public Fight Fight;

	public bool IsOpen => Visible;

	private Label _upgrades;
	private bool _openedThisFrame;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;

		VBoxContainer column = Ui.DimmedColumn(this, 0.65f, 22);
		column.AddChild(Ui.Label("PAUSED", 64, Colors.White));
		column.AddChild(Ui.Label("Fight upgrades", 30, Tuning.ExperienceColor));

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(620f, 0f) };
		panel.AddThemeStyleboxOverride("panel", Ui.Panel(new Color(0.07f, 0.09f, 0.15f, 0.96f), new Color(0.3f, 0.34f, 0.45f)));
		column.AddChild(panel);
		_upgrades = Ui.Label("", 28, new Color(1f, 1f, 1f, 0.9f));
		panel.AddChild(_upgrades);

		column.AddChild(Ui.Label("Esc / Start or Space / A to resume", 22, new Color(1f, 1f, 1f, 0.5f)));
	}

	public void Open()
	{
		_upgrades.Text = Describe(Fight.TakenUpgrades);
		Visible = true;
		_openedThisFrame = true;
		GetTree().Paused = true;
	}

	public void Close()
	{
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
		if (Input.IsActionJustPressed("pause") || Input.IsActionJustPressed("menu_select"))
			Close();
	}

	/// <summary>One line per upgrade, in the order first taken, with a count when taken more than once.</summary>
	public static string Describe(IReadOnlyList<string> taken)
	{
		if (taken.Count == 0)
			return "None yet";

		return string.Join("\n", taken
			.GroupBy(name => name)
			.Select(group => group.Count() > 1 ? $"{group.Key}  ×{group.Count()}" : group.Key));
	}
}
