using Godot;
using System.Collections.Generic;

/// <summary>
/// The fight upgrades taken so far, in a panel on the right of the screen. One upgrade per line, with how
/// many times it was taken and the total it has added so far (a percentage, or a flat number), grouped by
/// what it affected: each VNP Weapon by name in slot order,
/// then the Drone Weapon, the VNP's stats, and the drones. Each heading takes its category's card colour.
/// Used by the pause menu and the result screen.
/// </summary>
public partial class UpgradeList : Control
{
	// Sizes in pixels at 1920 by 1080.
	private const float Width = 450f;
	private const float Indent = 14f;
	private const float BonusWidth = 70f;
	private const float CountWidth = 42f;
	private const float Margin = 40f;

	private VBoxContainer _lines;
	private ScrollContainer _scroll;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		_lines = Ui.SidePanel(this, right: true, Width, Margin, 0f, out _scroll);
	}

	/// <summary>Fills the panel with the upgrades taken in this fight.</summary>
	public void ShowUpgrades(Fight fight)
	{
		// Disposed straight away: a Godot array left for .NET to clean up later can be cleaned up after Godot has
		// shut down, which crashes the game as it quits.
		_scroll.ScrollVertical = 0;
		var children = _lines.GetChildren();
		using ((Godot.Collections.Array)children)
		{
			foreach (Node child in children)
				child.QueueFree();
		}

		AddLine("FIGHT UPGRADES", 24, new Color(1f, 1f, 1f, 0.8f));
		IReadOnlyList<UpgradeDefinition> taken = fight.TakenUpgrades;
		if (taken.Count == 0)
		{
			AddLine("None yet", 22, new Color(1f, 1f, 1f, 0.6f));
			return;
		}

		foreach (Group group in GroupsOf(fight, taken))
		{
			AddSpacer(8f);
			AddLine(group.Title, 22, group.Color);
			foreach ((UpgradeDefinition upgrade, int count) in group.Upgrades)
			{
				// Under a weapon's own heading, the upgrade that equipped it would just repeat its name.
				string name = upgrade.Effect == UpgradeEffect.EquipWeapon ? "Equipped" : upgrade.Name;
				AddUpgrade(name, count, Upgrades.TotalBonus(upgrade, count, fight));
			}
		}
	}

	private sealed class Group
	{
		public string Title;
		public Color Color;
		public int Order;
		public readonly List<(UpgradeDefinition Upgrade, int Count)> Upgrades = new();
	}

	/// <summary>
	/// The upgrades taken, grouped by what they affected. Upgrades to a VNP Weapon, and the upgrade that
	/// equipped it, go under that weapon's name. The rest go under their category. Within a group, upgrades
	/// keep the order they were first taken in.
	/// </summary>
	private static List<Group> GroupsOf(Fight fight, IReadOnlyList<UpgradeDefinition> taken)
	{
		var groups = new Dictionary<string, Group>();
		var order = new List<Group>();
		foreach (UpgradeDefinition upgrade in taken)
		{
			bool weapon = upgrade.Category == UpgradeCategory.VnpWeapon && upgrade.Weapon != null;
			string key = weapon ? "weapon:" + upgrade.Weapon.Id : "category:" + upgrade.Category;
			if (!groups.TryGetValue(key, out Group group))
			{
				group = new Group
				{
					Title = weapon ? upgrade.Weapon.Name : CategoryTitle(upgrade.Category),
					Color = CategoryColor(upgrade.Category),
					Order = weapon ? SlotOf(fight, upgrade.Weapon) : 100 + (int)upgrade.Category,
				};
				groups[key] = group;
				order.Add(group);
			}

			int index = group.Upgrades.FindIndex(entry => entry.Upgrade.Id == upgrade.Id);
			if (index >= 0)
				group.Upgrades[index] = (group.Upgrades[index].Upgrade, group.Upgrades[index].Count + 1);
			else
				group.Upgrades.Add((upgrade, 1));
		}

		order.Sort((a, b) => a.Order.CompareTo(b.Order));
		return order;
	}

	/// <summary>Where a weapon sits in the VNP's slots, so weapon groups follow slot order.</summary>
	private static int SlotOf(Fight fight, WeaponDefinition weapon)
	{
		for (int i = 0; i < fight.Weapons.Count; i++)
		{
			if (fight.Weapons[i].Definition.Id == weapon.Id)
				return i;
		}
		return 50;
	}

	private static string CategoryTitle(UpgradeCategory category) => category switch
	{
		UpgradeCategory.VnpWeapon => "VNP Weapons",
		UpgradeCategory.DroneWeapon => "Drone Weapon",
		UpgradeCategory.Drones => "Drones",
		_ => "Stats",
	};

	private static Color CategoryColor(UpgradeCategory category) => Ui.CategoryColor(category);

	private void AddLine(string text, int size, Color color)
	{
		Label label = Ui.Label(text, size, color);
		label.HorizontalAlignment = HorizontalAlignment.Left;
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(Width - 50f, 0f);
		_lines.AddChild(label);
	}

	/// <summary>
	/// An indented upgrade name, then how many times it was taken (×2) and its total bonus, each in its own
	/// right-aligned column so they line up and a long name wraps within its own column. Upgrades with no
	/// bonus, which equip or switch a weapon and can only be taken once, show neither.
	/// </summary>
	private void AddUpgrade(string name, int count, string bonus)
	{
		var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 8);
		line.AddChild(new Control { CustomMinimumSize = new Vector2(Indent, 0f), MouseFilter = MouseFilterEnum.Ignore });

		Label nameLabel = Ui.Label(name, 22, new Color(1f, 1f, 1f, 0.85f));
		nameLabel.HorizontalAlignment = HorizontalAlignment.Left;
		nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameLabel.CustomMinimumSize = new Vector2(Width - 50f - Indent - CountWidth - BonusWidth - 24f, 0f);
		line.AddChild(nameLabel);

		Label countLabel = Ui.Label(bonus != null ? $"×{count}" : "", 22, new Color(1f, 1f, 1f, 0.55f));
		countLabel.HorizontalAlignment = HorizontalAlignment.Right;
		countLabel.CustomMinimumSize = new Vector2(CountWidth, 0f);
		line.AddChild(countLabel);

		Label bonusLabel = Ui.Label(bonus ?? "", 22, Colors.White);
		bonusLabel.HorizontalAlignment = HorizontalAlignment.Right;
		bonusLabel.CustomMinimumSize = new Vector2(BonusWidth, 0f);
		line.AddChild(bonusLabel);

		_lines.AddChild(line);
	}

	private void AddSpacer(float height) =>
		_lines.AddChild(new Control { CustomMinimumSize = new Vector2(0f, height), MouseFilter = MouseFilterEnum.Ignore });
}
