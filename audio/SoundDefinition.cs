using Godot;

/// <summary>Which volume setting in audio.tres a sound follows, on top of master and effects.</summary>
public enum SoundCategory
{
	/// <summary>Weapons firing, and shards and drone beams hitting.</summary>
	Weapons,

	/// <summary>Enemies and drones being hit.</summary>
	Hits,

	/// <summary>Enemies and drones destroyed.</summary>
	Deaths,

	/// <summary>The VNP taking damage, and the low-health warning.</summary>
	Player,

	/// <summary>The harvest beam loop and used-up material popping.</summary>
	Harvesting,

	/// <summary>Temporary drones being built.</summary>
	Drones,

	/// <summary>The wave warning and the time limit steps.</summary>
	Alerts,

	/// <summary>Level ups, choosing upgrades, and moving and selecting in menus.</summary>
	Menus,

	/// <summary>The win, loss and retreat results.</summary>
	Results,
}

/// <summary>
/// One sound the fight can play, listed in res://audio/audio.tres. The game plays it by its ID; the
/// file it plays is a placeholder in res://placeholder/audio/ and can be swapped for another.
/// </summary>
[GlobalClass]
public partial class SoundDefinition : Resource
{
	/// <summary>The name the game plays it by. Do not change it unless the code changes too.</summary>
	[Export] public string Id;

	/// <summary>The sound file.</summary>
	[Export] public AudioStream Stream;

	/// <summary>Which category volume in audio.tres it follows.</summary>
	[Export] public SoundCategory Category;

	/// <summary>Its own volume, from 0 to 1, before the master, effects and category volumes.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float Volume;

	/// <summary>Most copies of this sound playing at once. Further plays are skipped until one ends.
	/// 0 uses the default in audio.tres.</summary>
	[Export] public int MaxCopies;

	/// <summary>Plays each copy at a slightly different pitch, so repeats don't grate. Leave off for
	/// tunes and alarms.</summary>
	[Export] public bool VaryPitch;

	/// <summary>Plays over and over while the game keeps asking for it, fading in and out.</summary>
	[Export] public bool Loop;
}
