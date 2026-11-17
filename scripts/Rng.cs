using Godot;
using System;

/// <summary>Random helpers for the plane.</summary>
public static class Rng
{
	private static readonly Random Random = new();

	/// <summary>A random number from min up to max.</summary>
	public static float Range(float min, float max) => min + (max - min) * Random.NextSingle();

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
