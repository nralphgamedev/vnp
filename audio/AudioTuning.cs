using Godot;

/// <summary>Volumes, how repeated sounds are handled, and the list of every sound. Values live in
/// res://audio/audio.tres. Volumes run from 0 (silent) to 1 (full).</summary>
[GlobalClass]
public partial class AudioTuning : Resource
{
	/// <summary>Everything. This is the starting value: once the player changes Master in the pause menu's
	/// settings, their choice is saved and used instead.</summary>
	[ExportGroup("Volume")]
	[Export(PropertyHint.Range, "0,1,0.01")] public float MasterVolume;

	/// <summary>Every sound effect. Separate from master so music can sit beside it later. Like master,
	/// a starting value that the player's own setting replaces once changed.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float EffectsVolume;

	[ExportSubgroup("Categories")]
	[Export(PropertyHint.Range, "0,1,0.01")] public float WeaponsVolume;
	[Export(PropertyHint.Range, "0,1,0.01")] public float HitsVolume;
	[Export(PropertyHint.Range, "0,1,0.01")] public float DeathsVolume;
	[Export(PropertyHint.Range, "0,1,0.01")] public float PlayerVolume;
	[Export(PropertyHint.Range, "0,1,0.01")] public float HarvestingVolume;
	[Export(PropertyHint.Range, "0,1,0.01")] public float DronesVolume;
	[Export(PropertyHint.Range, "0,1,0.01")] public float AlertsVolume;
	[Export(PropertyHint.Range, "0,1,0.01")] public float MenusVolume;
	[Export(PropertyHint.Range, "0,1,0.01")] public float ResultsVolume;

	/// <summary>Most copies of one sound playing at once, for sounds that don't set their own.</summary>
	[ExportGroup("Repeated sounds")]
	[Export] public int DefaultMaxCopies;

	/// <summary>How far a varied pitch strays, as a fraction: 0.06 plays between 6% lower and 6% higher.</summary>
	[Export] public float PitchVariation;

	/// <summary>The same sound is not started again within this many seconds, so a burst of hits in one
	/// moment plays once instead of stacking into one loud sound.</summary>
	[Export] public float MinRepeatSeconds;

	/// <summary>Seconds a looping sound, such as harvesting, takes to fade in or out.</summary>
	[Export] public float LoopFadeSeconds;

	/// <summary>Every sound the fight plays.</summary>
	[ExportGroup("Sounds")]
	[Export] public Godot.Collections.Array<SoundDefinition> Sounds = new();
}
