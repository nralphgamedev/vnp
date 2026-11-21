using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// How much damage each source did this fight, in a panel on the left of the result screen, mirroring the
/// upgrade list on the right. One line per source, most damage first, with its total and its share of all
/// damage dealt, and under it the split by enemy type, so the player can see which weapons worked against
/// which enemy. Then drones lost, split into destroyed and absorbed, and enemy shots stopped by Orbital
/// Shards blocking. Only damage that took health off an enemy counts, from the fight statistics.
/// </summary>
public partial class DamageBreakdown : Control
{
	// Sizes in pixels at 1920 by 1080.
	private const float Width = 450f;
	private const float Indent = 14f;
	private const float NumberWidth = 78f;
	private const float ShareWidth = 58f;
	private const float Margin = 40f;

	private VBoxContainer _lines;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(Width, 0f) };
		panel.AddThemeStyleboxOverride("panel", Ui.Panel(new Color(0.07f, 0.09f, 0.15f, 0.85f), new Color(0.3f, 0.34f, 0.45f, 0.35f)));
		AddChild(panel);
		// Pinned to the left edge, centred top to bottom, growing both ways as lines are added.
		panel.AnchorLeft = 0f;
		panel.AnchorRight = 0f;
		panel.AnchorTop = 0.5f;
		panel.AnchorBottom = 0.5f;
		panel.OffsetLeft = Margin;
		panel.OffsetRight = Margin + Width;
		panel.GrowVertical = GrowDirection.Both;

		_lines = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		_lines.AddThemeConstantOverride("separation", 4);
		panel.AddChild(_lines);
	}

	/// <summary>Fills the panel from this fight's statistics.</summary>
	public void ShowFight(Fight fight)
	{
		// Disposed straight away: a Godot array left for .NET to clean up later can be cleaned up after Godot has
		// shut down, which crashes the game as it quits.
		var children = _lines.GetChildren();
		using ((Godot.Collections.Array)children)
		{
			foreach (Node child in children)
				child.QueueFree();
		}

		FightStats stats = fight.Stats;
		var enemyNames = fight.Enemies.Types.ToDictionary(t => t.Definition.Id, t => t.Definition.Name);
		var enemyColors = fight.Enemies.Types.ToDictionary(t => t.Definition.Id, t => t.Definition.Color);

		AddRow("DAMAGE", Number(stats.TotalDamage), "", 24, new Color(1f, 1f, 1f, 0.8f), indent: false);
		if (stats.TotalDamage <= 0f)
			AddText("None dealt", 22, new Color(1f, 1f, 1f, 0.6f));

		foreach (FightStats.SourceDamage source in stats.Damage.OrderByDescending(d => d.Total))
		{
			AddSpacer(6f);
			AddRow(source.Source.Name, Number(source.Total), Percent(source.Total, stats.TotalDamage), 22,
				source.Source.Color.Lerp(Colors.White, 0.25f), indent: false);
			// The split by enemy type, in the order the enemy types are listed.
			foreach ((string enemyId, float amount) in source.ByEnemy.OrderBy(e => EnemyOrder(fight, e.Key)))
			{
				string name = enemyNames.GetValueOrDefault(enemyId, enemyId);
				Color color = enemyColors.TryGetValue(enemyId, out Color c) ? c.Lerp(Colors.White, 0.35f) : Colors.White;
				AddRow($"vs {name}", Number(amount), Percent(amount, source.Total), 20, color * new Color(1f, 1f, 1f, 0.85f), indent: true);
			}
		}

		AddSpacer(14f);
		int destroyed = stats.DronesDestroyedTotal;
		int absorbed = stats.DronesAbsorbedTotal;
		AddRow("DRONES LOST", (destroyed + absorbed).ToString(), "", 22, new Color(1f, 1f, 1f, 0.8f), indent: false);
		AddRow("Destroyed", destroyed.ToString(), "", 20, new Color(1f, 1f, 1f, 0.75f), indent: true);
		AddRow("Absorbed", absorbed.ToString(), "", 20, new Color(1f, 1f, 1f, 0.75f), indent: true);

		if (stats.ShotsBlocked > 0)
		{
			AddSpacer(10f);
			AddRow("SHOTS BLOCKED", stats.ShotsBlocked.ToString(), "", 22, new Color(1f, 1f, 1f, 0.8f), indent: false);
		}
	}

	private static int EnemyOrder(Fight fight, string enemyId)
	{
		for (int i = 0; i < fight.Enemies.Types.Count; i++)
		{
			if (fight.Enemies.Types[i].Definition.Id == enemyId)
				return i;
		}
		return int.MaxValue;
	}

	private static string Number(float value) => Mathf.RoundToInt(value).ToString("N0");

	private static string Percent(float part, float whole) => whole > 0f ? $"{part / whole * 100f:0}%" : "";

	/// <summary>A name on the left, then a number and a share, each in its own right-aligned column.</summary>
	private void AddRow(string name, string number, string share, int size, Color color, bool indent)
	{
		var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 8);
		if (indent)
			line.AddChild(new Control { CustomMinimumSize = new Vector2(Indent, 0f), MouseFilter = MouseFilterEnum.Ignore });

		Label nameLabel = Ui.Label(name, size, color);
		nameLabel.HorizontalAlignment = HorizontalAlignment.Left;
		nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameLabel.CustomMinimumSize = new Vector2(Width - 50f - (indent ? Indent : 0f) - NumberWidth - ShareWidth - 24f, 0f);
		line.AddChild(nameLabel);

		Label numberLabel = Ui.Label(number, size, Colors.White);
		numberLabel.HorizontalAlignment = HorizontalAlignment.Right;
		numberLabel.CustomMinimumSize = new Vector2(NumberWidth, 0f);
		line.AddChild(numberLabel);

		Label shareLabel = Ui.Label(share, size, new Color(1f, 1f, 1f, 0.6f));
		shareLabel.HorizontalAlignment = HorizontalAlignment.Right;
		shareLabel.CustomMinimumSize = new Vector2(ShareWidth, 0f);
		line.AddChild(shareLabel);

		_lines.AddChild(line);
	}

	private void AddText(string text, int size, Color color)
	{
		Label label = Ui.Label(text, size, color);
		label.HorizontalAlignment = HorizontalAlignment.Left;
		_lines.AddChild(label);
	}

	private void AddSpacer(float height) =>
		_lines.AddChild(new Control { CustomMinimumSize = new Vector2(0f, height), MouseFilter = MouseFilterEnum.Ignore });
}
