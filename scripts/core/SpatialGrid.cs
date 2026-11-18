using Godot;
using System;

/// <summary>
/// Finds which units are near a point without comparing every unit against every other.
/// The plane is divided into square cells. Each unit is filed under its cell, and a search
/// only looks in the cells the search circle touches. Refill it every frame.
/// </summary>
public sealed class SpatialGrid
{
	private const int BucketCount = 4096; // Must be a power of two.

	private readonly float _cellsPerUnit;
	private readonly int[] _firstInBucket = new int[BucketCount];
	private int[] _nextInBucket = new int[256];
	private int[] _cellX = new int[256];
	private int[] _cellY = new int[256];

	/// <param name="cellSize">Width of a cell. Use the largest radius that will be searched.</param>
	public SpatialGrid(float cellSize)
	{
		_cellsPerUnit = 1f / cellSize;
		Clear();
	}

	public void Clear() => Array.Fill(_firstInBucket, -1);

	/// <summary>Files unit number <paramref name="id"/> under the cell at <paramref name="position"/>.</summary>
	public void Insert(int id, Vector2 position)
	{
		if (id >= _nextInBucket.Length)
		{
			int size = Math.Max(id + 1, _nextInBucket.Length * 2);
			Array.Resize(ref _nextInBucket, size);
			Array.Resize(ref _cellX, size);
			Array.Resize(ref _cellY, size);
		}

		int x = Mathf.FloorToInt(position.X * _cellsPerUnit);
		int y = Mathf.FloorToInt(position.Y * _cellsPerUnit);
		int bucket = Bucket(x, y);
		_cellX[id] = x;
		_cellY[id] = y;
		_nextInBucket[id] = _firstInBucket[bucket];
		_firstInBucket[bucket] = id;
	}

	/// <summary>
	/// Writes the ids of units in the cells around <paramref name="center"/> into <paramref name="results"/>
	/// and returns how many were written. Some may be slightly farther than <paramref name="radius"/>,
	/// so check the real distance. Stops when <paramref name="results"/> is full.
	/// </summary>
	public int Query(Vector2 center, float radius, int[] results)
	{
		int minX = Mathf.FloorToInt((center.X - radius) * _cellsPerUnit);
		int maxX = Mathf.FloorToInt((center.X + radius) * _cellsPerUnit);
		int minY = Mathf.FloorToInt((center.Y - radius) * _cellsPerUnit);
		int maxY = Mathf.FloorToInt((center.Y + radius) * _cellsPerUnit);

		int found = 0;
		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				// Different cells can share a bucket, so confirm the cell before accepting a unit.
				for (int id = _firstInBucket[Bucket(x, y)]; id >= 0; id = _nextInBucket[id])
				{
					if (_cellX[id] != x || _cellY[id] != y)
						continue;
					if (found == results.Length)
						return found;
					results[found++] = id;
				}
			}
		}
		return found;
	}

	private static int Bucket(int x, int y) =>
		(int)(((uint)(x * 73856093) ^ (uint)(y * 19349663)) & (BucketCount - 1));
}
