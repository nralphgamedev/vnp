using Godot;
using System.Collections.Generic;

/// <summary>
/// The level-up offer. It pauses the fight and shows a choice of upgrades from the pool,
/// with a reroll below them. Played one-handed: WASD or the stick moves the highlight,
/// Space or the A / Cross button picks. The mouse works too.
/// </summary>
public partial class LevelUpMenu : Control
{
	// Sizes in pixels at 1920 by 1080.
	private static readonly Vector2 CardSize = new(420f, 260f);
	private static readonly Vector2 RerollSize = new(420f, 76f);

	public Fight Fight;

	public bool IsOpen => Visible;

	/// <summary>The upgrades on offer right now.</summary>
	public IReadOnlyList<UpgradeDefinition> Options => _options;

	/// <summary>Which card is highlighted. The reroll is the one after the last option.</summary>
	public int Selected => _selected;

	private List<UpgradeDefinition> _options = new();
	private int _selected;
	private int _lastOption;
	private Label _title;
	private readonly List<PanelContainer> _cards = new();
	private readonly List<Label> _cardTags = new();
	private readonly List<Label> _cardNames = new();
	private readonly List<Label> _cardTexts = new();
	private PanelContainer _rerollCard;
	private Label _rerollLabel;
	private StyleBoxFlat _normalStyle;
	private StyleBoxFlat _selectedStyle;
	private StyleBoxFlat _disabledStyle;
	private LevelUpPulse _pulse;
#if TOOLS
	private bool _cheatShown; // Editor only: whether the reroll currently shows the Alt cheat.
#endif

	// Card styles for each category, normal and highlighted, made on first use.
	private readonly Dictionary<(UpgradeCategory, bool), StyleBoxFlat> _cardStyles = new();

	public override void _Ready()
	{
		// Keep running while the fight is paused.
		ProcessMode = ProcessModeEnum.Always;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;
		BuildLayout();
	}

	/// <summary>Pauses the fight and offers a fresh draw of upgrades. If every upgrade is used up,
	/// there is nothing to offer, so the level up passes without opening.</summary>
	public void Open(int level)
	{
		_options = Upgrades.Draw(Fight, Tuning.Levels.OptionsPerOffer);
		if (_options.Count == 0)
			return;
		_title.Text = $"LEVEL {level}";
		_selected = 0;
		_lastOption = 0;
		Visible = true;
		GetTree().Paused = true;
		Refresh();
		_pulse.Start(Fight.Camera.UnprojectPosition(Fight.Vnp.GlobalPosition));
		Fight.Sounds.Play(Sound.LevelUp);
	}

	/// <summary>Moves the highlight. Left and right move along the cards; down and up move to and from the reroll.</summary>
	public void MoveSelection(int across, int down)
	{
		int before = _selected;
		bool onReroll = _selected == _options.Count;
		if (down > 0 && !onReroll)
		{
			_lastOption = _selected;
			_selected = _options.Count;
		}
		else if (down < 0 && onReroll)
		{
			_selected = _lastOption;
		}
		else if (across != 0 && !onReroll)
		{
			_selected = Mathf.Clamp(_selected + across, 0, _options.Count - 1);
		}
		if (_selected != before)
			Fight.Sounds.Play(Sound.MenuMove);
		Refresh();
	}

	/// <summary>Takes the highlighted upgrade, or rerolls if the reroll is highlighted.</summary>
	public void Confirm()
	{
		if (_selected == _options.Count)
			Reroll();
		else
			Choose(_selected);
	}

	public void Choose(int index)
	{
		if (index < 0 || index >= _options.Count)
			return;
		Fight.ApplyUpgrade(_options[index]);
		Fight.Sounds.Play(Sound.UpgradeChoose);
		Visible = false;
		GetTree().Paused = false;
	}

	/// <summary>Swaps the offer for a new draw, avoiding the upgrades just shown where possible.</summary>
	public void Reroll()
	{
		bool free = false;
#if TOOLS
		free = PlaytestCheats.FreeRerolls; // Editor only: Alt held rerolls for free.
#endif
		if (!free)
		{
			if (Fight.Rerolls <= 0)
				return;
			Fight.Rerolls--;
		}
		Fight.Sounds.Play(Sound.MenuSelect);
		_options = Upgrades.Draw(Fight, Tuning.Levels.OptionsPerOffer, _options);
		Refresh();
	}

	public override void _Process(double delta)
	{
		if (!Visible)
			return;

		if (Input.IsActionJustPressed("move_left")) MoveSelection(-1, 0);
		if (Input.IsActionJustPressed("move_right")) MoveSelection(1, 0);
		if (Input.IsActionJustPressed("move_down")) MoveSelection(0, 1);
		if (Input.IsActionJustPressed("move_up")) MoveSelection(0, -1);
		if (Input.IsActionJustPressed("menu_select")) Confirm();
#if TOOLS
		// Editor only: show the free reroll as soon as Alt is pressed or let go.
		if (PlaytestCheats.FreeRerolls != _cheatShown)
			Refresh();
#endif
	}

