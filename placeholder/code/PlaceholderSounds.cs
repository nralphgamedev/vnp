using Godot;
using System;

/// <summary>
/// Makes every placeholder sound by mixing simple tones and noise, and saves each as a .wav file in
/// res://placeholder/audio/. PlaceholderBaker runs it. To change a sound, change its recipe here and bake
/// again. Final sounds can simply replace the .wav files.
/// </summary>
public static class PlaceholderSounds
{
	private const int Rate = 44100;

	private enum Wave { Sine, Square, Saw, Triangle }

	// How a tone's loudness changes: Decay starts loud and dies away; Hold stays level and ends briefly.
	private enum Shape { Decay, Hold }

	// Musical notes used by the tunes, in Hz.
	private const float A3 = 220f, C4 = 261.63f, D4 = 293.66f, F4 = 349.23f, A4 = 440f;
	private const float C5 = 523.25f, E5 = 659.25f, G5 = 783.99f, C6 = 1046.5f, E6 = 1318.5f;

	public static void Bake(string folder)
	{
		DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
		// A fixed seed, so baking twice gives the same sounds.
		var random = new Random(7);

		// ───────────── Weapons ─────────────
		Save(folder, "pulse_laser_fire", Make(0.18f, b =>
		{
			Tone(b, 0f, 0.16f, 1500f, 250f, Wave.Saw, 0.6f);
			Tone(b, 0f, 0.10f, 3000f, 600f, Wave.Sine, 0.25f);
			Noise(b, random, 0f, 0.04f, 8000f, 3000f, 0.3f);
		}));
		Save(folder, "shard_hit", Make(0.12f, b =>
		{
			Tone(b, 0f, 0.10f, 2100f, 1900f, Wave.Sine, 0.5f);
			Tone(b, 0f, 0.08f, 3150f, 2900f, Wave.Sine, 0.3f);
			Noise(b, random, 0f, 0.015f, 9000f, 9000f, 0.25f);
		}));
		Save(folder, "drone_projectile_fire", Make(0.06f, b => Tone(b, 0f, 0.05f, 1100f, 750f, Wave.Square, 0.5f)));
		Save(folder, "drone_beam_tick", Make(0.07f, b =>
		{
			Tone(b, 0f, 0.06f, 300f, 260f, Wave.Saw, 0.4f);
			Noise(b, random, 0f, 0.05f, 3000f, 1500f, 0.3f);
		}));
		Save(folder, "enemy_fire", Make(0.12f, b =>
		{
			Tone(b, 0f, 0.11f, 620f, 240f, Wave.Triangle, 0.6f);
			Tone(b, 0f, 0.08f, 310f, 120f, Wave.Square, 0.2f);
		}));

		// ───────────── Hits and deaths ─────────────
		Save(folder, "hit", Make(0.07f, b =>
		{
			Noise(b, random, 0f, 0.06f, 5000f, 800f, 0.6f);
			Tone(b, 0f, 0.05f, 260f, 140f, Wave.Sine, 0.5f);
		}));
		Save(folder, "enemy_death", Make(0.5f, b =>
		{
			Noise(b, random, 0f, 0.45f, 4000f, 150f, 0.7f);
			Tone(b, 0f, 0.35f, 140f, 40f, Wave.Sine, 0.7f);
			Tone(b, 0f, 0.20f, 90f, 45f, Wave.Square, 0.2f);
		}));
		Save(folder, "drone_death", Make(0.2f, b =>
		{
			Noise(b, random, 0f, 0.15f, 7000f, 900f, 0.5f);
			Tone(b, 0f, 0.14f, 900f, 250f, Wave.Sine, 0.5f);
		}));

		// ───────────── The VNP ─────────────
		Save(folder, "vnp_damage", Make(0.35f, b =>
		{
			Tone(b, 0f, 0.30f, 180f, 55f, Wave.Sine, 0.8f);
			Noise(b, random, 0f, 0.20f, 2500f, 300f, 0.5f);
			Tone(b, 0f, 0.20f, 90f, 60f, Wave.Saw, 0.25f);
		}));
		// A soft, low heartbeat: two muffled thumps. It repeats while health is low, so it stays gentle.
		Save(folder, "low_health", Make(0.45f, b =>
		{
			Tone(b, 0f, 0.14f, 120f, 60f, Wave.Sine, 0.8f);
			Tone(b, 0f, 0.05f, 240f, 160f, Wave.Triangle, 0.15f);
			Tone(b, 0.2f, 0.16f, 110f, 55f, Wave.Sine, 0.6f);
			Tone(b, 0.2f, 0.05f, 220f, 150f, Wave.Triangle, 0.1f);
		}));

		// ───────────── Harvesting and drones ─────────────
		Save(folder, "harvest_loop", HarvestLoop(), peak: 0.5f);
		Save(folder, "material_pop", Make(0.18f, b =>
		{
			Tone(b, 0f, 0.08f, 300f, 900f, Wave.Sine, 0.6f);
			Tone(b, 0.05f, 0.07f, 900f, 1400f, Wave.Sine, 0.3f);
			Noise(b, random, 0f, 0.05f, 6000f, 2000f, 0.2f);
		}));
		Save(folder, "drone_built", Make(0.22f, b =>
		{
			Tone(b, 0f, 0.18f, 500f, 1300f, Wave.Sine, 0.5f);
			Tone(b, 0f, 0.14f, 1000f, 2600f, Wave.Triangle, 0.2f);
		}));

		// ───────────── Alerts ─────────────
		// Two soft rising sweeps, each fading out: noticeable without blaring.
		Save(folder, "wave_warning", Make(0.8f, b =>
		{
			for (int i = 0; i < 2; i++)
			{
				Tone(b, i * 0.35f, 0.4f, 380f, 560f, Wave.Triangle, 0.5f);
				Tone(b, i * 0.35f, 0.4f, 190f, 280f, Wave.Sine, 0.3f);
			}
		}));
		// Two low, round tones, each falling a little and dying away, the second lower: a sober warning.
		// After playtesting, this replaced two held, buzzy pulses a semitone apart, which were too jarring.
		Save(folder, "time_limit", Make(1.1f, b =>
		{
			for (int i = 0; i < 2; i++)
			{
				float at = i * 0.45f;
				float pitch = i == 0 ? A3 : 196f;
				Tone(b, at, 0.55f, pitch, pitch * 0.9f, Wave.Triangle, 0.45f);
				Tone(b, at, 0.55f, pitch * 0.5f, pitch * 0.45f, Wave.Sine, 0.35f);
			}
		}));

		// ───────────── Menus ─────────────
		Save(folder, "level_up", Make(0.7f, b =>
		{
			float[] notes = { C5, E5, G5, C6 };
			for (int i = 0; i < notes.Length; i++)
			{
				Tone(b, i * 0.11f, 0.3f, notes[i], notes[i], Wave.Triangle, 0.5f);
				Tone(b, i * 0.11f, 0.3f, notes[i] * 0.5f, notes[i] * 0.5f, Wave.Sine, 0.25f);
			}
		}));
		Save(folder, "upgrade_choose", Make(0.3f, b =>
		{
			Tone(b, 0f, 0.12f, 660f, 660f, Wave.Triangle, 0.5f);
			Tone(b, 0.09f, 0.18f, 990f, 990f, Wave.Triangle, 0.5f);
		}));
		Save(folder, "menu_move", Make(0.05f, b =>
		{
			Tone(b, 0f, 0.035f, 1300f, 1300f, Wave.Sine, 0.5f);
			Tone(b, 0f, 0.02f, 1300f, 1300f, Wave.Square, 0.1f);
		}));
		Save(folder, "menu_select", Make(0.12f, b =>
		{
			Tone(b, 0f, 0.10f, 880f, 880f, Wave.Triangle, 0.5f);
			Tone(b, 0f, 0.06f, 1760f, 1760f, Wave.Sine, 0.3f);
		}));

		// ───────────── Results ─────────────
		Save(folder, "win", Make(1.4f, b =>
		{
			float[] notes = { C5, E5, G5, C6, E6 };
			for (int i = 0; i < notes.Length; i++)
			{
				Tone(b, i * 0.12f, 0.9f, notes[i], notes[i], Wave.Triangle, 0.4f);
				Tone(b, i * 0.12f, 0.9f, notes[i] * 0.5f, notes[i] * 0.5f, Wave.Sine, 0.2f);
			}
		}));
		Save(folder, "loss", Make(1.4f, b =>
		{
			float[] notes = { A4, F4, D4, A3 };
			for (int i = 0; i < notes.Length; i++)
				Tone(b, i * 0.22f, 0.8f, notes[i], notes[i] * 0.98f, Wave.Triangle, 0.5f);
			Tone(b, 0f, 1.3f, 110f, 80f, Wave.Sine, 0.4f);
		}));
		Save(folder, "retreat", Make(1.0f, b =>
		{
			Tone(b, 0f, 0.8f, 400f, 900f, Wave.Sine, 0.5f, Shape.Hold);
			Tone(b, 0.1f, 0.7f, 1200f, 1800f, Wave.Triangle, 0.15f);
			Tone(b, 0.5f, 0.5f, C4 * 2f, C4 * 2f, Wave.Triangle, 0.3f);
		}));

		// ───────────── Stage 4 weapons ─────────────
		// Added last, so the sounds above keep the same random noise when baked again.
		Save(folder, "scatter_shot_fire", Make(0.16f, b =>
		{
			Noise(b, random, 0f, 0.13f, 6000f, 1200f, 0.7f);
			Tone(b, 0f, 0.08f, 220f, 110f, Wave.Square, 0.3f);
		}));
		Save(folder, "seeker_launch", Make(0.28f, b =>
		{
			Noise(b, random, 0f, 0.26f, 800f, 3000f, 0.5f);
			Tone(b, 0f, 0.2f, 300f, 600f, Wave.Sine, 0.2f);
		}));
		Save(folder, "seeker_hit", Make(0.26f, b =>
		{
			Noise(b, random, 0f, 0.22f, 5000f, 300f, 0.7f);
			Tone(b, 0f, 0.18f, 180f, 60f, Wave.Sine, 0.5f);
		}));

		// ───────────── Stage 5 weapons ─────────────
		Save(folder, "shockwave_fire", Make(0.5f, b =>
		{
			Tone(b, 0f, 0.4f, 160f, 50f, Wave.Sine, 0.8f);
			Noise(b, random, 0f, 0.45f, 1200f, 300f, 0.35f);
			Tone(b, 0f, 0.25f, 600f, 200f, Wave.Triangle, 0.15f);
		}));
		Save(folder, "mine_drop", Make(0.1f, b =>
		{
			Tone(b, 0f, 0.05f, 700f, 650f, Wave.Triangle, 0.5f);
			Tone(b, 0.04f, 0.05f, 950f, 900f, Wave.Triangle, 0.35f);
		}));
		Save(folder, "mine_explode", Make(0.45f, b =>
		{
			Noise(b, random, 0f, 0.4f, 6000f, 200f, 0.8f);
			Tone(b, 0f, 0.3f, 120f, 40f, Wave.Sine, 0.7f);
			Tone(b, 0f, 0.12f, 300f, 120f, Wave.Square, 0.2f);
		}));

		// ───────────── Stage 6 ─────────────
		Save(folder, "ram_hit", Make(0.16f, b =>
		{
			Noise(b, random, 0f, 0.08f, 4000f, 900f, 0.5f);
			Tone(b, 0f, 0.12f, 420f, 180f, Wave.Square, 0.35f);
			Tone(b, 0f, 0.1f, 900f, 700f, Wave.Triangle, 0.2f);
		}));
		Save(folder, "shard_block", Make(0.1f, b =>
		{
			Tone(b, 0f, 0.08f, 2600f, 2300f, Wave.Triangle, 0.5f);
			Noise(b, random, 0f, 0.03f, 9000f, 6000f, 0.25f);
		}));
	}

