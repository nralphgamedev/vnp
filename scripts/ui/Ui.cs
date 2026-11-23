using Godot;

/// <summary>Small helpers for building the menus in code, so they all look alike.</summary>
public static class Ui
{
	/// <summary>A centred label with a font size and colour.</summary>
	public static Label Label(string text, int size, Color color)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	/// <summary>A rounded panel background with a border.</summary>
	/// <summary>
	/// A panel pinned to the left or right edge of <paramref name="owner"/>, centred top to bottom, holding lines
	/// added to the returned column. It grows with its lines until it would come within <paramref name="margin"/>
	/// of the top and bottom of the screen, or of <paramref name="bottomClear"/> above the bottom, then scrolls, by the mouse wheel or its scroll bar, so a long list,
	/// such as a long fight's damage breakdown, stays readable. Used by the upgrade list and the damage breakdown.
	/// </summary>
	public static VBoxContainer SidePanel(Control owner, bool right, float width, float margin, float bottomClear,
		out ScrollContainer scroll)
	{
		StyleBoxFlat style = Panel(new Color(0.07f, 0.09f, 0.15f, 0.85f), new Color(0.3f, 0.34f, 0.45f, 0.35f));
		var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(width, 0f) };
		panel.AddThemeStyleboxOverride("panel", style);
		owner.AddChild(panel);
		panel.AnchorLeft = right ? 1f : 0f;
		panel.AnchorRight = right ? 1f : 0f;
		panel.AnchorTop = 0.5f;
		panel.AnchorBottom = 0.5f;
		panel.OffsetLeft = right ? -margin - width : margin;
		panel.OffsetRight = right ? -margin : margin + width;
		// Centred in the space above what it must clear.
		panel.OffsetTop = -bottomClear * 0.5f;
		panel.OffsetBottom = -bottomClear * 0.5f;
		panel.GrowVertical = Control.GrowDirection.Both;

		var box = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			MouseFilter = Control.MouseFilterEnum.Pass,
		};
		panel.AddChild(box);
		var lines = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		lines.AddThemeConstantOverride("separation", 4);
		box.AddChild(lines);

		// A scroll box takes no height from what it holds, so it is given the lines' height, up to what fits.
		lines.MinimumSizeChanged += () =>
		{
			float fits = owner.GetViewportRect().Size.Y - 2f * margin - bottomClear - style.ContentMarginTop - style.ContentMarginBottom;
			box.CustomMinimumSize = new Vector2(0f, Mathf.Min(lines.GetCombinedMinimumSize().Y, Mathf.Max(100f, fits)));
		};
		scroll = box;
		return lines;
	}

	public static StyleBoxFlat Panel(Color background, Color border)
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

	/// <summary>A full-screen dark layer behind a menu, and a centred column to put the menu in.</summary>
	public static VBoxContainer DimmedColumn(Control parent, float dim, int separation)
	{
		var shade = new ColorRect { Color = new Color(0f, 0f, 0f, dim), MouseFilter = Control.MouseFilterEnum.Ignore };
		shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		parent.AddChild(shade);

		var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		parent.AddChild(center);

		var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		column.AddThemeConstantOverride("separation", separation);
		center.AddChild(column);
		return column;
	}

	/// <summary>The colour of an upgrade category, from look.tres. Used on offer cards and in the upgrade list.</summary>
	public static Color CategoryColor(UpgradeCategory category) => category switch
	{
		UpgradeCategory.VnpWeapon => Tuning.Look.VnpWeaponCardColor,
		UpgradeCategory.DroneWeapon => Tuning.Look.DroneWeaponCardColor,
		UpgradeCategory.Drones => Tuning.Look.DronesCardColor,
		_ => Tuning.Look.StatsCardColor,
	};

	public static string Clock(double seconds)
	{
		int whole = (int)seconds;
		return $"{whole / 60}:{whole % 60:00}";
	}
}
