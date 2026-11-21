using Godot;
using System;
using System.Collections.Generic;

/// <summary>The IDs the game plays sounds by. Each matches a sound listed in res://audio/audio.tres.</summary>
public static class Sound
{
	public const string PulseLaserFire = "pulse_laser_fire";
	public const string ShardHit = "shard_hit";
	public const string DroneProjectileFire = "drone_projectile_fire";
	public const string DroneBeamTick = "drone_beam_tick";
	public const string EnemyFire = "enemy_fire";
	public const string ScatterShotFire = "scatter_shot_fire";
	public const string SeekerLaunch = "seeker_launch";
	public const string SeekerHit = "seeker_hit";
	public const string ShockwaveFire = "shockwave_fire";
	public const string MineDrop = "mine_drop";
	public const string MineExplode = "mine_explode";
	public const string RamHit = "ram_hit";
	public const string ShardBlock = "shard_block";
	public const string GooAbsorb = "goo_absorb";
	public const string GooSplit = "goo_split";
	public const string GooDeath = "goo_death";
	public const string Hit = "hit";
	public const string EnemyDeath = "enemy_death";
	public const string DroneDeath = "drone_death";
	public const string VnpDamage = "vnp_damage";
	public const string LowHealth = "low_health";
	public const string HarvestLoop = "harvest_loop";
	public const string MaterialPop = "material_pop";
	public const string DroneBuilt = "drone_built";
	public const string WaveWarning = "wave_warning";
	public const string TimeLimit = "time_limit";
	public const string LevelUp = "level_up";
	public const string UpgradeChoose = "upgrade_choose";
	public const string MenuMove = "menu_move";
	public const string MenuSelect = "menu_select";
	public const string Win = "win";
	public const string Loss = "loss";
	public const string Retreat = "retreat";
}

/// <summary>
/// Plays the fight's sounds. Each sound has a few players of its own, so only that many copies of it can
/// play at once, and a sound asked for again within moments is skipped rather than stacked. Pitch varies
/// slightly between copies where the sound allows it. Looping sounds, like harvesting, play while the game
/// keeps asking for them each frame and fade out when it stops.
///
/// It keeps running while the fight is paused, so menu sounds play during level-up offers and the pause menu.
/// </summary>
public partial class Sounds : Node
{
	private sealed class Entry
	{
		public SoundDefinition Definition;
		public AudioStreamPlayer[] Players;
		public ulong LastStartedMsec;
		public bool Started;
		// For looping sounds: how loud it is now, from 0 to 1, and whether it was asked for this frame.
		public float LoopLevel;
		public bool LoopWanted;
	}

	private readonly Dictionary<string, Entry> _entries = new();
	private readonly List<Entry> _loops = new();
	private readonly Random _random = new();

	private static AudioTuning Audio => Tuning.Audio;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		ProcessPriority = UpdateOrder.Audio;

		foreach (SoundDefinition definition in Audio.Sounds)
		{
			if (definition?.Stream == null || string.IsNullOrEmpty(definition.Id))
				continue;

			AudioStream stream = definition.Loop ? Looping(definition.Stream) : definition.Stream;
			int copies = definition.Loop ? 1 : definition.MaxCopies > 0 ? definition.MaxCopies : Mathf.Max(1, Audio.DefaultMaxCopies);
			var entry = new Entry { Definition = definition, Players = new AudioStreamPlayer[copies] };
			for (int i = 0; i < copies; i++)
			{
				entry.Players[i] = new AudioStreamPlayer { Stream = stream, Name = $"{definition.Id}_{i}" };
				AddChild(entry.Players[i]);
			}
			_entries[definition.Id] = entry;
			if (definition.Loop)
				_loops.Add(entry);
		}
	}

	/// <summary>
	/// Plays a sound once. Skipped if every copy of it is already playing, or if it started a moment ago.
	/// <paramref name="volume"/> scales it further, for quieter variants of the same sound.
	/// </summary>
	public void Play(string id, float volume = 1f)
	{
		if (!_entries.TryGetValue(id, out Entry entry) || entry.Definition.Loop)
			return;

		ulong now = Time.GetTicksMsec();
		if (entry.Started && now - entry.LastStartedMsec < (ulong)(Audio.MinRepeatSeconds * 1000f))
			return;

		foreach (AudioStreamPlayer player in entry.Players)
		{
			if (player.Playing)
				continue;
			player.VolumeDb = ToDb(Loudness(entry.Definition) * volume);
			player.PitchScale = entry.Definition.VaryPitch
				? 1f + Audio.PitchVariation * ((float)_random.NextDouble() * 2f - 1f)
				: 1f;
			player.Play();
			entry.Started = true;
			entry.LastStartedMsec = now;
			return;
		}
	}

	/// <summary>Keeps a looping sound playing. Call it every frame it should be heard; it fades out once the calls stop.</summary>
	public void Hold(string id)
	{
		if (_entries.TryGetValue(id, out Entry entry) && entry.Definition.Loop)
			entry.LoopWanted = true;
	}

	public override void _Process(double delta)
	{
		float fade = Audio.LoopFadeSeconds > 0f ? (float)delta / Audio.LoopFadeSeconds : 1f;
		foreach (Entry entry in _loops)
		{
			entry.LoopLevel = Mathf.MoveToward(entry.LoopLevel, entry.LoopWanted ? 1f : 0f, fade);
			entry.LoopWanted = false;

			AudioStreamPlayer player = entry.Players[0];
			if (entry.LoopLevel > 0f)
			{
				player.VolumeDb = ToDb(Loudness(entry.Definition) * entry.LoopLevel);
				if (!player.Playing)
					player.Play();
			}
			else if (player.Playing)
			{
				player.Stop();
			}
		}
	}

	/// <summary>How loud a sound plays, from 0 to 1: its own volume times its category volume from
	/// audio.tres, times the player's master and effects volumes from the pause menu's settings.</summary>
	private static float Loudness(SoundDefinition definition)
	{
		return definition.Volume * Settings.MasterVolume * Settings.EffectsVolume * CategoryVolume(definition.Category);
	}

	private static float CategoryVolume(SoundCategory category) => category switch
	{
		SoundCategory.Weapons => Audio.WeaponsVolume,
		SoundCategory.Hits => Audio.HitsVolume,
		SoundCategory.Deaths => Audio.DeathsVolume,
		SoundCategory.Player => Audio.PlayerVolume,
		SoundCategory.Harvesting => Audio.HarvestingVolume,
		SoundCategory.Drones => Audio.DronesVolume,
		SoundCategory.Alerts => Audio.AlertsVolume,
		SoundCategory.Menus => Audio.MenusVolume,
		_ => Audio.ResultsVolume,
	};

	private static float ToDb(float linear) => linear <= 0.0001f ? -80f : Mathf.LinearToDb(linear);

	/// <summary>A copy of a sound set to loop end to end, whatever its file says.</summary>
	private static AudioStream Looping(AudioStream stream)
	{
		switch (stream)
		{
			case AudioStreamWav wav:
				var copy = (AudioStreamWav)wav.Duplicate();
				copy.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
				copy.LoopBegin = 0;
				copy.LoopEnd = (int)(wav.GetLength() * wav.MixRate);
				return copy;
			case AudioStreamOggVorbis ogg:
				var oggCopy = (AudioStreamOggVorbis)ogg.Duplicate();
				oggCopy.Loop = true;
				return oggCopy;
			default:
				return stream;
		}
	}
}
