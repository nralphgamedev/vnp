using Godot;

/// <summary>
/// The player's own settings, changed in the pause menu and kept between runs in a small settings file in
/// Godot's user folder (user://settings.cfg). Nothing about a fight is kept, only preferences.
///
/// Until the player changes a setting, it uses the starting value from the tuning files: the sound volumes
/// start from MasterVolume and EffectsVolume in audio.tres.
/// </summary>
public static class Settings
{
	private const string FilePath = "user://settings.cfg";
	private const string SoundSection = "sound";

	private static bool _loaded;
	private static float _masterVolume;
	private static float _effectsVolume;

	/// <summary>Everything, from 0 to 1.</summary>
	public static float MasterVolume
	{
		get { Load(); return _masterVolume; }
		set { Load(); _masterVolume = Mathf.Clamp(value, 0f, 1f); Save(); }
	}

	/// <summary>Every sound effect, from 0 to 1.</summary>
	public static float EffectsVolume
	{
		get { Load(); return _effectsVolume; }
		set { Load(); _effectsVolume = Mathf.Clamp(value, 0f, 1f); Save(); }
	}

	private static void Load()
	{
		if (_loaded)
			return;
		_loaded = true;

		_masterVolume = Tuning.Audio.MasterVolume;
		_effectsVolume = Tuning.Audio.EffectsVolume;

		var file = new ConfigFile();
		if (file.Load(FilePath) != Error.Ok)
			return; // No settings saved yet.
		_masterVolume = Mathf.Clamp((float)file.GetValue(SoundSection, "master_volume", _masterVolume), 0f, 1f);
		_effectsVolume = Mathf.Clamp((float)file.GetValue(SoundSection, "effects_volume", _effectsVolume), 0f, 1f);
	}

	private static void Save()
	{
		var file = new ConfigFile();
		file.SetValue(SoundSection, "master_volume", _masterVolume);
		file.SetValue(SoundSection, "effects_volume", _effectsVolume);
		Error error = file.Save(FilePath);
		if (error != Error.Ok)
			GD.PushError($"Could not save settings to {FilePath}: {error}");
	}
}
