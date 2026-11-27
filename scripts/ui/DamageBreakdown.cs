using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// How much damage each source did this fight, in a panel on the left of the result screen, mirroring the
/// upgrade list on the right. One line per source, most damage first, with its total and its share of all
/// damage dealt. Clicking a source shows its split by enemy type under it, so the player can see which weapons
/// worked against which enemy, and clicking again hides it; every source starts collapsed, at the designer's
/// request, so the list stays short. Then drones lost, split into destroyed and absorbed, and enemy shots
/// stopped by Orbital Shards blocking. Only damage that took health off an enemy counts, from the fight
/// statistics.
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
	private ScrollContainer _scroll;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		_lines = Ui.SidePanel(this, right: false, Width, Margin, WeaponSlots.Clearance, out _scroll);
	}

	/// <summary>Fills the panel from this fight's statistics.</summary>
	public void ShowFight(Fight fight)
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

		FightStats stats = fight.Stats;
		var enemyNames = fight.Enemies.Types.ToDictionary(t => t.Definition.Id, t => t.Definition.Name);
		var enemyColors = fight.Enemies.Types.ToDictionary(t => t.Definition.Id, t => t.Definition.Color);

		AddRow(_lines, "DAMAGE", Number(stats.TotalDamage), "", 24, new Color(1f, 1f, 1f, 0.8f), indent: false, out _);
		if (stats.TotalDamage <= 0f)
			AddText("None dealt", 22, new Color(1f, 1f, 1f, 0.6f));

		Font font = ThemeDB.FallbackFont;
		bool triangles = font.HasChar('\u25B8') && font.HasChar('\u25BE');
		string closed = triangles ? "\u25B8 " : "+ ", open = triangles ? "\u25BE " : "- ";
		foreach (FightStats.SourceDamage source in stats.Damage.OrderByDescending(d => d.Total))
		{
			AddSpacer(6f);
			bool hasSplit = source.ByEnemy.Count > 0;
			HBoxContainer header = AddRow(_lines, (hasSplit ? closed : "") + source.Source.Name, Number(source.Total),
				Percent(source.Total, stats.TotalDamage), 22, source.Source.Color.Lerp(Colors.White, 0.25f), indent: false,
				out Label headerName);
			if (!hasSplit)
				continue;
			// The split by enemy type, in the order the enemy types are listed, hidden until the source is clicked.
			var split = new VBoxContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
			split.AddThemeConstantOverride("separation", _lines.GetThemeConstant("separation"));
			foreach ((string enemyId, float amount) in source.ByEnemy.OrderBy(e => EnemyOrder(fight, e.Key)))
			{
				string name = enemyNames.GetValueOrDefault(enemyId, enemyId);
				Color color = enemyColors.TryGetValue(enemyId, out Color c) ? c.Lerp(Colors.White, 0.35f) : Colors.White;
				AddRow(split, $"vs {name}", Number(amount), Percent(amount, source.Total), 20,
					color * new Color(1f, 1f, 1f, 0.85f), indent: true, out _);
			}
			_lines.AddChild(split);
			string sourceName = source.Source.Name;
			// Pass, not Stop, so the mouse wheel still scrolls the panel over a source line; only a left click is used.
			header.MouseFilter = MouseFilterEnum.Pass;
			header.MouseDefaultCursorShape = CursorShape.PointingHand;
			header.GuiInput += input =>
			{
				if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
					return;
				split.Visible = !split.Visible;
				headerName.Text = (split.Visible ? open : closed) + sourceName;
				header.AcceptEvent();
			};
		}

		AddSpacer(14f);
		int destroyed = stats.DronesDestroyedTotal;
		int absorbed = stats.DronesAbsorbedTotal;
		AddRow(_lines, "DRONES LOST", (destroyed + absorbed).ToString(), "", 22, new Color(1f, 1f, 1f, 0.8f), indent: false, out _);
		AddRow(_lines, "Destroyed", destroyed.ToString(), "", 20, new Color(1f, 1f, 1f, 0.75f), indent: true, out _);
		AddRow(_lines, "Absorbed", absorbed.ToString(), "", 20, new Color(1f, 1f, 1f, 0.75f), indent: true, out _);

		if (stats.ShotsBlocked > 0)
		{
			AddSpacer(10f);
			AddRow(_lines, "SHOTS BLOCKED", stats.ShotsBlocked.ToString(), "", 22, new Color(1f, 1f, 1f, 0.8f), indent: false, out _);
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

	/// <summary>A name on the left, then a number and a share, each in its own right-aligned column, added to
	/// <paramref name="parent"/>. Returns the line, with its name label.</summary>
	private HBoxContainer AddRow(Container parent, string name, string number, string share, int size, Color color,
		bool indent, out Label nameLabel)
	{
		var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 8);
		if (indent)
			line.AddChild(new Control { CustomMinimumSize = new Vector2(Indent, 0f), MouseFilter = MouseFilterEnum.Ignore });

		nameLabel = Ui.Label(name, size, color);
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

		parent.AddChild(line);
		return line;
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