	/// <summary>
	/// A soft hum with a gentle pulse, two seconds long. Every frequency fits a whole number of times into
	/// two seconds, so the end joins the start and it loops without a click.
	/// </summary>
	private static float[] HarvestLoop()
	{
		const float Seconds = 2f;
		var b = new float[(int)(Seconds * Rate)];
		for (int i = 0; i < b.Length; i++)
		{
			float t = (float)i / Rate;
			float pulse = 0.75f + 0.25f * Mathf.Sin(Mathf.Tau * 4f * t);
			float hum = 0.5f * Mathf.Sin(Mathf.Tau * 110f * t)
				+ 0.25f * Mathf.Sin(Mathf.Tau * 165f * t)
				+ 0.2f * Mathf.Sin(Mathf.Tau * 220f * t);
			float sparkle = 0.06f * Mathf.Sin(Mathf.Tau * 1320f * t) * Mathf.Pow(Mathf.Sin(Mathf.Tau * 1f * t), 2f);
			b[i] = hum * pulse + sparkle;
		}
		return b;
	}

	private static float[] Make(float seconds, Action<float[]> fill)
	{
		var buffer = new float[(int)(seconds * Rate)];
		fill(buffer);
		return buffer;
	}

	/// <summary>Adds a tone that slides from one pitch to another.</summary>
	private static void Tone(float[] b, float start, float duration, float fromHz, float toHz, Wave wave, float volume,
		Shape shape = Shape.Decay)
	{
		int first = (int)(start * Rate);
		int count = (int)(duration * Rate);
		double phase = 0;
		for (int i = 0; i < count && first + i < b.Length; i++)
		{
			float t = (float)i / count;
			double hz = fromHz * Math.Pow(toHz / fromHz, t);
			phase += hz / Rate;
			b[first + i] += Sample(wave, phase) * volume * Envelope(i / (float)Rate, duration, shape);
		}
	}

