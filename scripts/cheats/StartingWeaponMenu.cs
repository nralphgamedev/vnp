#if TOOLS
using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Playtest cheat, editor only: at the start of a fight, a menu to choose the starting VNP Weapon, then the
/// starting Drone Weapon, from the choices in in_game_cheats.tres, then the asteroid amount (ChooseAsteroidAmount),
/// where Random keeps the one the fight picked. The fight waits, paused, until every page is picked.
/// A page with no choices is skipped. A/D or the stick picks, Space or A chooses, and the mouse works too.
/// Turned on and off by ChooseStartingWeapon in in_game_cheats.tres. InGameCheats adds it to the HUD.
/// </summary>
public partial class StartingWeaponMenu : Control
{
	// Sizes in pixels at 1920 by 1080.
	private static readonly Vector2 CardSize = new(260f, 230f);
	private const float IconSize = 96f;

	/// <summary>One card: a weapon's ID (which also names its icon), its name and colour, and what picking it does.</summary>
	private sealed class Choice
	{
		public string Id;
		public string Name;
		public Color Color;
		public Action Pick;
	}

	/// <summary>One page of the menu: a title and its cards.</summary>
	private sealed class Page
	{
		public string Title;
		public List<Choice> Choices;
	}

	public Fight Fight;

	public bool IsOpen => Visible;

	private readonly Queue<Page> _pages = new();
	private Page _page;
	private readonly List<PanelContainer> _cards = new();
	private Label _title;
	private HBoxContainer _row;
	private int _selected;
	private bool _openedThisFrame;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;

		VBoxContainer column = Ui.DimmedColumn(this, 0.6f, 26);
		_title = Ui.Label("", 56, Colors.White);
		column.AddChild(_title);
		column.AddChild(Ui.Label("Playtest cheat. Turn this off with ChooseStartingWeapon in scripts/cheats/in_game_cheats.tres.", 24, new Color(1f, 1f, 1f, 0.6f)));

		_row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
		_row.AddThemeConstantOverride("separation", 24);
		column.AddChild(_row);

		column.AddChild(Ui.Label("A/D to choose  ·  Space or A to pick", 22, new Color(1f, 1f, 1f, 0.5f)));
	}

	/// <summary>
	/// Pauses the fight and asks for the starting VNP Weapon, then the starting Drone Weapon. A list that
	/// is empty is skipped; with both empty, nothing opens.
	/// </summary>
	public void Open(IEnumerable<WeaponDefinition> weapons, IEnumerable<DroneWeaponDefinition> droneWeapons,
		IEnumerable<AsteroidAmountDefinition> asteroidAmounts)
	{
		_pages.Clear();

		var weaponChoices = new List<Choice>();
		foreach (WeaponDefinition weapon in weapons)
		{
			if (weapon != null)
				weaponChoices.Add(new Choice { Id = weapon.Id, Name = weapon.Name, Color = weapon.Color, Pick = () => Fight.Equip(weapon) });
		}
		if (weaponChoices.Count > 0)
			_pages.Enqueue(new Page { Title = "CHOOSE A STARTING WEAPON", Choices = weaponChoices });

		var droneChoices = new List<Choice>();
		foreach (DroneWeaponDefinition weapon in droneWeapons)
		{
			if (weapon != null)
				droneChoices.Add(new Choice { Id = weapon.Id, Name = weapon.Name, Color = weapon.Color, Pick = () => Fight.Drones.SwitchWeapon(weapon) });
		}
		if (droneChoices.Count > 0)
			_pages.Enqueue(new Page { Title = "CHOOSE A DRONE WEAPON", Choices = droneChoices });

		// Random first, so the fight's own pick is one press away.
		var amountChoices = new List<Choice>();
		foreach (AsteroidAmountDefinition amount in asteroidAmounts)
		{
			if (amount != null)
				amountChoices.Add(new Choice { Id = amount.Id, Name = amount.Name, Color = Tuning.Asteroids.Color.Lightened(0.35f),
					Pick = () => Fight.ChooseAsteroidAmount(amount) });
		}
		if (amountChoices.Count > 0)
		{
			amountChoices.Insert(0, new Choice { Id = "random", Name = "Random", Color = Colors.White, Pick = () => { } });
			_pages.Enqueue(new Page { Title = "CHOOSE THE ASTEROID AMOUNT", Choices = amountChoices });
		}

		if (_pages.Count == 0)
			return;
		Visible = true;
		GetTree().Paused = true;
		NextPage();
	}

	public override void _Process(double delta)
	{
		if (!Visible)
			return;
		// Ignore a key already held when the page appeared.
		if (_openedThisFrame)
		{
			_openedThisFrame = false;
			return;
		}

		if (Input.IsActionJustPressed("move_left")) Select(_selected - 1);
		if (Input.IsActionJustPressed("move_right")) Select(_selected + 1);
		if (Input.IsActionJustPressed("menu_select")) Choose(_selected);
	}

	/// <summary>Shows the next page, or closes the menu and starts the fight when there are none left.</summary>
	private void NextPage()
	{
		if (_pages.Count == 0)
		{
			Visible = false;
			GetTree().Paused = false;
			return;
		}
		_page = _pages.Dequeue();
		_title.Text = _page.Title;
		BuildCards();
		_selected = 0;
		_openedThisFrame = true;
		Refresh();
	}

	private void Select(int index)
	{
		index = Mathf.Clamp(index, 0, _page.Choices.Count - 1);
		if (index == _selected)
			return;
		_selected = index;
		Fight.Sounds.Play(Sound.MenuMove);
		Refresh();
	}

	private void Choose(int index)
	{
		if (index < 0 || index >= _page.Choices.Count)
			return;
		Fight.Sounds.Play(Sound.UpgradeChoose);
		_page.Choices[index].Pick();
		NextPage();
	}

	private void Refresh()
	{
		for (int i = 0; i < _cards.Count; i++)
		{
			Color color = _page.Choices[i].Color;
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
		// Disposed straight away: a Godot array left for .NET to clean up later can be cleaned up after Godot has
		// shut down, which crashes the game as it quits.
		var children = _row.GetChildren();
		using ((Godot.Collections.Array)children)
		{
			foreach (Node child in children)
			{
				_row.RemoveChild(child);
				child.QueueFree();
			}
		}
		_cards.Clear();

		for (int i = 0; i < _page.Choices.Count; i++)
		{
			Choice choice = _page.Choices[i];
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

			string iconPath = $"res://placeholder/textures/icons/{choice.Id}.svg";
			if (ResourceLoader.Exists(iconPath))
			{
				inside.AddChild(new TextureRect
				{
					Texture = Placeholders.Icon(choice.Id),
					Modulate = choice.Color,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					CustomMinimumSize = new Vector2(IconSize, IconSize),
					SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
					MouseFilter = MouseFilterEnum.Ignore,
				});
			}

			Label name = Ui.Label(choice.Name, 30, Colors.White);
			name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			name.CustomMinimumSize = new Vector2(CardSize.X - 50f, 0f);
			inside.AddChild(name);
		}
	}
}
#endif
