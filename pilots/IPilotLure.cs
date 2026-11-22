using Godot;
using System.Collections.Generic;

/// <summary>
/// A weapon that leaves spots behind which enemies should be led across, such as Mine Layer's mines. A pilot whose
/// weapon profile asks for it (LeadChasers) steers through these spots when enemies are chasing it.
/// </summary>
public interface IPilotLure
{
	/// <summary>Adds the spots enemies should be led across to <paramref name="spots"/>.</summary>
	void CollectLureSpots(List<Vector2> spots);
}
