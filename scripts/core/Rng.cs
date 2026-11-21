using Godot;
using System;

/// <summary>
/// Random helpers for the plane. Every random choice in a fight comes from here, and the fight seeds it as it
/// starts, so the same seed (and the same inputs and tuning) gives the same fight. Effects and sounds use
/// their own randomness, so turning them up, down or off never changes a fight.
/// </summary>
public static class Rng
{
	private static Random _random = new();

	/// <summary>How many random numbers have been drawn since the last seed. Two runs of the same fight that
	/// should match can be compared by this, to find where one of them first drew differently.</summary>
	public static long Draws { get; private set; }

	private static Random Random
	{
		get
		{
			Draws++;
			return _random;
		}
	}

	/// <summary>Starts the sequence of random numbers again from a seed.</summary>
	public static void Seed(int seed)
	{
		_random = new Random(seed);
		Draws = 0;
	}

	/// <summary>A seed picked at random, for a fight nobody asked to repeat.</summary>
	public static int NewSeed() => System.Random.Shared.Next(1, int.MaxValue);

	/// <summary>A random number from 0 up to 1.</summary>
	public static float Value() => Random.NextSingle();

	/// <summary>A random number from min up to max.</summary>
	public static float Range(float min, float max) => min + (max - min) * Random.NextSingle();

	/// <summary>A random whole number from min to max, both included.</summary>
	public static int Int(int min, int max) => Random.Next(min, max + 1);

	/// <summary>A random direction of length 1.</summary>
	public static Vector2 Direction()
	{
		float angle = Random.NextSingle() * Mathf.Tau;
		return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
	}

	/// <summary>A random point inside a circle, spread evenly over its area.</summary>
	public static Vector2 InsideCircle(float radius) => Direction() * radius * Mathf.Sqrt(Random.NextSingle());

	/// <summary>Either 1 or -1.</summary>
	public static float Sign() => Random.Next(2) == 0 ? 1f : -1f;
}
