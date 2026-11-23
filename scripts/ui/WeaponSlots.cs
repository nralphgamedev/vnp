using Godot;
using System.Collections.Generic;

/// <summary>
/// Bottom left of the screen: one box for each of the VNP's weapon slots, showing the icon of the weapon
/// in it or nothing if it is free, and a box beside them for the Drone Weapon the drones are using.
/// It shows only while a level-up offer, the pause menu or the result screen is up, drawn above them, so
/// the player can see their weapons while choosing and at the end, and the screen stays clean during play.
/// </summary>
public partial class WeaponSlots : Control
{
	// Sizes in pixels at 1920 by 1080. The boxes sit just above the level text and experience bar.
	private const float Left = 40f;
	private const float BoxSize = 64f;
	private const float Gap = 10f;
	private const float DroneGap = 34f;
	private const float AboveBottom = 100f;
	private const int LabelSize = 16;

	/// <summary>How far up from the bottom of the screen the boxes and their labels reach, so a panel beside them,
	/// such as the damage breakdown, can stop above them.</summary>
	public const float Clearance = AboveBottom + BoxSize + LabelSize + 16f;

	private static readonly Color BoxColor = new(0.05f, 0.07f, 0.12f, 0.85f);
	private static readonly Color EmptyBorder = new(1f, 1f, 1f, 0.18f);
	private static readonly Color LabelColor = new(1f, 1f, 1f, 0.5f);

	public Fight Fight;

	private readonly Dictionary<string, Texture2D> _icons = new();
	private LevelUpMenu _levelUpMenu;
	private PauseMenu _pauseMenu;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Hud;
		// Keep updating while an offer or the pause menu has the fight paused.
		ProcessMode = ProcessModeEnum.Always;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_levelUpMenu = GetParent().GetNode<LevelUpMenu>("LevelUpMenu");
		_pauseMenu = GetParent().GetNode<PauseMenu>("PauseMenu");
	}

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		if (Fight == null || !(_levelUpMenu.IsOpen || _pauseMenu.IsOpen || Fight.IsOver))
			return;

		Font font = ThemeDB.FallbackFont;
		float top = Size.Y - AboveBottom - BoxSize;
		float x = Left;

		DrawString(font, new Vector2(x, top - 8f), "WEAPONS", HorizontalAlignment.Left, -1, LabelSize, LabelColor);
		int slots = Tuning.Vnp.WeaponSlots;
		for (int s = 0; s < slots; s++)
		{
			WeaponDefinition weapon = s < Fight.WeaponCount ? Fight.Weapons[s].Definition : null;
			DrawBox(new Rect2(x, top, BoxSize, BoxSize), weapon?.Id, weapon?.Color ?? default);
			x += BoxSize + Gap;
		}

		x += DroneGap - Gap;
		DroneWeaponDefinition droneWeapon = Fight.Drones.Weapon;
		DrawString(font, new Vector2(x, top - 8f), "DRONES", HorizontalAlignment.Left, -1, LabelSize, LabelColor);
		DrawBox(new Rect2(x, top, BoxSize, BoxSize), droneWeapon.Id, droneWeapon.Color);
	}

	/// <summary>A box with a weapon's icon in its colour, or an empty box when <paramref name="id"/> is null.</summary>
	private void DrawBox(Rect2 box, string id, Color color)
	{
		DrawRect(box, BoxColor);
		if (id == null)
		{
			DrawRect(box, EmptyBorder, filled: false, width: 2f);
			return;
		}

		DrawRect(box, new Color(color, 0.9f), filled: false, width: 3f);
		Texture2D icon = IconFor(id);
		if (icon != null)
			DrawTextureRect(icon, box.Grow(-10f), tile: false, modulate: color);
		else
			DrawString(ThemeDB.FallbackFont, box.Position + new Vector2(0f, box.Size.Y * 0.62f), id.Substring(0, 1).ToUpper(),
				HorizontalAlignment.Center, box.Size.X, 30, color);
	}

	/// <summary>The icon named after a weapon's ID, loaded once. A weapon without an icon shows its first letter.</summary>
	private Texture2D IconFor(string id)
	{
		if (!_icons.TryGetValue(id, out Texture2D icon))
		{
			string path = $"res://placeholder/textures/icons/{id}.svg";
			icon = ResourceLoader.Exists(path) ? Placeholders.Icon(id) : null;
			_icons[id] = icon;
		}
		return icon;
	}
}
