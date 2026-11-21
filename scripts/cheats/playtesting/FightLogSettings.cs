#if TOOLS
using Godot;

/// <summary>
/// Settings for fight logs: the files the editor writes after each fight, for playtesting analysis. Values live in
/// res://scripts/cheats/playtesting/fight_log.tres. Editor only, like the cheats.
/// </summary>
[GlobalClass]
public partial class FightLogSettings : Resource
{
	/// <summary>Write a fight log after each fight played in the editor. Off writes nothing.</summary>
	[Export] public bool WriteLogs = true;

	/// <summary>Where the designer's own fights are logged. It is kept out of git and out of Godot's import.</summary>
	[Export] public string Folder = "res://playtests/human";

	/// <summary>Seconds between the timeline's samples of health, level, experience, drones, enemies and kills.</summary>
	[Export] public float TimelineSeconds = 1f;

	/// <summary>Samples a second of the player's movement input, recorded in the designer's own fights.</summary>
	[Export] public float InputSamplesPerSecond = 10f;
}
#endif
