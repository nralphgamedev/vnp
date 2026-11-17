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
	public IReadOnlyList<Upgrade> Options => _options;

	/// <summary>Which card is highlighted. The reroll is the one after the last option.</summary>
	public int Selected => _selected;

	private List<Upgrade> _options = new();
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

	public override void _Ready()
	{
		// Keep running while the fight is paused.
		ProcessMode = ProcessModeEnum.Always;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;
		BuildLayout();
	}

	/// <summary>Pauses the fight and offers a fresh draw of upgrades.</summary>
	public void Open(int level)
	{
		_title.Text = $"LEVEL {level}";
		_options = Upgrades.Draw(Fight, Tuning.UpgradeOptionsPerOffer);
		_selected = 0;
		_lastOption = 0;
		Visible = true;
		GetTree().Paused = true;
		Refresh();
	}

	/// <summary>Moves the highlight. Left and right move along the cards; down and up move to and from the reroll.</summary>
	public void MoveSelection(int across, int down)
	{
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
		Visible = false;
		GetTree().Paused = false;
	}

	/// <summary>Swaps the offer for a new draw, avoiding the upgrades just shown where possible.</summary>
	public void Reroll()
	{
		if (Fight.Rerolls <= 0)
			return;
		Fight.Rerolls--;
		_options = Upgrades.Draw(Fight, Tuning.UpgradeOptionsPerOffer, _options);
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
	}

	private void Refresh()
	{
		for (int i = 0; i < _cards.Count; i++)
		{
			bool shown = i < _options.Count;
			_cards[i].Visible = shown;
			if (!shown)
				continue;

			Upgrade upgrade = _options[i];
			_cardTags[i].Text = upgrade.IsWeapon ? "NEW WEAPON" : "UPGRADE";
			_cardNames[i].Text = upgrade.Name;
			_cardTexts[i].Text = upgrade.Describe();
			_cards[i].AddThemeStyleboxOverride("panel", i == _selected ? _selectedStyle : _normalStyle);
		}

		bool canReroll = Fight.Rerolls > 0;
		_rerollLabel.Text = canReroll ? $"Reroll   ({Fight.Rerolls} free)" : "No rerolls left";
		_rerollLabel.Modulate = new Color(1f, 1f, 1f, canReroll ? 1f : 0.4f);
		StyleBoxFlat rerollStyle = !canReroll ? _disabledStyle : _selected == _options.Count ? _selectedStyle : _normalStyle;
		_rerollCard.AddThemeStyleboxOverride("panel", rerollStyle);
	}

	private void BuildLayout()
	{
		_normalStyle = MakeStyle(new Color(0.07f, 0.09f, 0.15f, 0.96f), new Color(0.3f, 0.34f, 0.45f));
		_selectedStyle = MakeStyle(new Color(0.13f, 0.11f, 0.22f, 0.98f), Tuning.ExperienceColor);
		_disabledStyle = MakeStyle(new Color(0.05f, 0.06f, 0.09f, 0.9f), new Color(0.18f, 0.2f, 0.26f));

		var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f), MouseFilter = MouseFilterEnum.Ignore };
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		column.AddThemeConstantOverride("separation", 26);
		center.AddChild(column);

		_title = MakeLabel("", 64, Tuning.ExperienceColor);
		column.AddChild(_title);
		column.AddChild(MakeLabel("Choose a fight upgrade", 28, new Color(1f, 1f, 1f, 0.8f)));

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 28);
		column.AddChild(row);

		for (int i = 0; i < Tuning.UpgradeOptionsPerOffer; i++)
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

	private static Label MakeLabel(string text, int size, Color color)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static StyleBoxFlat MakeStyle(Color background, Color border)
	{
		var style = new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomLeft = 12,
			CornerRadiusBottomRight = 12,
			ContentMarginLeft = 24,
			ContentMarginRight = 24,
			ContentMarginTop = 22,
			ContentMarginBottom = 22,
		};
		style.SetBorderWidthAll(3);
		return style;
	}
}
