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

	public static string Clock(double seconds)
	{
		int whole = (int)seconds;
		return $"{whole / 60}:{whole % 60:00}";
	}
}