	private void Refresh()
	{
		for (int i = 0; i < _cards.Count; i++)
		{
			bool shown = i < _options.Count;
			_cards[i].Visible = shown;
			if (!shown)
				continue;

			UpgradeDefinition upgrade = _options[i];
			_cardTags[i].Text = CardTag(upgrade);
			_cardTags[i].AddThemeColorOverride("font_color", CategoryColor(upgrade.Category));
			_cardNames[i].Text = upgrade.Name;
			_cardTexts[i].Text = Upgrades.Describe(upgrade, Fight);
			_cards[i].AddThemeStyleboxOverride("panel", CardStyle(upgrade.Category, i == _selected));
		}

		bool canReroll = Fight.Rerolls > 0;
		_rerollLabel.Text = canReroll ? $"Reroll   ({Fight.Rerolls} free)" : "No rerolls left";
#if TOOLS
		_cheatShown = PlaytestCheats.FreeRerolls;
		if (_cheatShown)
		{
			canReroll = true;
			_rerollLabel.Text = "Reroll   (Alt: free, playtest cheat)";
		}
#endif
		_rerollLabel.Modulate = new Color(1f, 1f, 1f, canReroll ? 1f : 0.4f);
		StyleBoxFlat rerollStyle = !canReroll ? _disabledStyle : _selected == _options.Count ? _selectedStyle : _normalStyle;
		_rerollCard.AddThemeStyleboxOverride("panel", rerollStyle);
	}

	/// <summary>The line at the top of a card: its category, marked NEW for a weapon the player does not
	/// have yet, and which time it would be for an upgrade with a limit, such as 2/4.</summary>
	private string CardTag(UpgradeDefinition upgrade)
	{
		string category = upgrade.Category switch
		{
			UpgradeCategory.VnpWeapon => "VNP WEAPON",
			UpgradeCategory.DroneWeapon => "DRONE WEAPON",
			UpgradeCategory.Drones => "DRONES",
			_ => "STATS",
		};
		if (Upgrades.IsNewWeapon(upgrade))
			return "NEW " + category;
		return upgrade.Limit > 0 ? $"{category}   {Upgrades.TimesTaken(Fight, upgrade) + 1}/{upgrade.Limit}" : category;
	}

	private static Color CategoryColor(UpgradeCategory category) => Ui.CategoryColor(category);

	/// <summary>A card tinted and bordered in its category's colour. The highlighted card is brighter, with a thicker border.</summary>
	private StyleBoxFlat CardStyle(UpgradeCategory category, bool highlighted)
	{
		if (_cardStyles.TryGetValue((category, highlighted), out StyleBoxFlat style))
			return style;

		Color color = CategoryColor(category);
		var background = new Color(0.07f, 0.09f, 0.15f, 0.96f);
		style = highlighted
			? MakeStyle(background.Lerp(color, 0.22f), color)
			: MakeStyle(background.Lerp(color, 0.08f), color.Darkened(0.45f));
		if (highlighted)
			style.SetBorderWidthAll(6);
		_cardStyles[(category, highlighted)] = style;
		return style;
	}

	private void BuildLayout()
	{
		_normalStyle = MakeStyle(new Color(0.07f, 0.09f, 0.15f, 0.96f), new Color(0.3f, 0.34f, 0.45f));
		_selectedStyle = MakeStyle(new Color(0.13f, 0.11f, 0.22f, 0.98f), Tuning.Look.ExperienceColor);
		_disabledStyle = MakeStyle(new Color(0.05f, 0.06f, 0.09f, 0.9f), new Color(0.18f, 0.2f, 0.26f));

		var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f), MouseFilter = MouseFilterEnum.Ignore };
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		// Over the dimmed fight, under the cards.
		_pulse = new LevelUpPulse();
		AddChild(_pulse);

		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		column.AddThemeConstantOverride("separation", 26);
		center.AddChild(column);

		_title = MakeLabel("", 64, Tuning.Look.ExperienceColor);
		column.AddChild(_title);
		column.AddChild(MakeLabel("Choose a fight upgrade", 28, new Color(1f, 1f, 1f, 0.8f)));

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 28);
		column.AddChild(row);

		for (int i = 0; i < Tuning.Levels.OptionsPerOffer; i++)
		{
			int index = i;
			var card = new PanelContainer { CustomMinimumSize = CardSize };
			card.MouseEntered += () => { _selected = index; Refresh(); };
			card.GuiInput += input =>
			{
				if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
					Choose(index);
			};
			row.AddChild(card);

			var inside = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			inside.AddThemeConstantOverride("separation", 12);
			card.AddChild(inside);

			var tag = MakeLabel("", 20, new Color(1f, 1f, 1f, 0.55f));
			var name = MakeLabel("", 36, Colors.White);
			var text = MakeLabel("", 24, new Color(1f, 1f, 1f, 0.85f));
			name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			text.CustomMinimumSize = new Vector2(CardSize.X - 50f, 0f);
			inside.AddChild(tag);
			inside.AddChild(name);
			inside.AddChild(text);

			_cards.Add(card);
			_cardTags.Add(tag);
			_cardNames.Add(name);
			_cardTexts.Add(text);
		}

		var rerollRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		column.AddChild(rerollRow);
		_rerollCard = new PanelContainer { CustomMinimumSize = RerollSize };
		_rerollCard.MouseEntered += () => { _selected = _options.Count; Refresh(); };
		_rerollCard.GuiInput += input =>
		{
			if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				Reroll();
		};
		rerollRow.AddChild(_rerollCard);
		_rerollLabel = MakeLabel("", 30, Colors.White);
		_rerollLabel.VerticalAlignment = VerticalAlignment.Center;
		_rerollCard.AddChild(_rerollLabel);

		column.AddChild(MakeLabel("WASD or stick to choose  ·  down for reroll  ·  Space or A to pick", 22,
			new Color(1f, 1f, 1f, 0.5f)));
	}

	private static Label MakeLabel(string text, int size, Color color) => Ui.Label(text, size, color);

	private static StyleBoxFlat MakeStyle(Color background, Color border) => Ui.Panel(background, border);
}
