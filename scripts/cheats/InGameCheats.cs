#if TOOLS
using Godot;

/// <summary>
/// Cheats for playtesting from the editor. They can never reach an exported game, debug or release, for
/// two reasons:
/// - VNP.csproj compiles the scripts/cheats/ folder only in Godot's Debug configuration, the one the
///   editor runs. Exports build as ExportDebug or ExportRelease, which leave this folder out.
/// - Every file here, and every line that uses one, is inside #if TOOLS, a symbol Godot defines only in
///   Debug. An export that somehow kept a use of it would fail to compile rather than ship a cheat.
/// Their settings are in res://scripts/cheats/in_game_cheats.tres.
/// </summary>
public static class InGameCheats
{
	/// <summary>The cheats' settings, read the first time a cheat needs them.</summary>
	public static InGameCheatSettings Settings => _settings ??= LoadSettings();

	private static InGameCheatSettings _settings;

	/// <summary>While Alt is held, rerolling a level-up offer is free, so it never runs out.</summary>
	public static bool FreeRerolls => Input.IsKeyPressed(Key.Alt);

	/// <summary>Whether the starting weapon menu picks the starting VNP Weapon, so the fight should not
	/// equip its usual StartingWeapons.</summary>
	public static bool ChoosesStartingWeapon => Settings.ChooseStartingWeapon && Settings.StartingWeaponChoices.Count > 0;

	/// <summary>
	/// If ChooseStartingWeapon is on, adds the starting weapon menu to the HUD, drawn just under
	/// <paramref name="drawnOver"/>, and opens it. Returns the menu, or null if it is off.
	/// </summary>
	public static StartingWeaponMenu OpenStartingWeaponMenu(Fight fight, Node hud, Node drawnOver)
	{
		if (!Settings.ChooseStartingWeapon && !Settings.ChooseAsteroidAmount && !Settings.ChoosePlan)
			return null;
		var menu = new StartingWeaponMenu { Name = "StartingWeaponMenu", Fight = fight };
		hud.AddChild(menu);
		hud.MoveChild(menu, drawnOver.GetIndex());
		menu.Open(Settings.ChooseStartingWeapon ? Settings.StartingWeaponChoices : new(),
			Settings.ChooseStartingWeapon ? Settings.StartingDroneWeaponChoices : new(),
			Settings.ChooseAsteroidAmount ? Tuning.Asteroids.Amounts : new(),
			Settings.ChoosePlan ? fight.Director.Plan.Faction?.Plans?.Keys ?? new Godot.Collections.Array<FightPlanDefinition>()
				: new Godot.Collections.Array<FightPlanDefinition>());
		return menu;
	}

	private static InGameCheatSettings LoadSettings()
	{
		const string path = "res://scripts/cheats/in_game_cheats.tres";
		var settings = GD.Load<InGameCheatSettings>(path);
		if (settings == null)
		{
			GD.PushError($"Could not load the cheat settings {path}.");
			settings = new InGameCheatSettings();
		}
		return settings;
	}
}
#endif
