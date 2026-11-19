using Godot;
using System.Collections.Generic;

/// <summary>
/// For testing: at the start of a fight, a menu to choose which VNP Weapon the VNP starts with, from the
/// choices in vnp.tres. The fight waits, paused, until one is picked. A/D or the stick picks, Space or A
/// chooses, and the mouse works too. Turned on and off by ChooseStartingWeapon in vnp.tres.
/// </summary>
public partial class StartingWeaponMenu : Control
{
	// Sizes in pixels at 1920 by 1080.
	private static readonly Vector2 CardSize = new(260f, 230f);
	private const float IconSize = 96f;

	public Fight Fight;

	public bool IsOpen => Visible;

	private readonly List<WeaponDefinition> _choices = new();
	private readonly List<PanelContainer> _cards = new();
	private HBoxContainer _row;
	private int _selected;
	private bool _openedThisFrame;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;

		VBoxContainer column = Ui.DimmedColumn(this, 0.6f, 26);
		column.AddChild(Ui.Label("CHOOSE A STARTING WEAPON", 56, Colors.White));
		column.AddChild(Ui.Label("For testing. Turn this off with ChooseStartingWeapon in vnp.tres.", 24, new Color(1f, 1f, 1f, 0.6f)));

		_row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
		_row.AddThemeConstantOverride("separation", 24);
		column.AddChild(_row);

		column.AddChild(Ui.Label("A/D to choose  ·  Space or A to pick", 22, new Color(1f, 1f, 1f, 0.5f)));
	}

	/// <summary>Shows the choices and pauses the fight until one is picked.</summary>
	public void Open(IEnumerable<WeaponDefinition> choices)
	{
		_choices.Clear();
		foreach (WeaponDefinition weapon in choices)
		{
			if (weapon != null)
				_choices.Add(weapon);
		}
		BuildCards();
		_selected = 0;
		Refresh();
		Visible = true;
		_openedThisFrame = true;
		GetTree().Paused = true;
	}

	public override void _Process(double delta)
	{
		if (!Visible)
			return;
		// Ignore a key already held when the fight started.
		if (_openedThisFrame)
		{
			_openedThisFrame = false;
			return;
		}

		if (Input.IsActionJustPressed("move_left")) Select(_selected - 1);
		if (Input.IsActionJustPressed("move_right")) Select(_selected + 1);
		if (Input.IsActionJustPressed("menu_select")) Choose(_selected);
	}

	private void Select(int index)
	{
		index = Mathf.Clamp(index, 0, _choices.Count - 1);
		if (index == _selected)
			return;
		_selected = index;
		Fight.Sounds.Play(Sound.MenuMove);
		Refresh();
	}

	private void Choose(int index)
	{
		if (index < 0 || index >= _choices.Count)
			return;
		Fight.Sounds.Play(Sound.UpgradeChoose);
		Visible = false;
		GetTree().Paused = false;
		Fight.Equip(_choices[index]);
	}

	private void Refresh()
	{
		for (int i = 0; i < _cards.Count; i++)
		{
			Color color = _choices[i].Color;
			var background = new Color(0.07f, 0.09f, 0.15f, 0.96f);
			StyleBoxFlat style = i == _selected
				? Ui.Panel(background.Lerp(color, 0.2f), color)
				: Ui.Panel(background, color.Darkened(0.5f));
			if (i == _selected)
				style.SetBorderWidthAll(6);
			_cards[i].AddThemeStyleboxOverride("panel", style);
		}
	}

	/// <summary>One card per weapon: its icon in its colour, and its name.</summary>
	private void BuildCards()
	{
		foreach (Node child in _row.GetChildren())
			child.QueueFree();
		_cards.Clear();

		for (int i = 0; i < _choices.Count; i++)
		{
			WeaponDefinition weapon = _choices[i];
			int index = i;
			var card = new PanelContainer { CustomMinimumSize = CardSize };
			card.MouseEntered += () => Select(index);
			card.GuiInput += input =>
			{
				if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
					Choose(index);
			};
			_row.AddChild(card);
			_cards.Add(card);

			var inside = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
			inside.AddThemeConstantOverride("separation", 18);
			card.AddChild(inside);

			string iconPath = $"res://placeholder/textures/icons/{weapon.Id}.svg";
			if (ResourceLoader.Exists(iconPath))
			{
				var icon = new TextureRect
				{
					Texture = Placeholders.Icon(weapon.Id),
					Modulate = weapon.Color,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					CustomMinimumSize = new Vector2(IconSize, IconSize),
					SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
					MouseFilter = MouseFilterEnum.Ignore,
				};
				inside.AddChild(icon);
			}

			Label name = Ui.Label(weapon.Name, 30, Colors.White);
			name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			name.CustomMinimumSize = new Vector2(CardSize.X - 50f, 0f);
			inside.AddChild(name);
		}
	}
}