	/// <summary>Adds a burst of noise, muffled more or less as it goes: high cutoffs hiss, low ones rumble.</summary>
	private static void Noise(float[] b, Random random, float start, float duration, float fromCutoff, float toCutoff,
		float volume)
	{
		int first = (int)(start * Rate);
		int count = (int)(duration * Rate);
		float smoothed = 0f;
		for (int i = 0; i < count && first + i < b.Length; i++)
		{
			float t = (float)i / count;
			float cutoff = fromCutoff * Mathf.Pow(toCutoff / fromCutoff, t);
			float a = 1f - Mathf.Exp(-Mathf.Tau * cutoff / Rate);
			smoothed += a * ((float)random.NextDouble() * 2f - 1f - smoothed);
			b[first + i] += smoothed * volume * Envelope(i / (float)Rate, duration, Shape.Decay);
		}
	}

	private static float Envelope(float t, float duration, Shape shape)
	{
		const float Attack = 0.003f;
		if (t < Attack)
			return t / Attack;
		if (shape == Shape.Hold)
			return Mathf.Clamp((duration - t) / 0.02f, 0f, 1f);
		float left = 1f - (t - Attack) / Mathf.Max(0.0001f, duration - Attack);
		return left * left;
	}

	private static float Sample(Wave wave, double phase)
	{
		float p = (float)(phase - Math.Floor(phase));
		return wave switch
		{
			Wave.Sine => Mathf.Sin(Mathf.Tau * p),
			// A softened square: brighter than a sine, without a harsh edge.
			Wave.Square => (float)Math.Tanh(4.0 * Math.Sin(Math.Tau * p)) * 0.8f,
			Wave.Saw => 2f * p - 1f,
			_ => 1f - 4f * Mathf.Abs(p - 0.5f),
		};
	}

	/// <summary>Scales a sound so its loudest point is <paramref name="peak"/>, and saves it as 16-bit mono .wav.</summary>
	private static void Save(string folder, string name, float[] samples, float peak = 0.8f)
	{
		float loudest = 0f;
		foreach (float s in samples)
			loudest = Mathf.Max(loudest, Mathf.Abs(s));
		float scale = loudest > 0f ? peak / loudest : 1f;

		var data = new byte[samples.Length * 2];
		for (int i = 0; i < samples.Length; i++)
		{
			short value = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * scale * 32767f), -32768, 32767);
			data[i * 2] = (byte)(value & 0xff);
			data[i * 2 + 1] = (byte)((value >> 8) & 0xff);
		}

		var stream = new AudioStreamWav
		{
			Format = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = Rate,
			Stereo = false,
			Data = data,
		};
		string path = $"{folder}/{name}.wav";
		Error error = stream.SaveToWav(path);
		if (error != Error.Ok)
			GD.PushError($"Could not save {path}: {error}");
	}
}
