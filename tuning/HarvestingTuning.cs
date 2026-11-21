using Godot;

/// <summary>Harvest beams, fight experience from mass, and building temporary drones. Values live in res://tuning/harvesting.tres.</summary>
[GlobalClass]
public partial class HarvestingTuning : Resource
{
	// Harvest beams draw mass out of asteroids and debris gradually, and experience and
	// drone-building progress arrive as it comes. Harvest time is the mass divided by the total
	// rate of the beams on it: the VNP alone takes half a second per unit of mass.

	/// <summary>Mass the VNP's harvest beam draws out per second. Kept well above a drone's rate.</summary>
	[ExportGroup("Harvesting")]
	[Export] public float VnpRate;

	/// <summary>Mass one drone's harvest beam draws out per second.
	/// More drones on the same asteroid harvest it faster.</summary>
	[Export] public float DroneRate;

	/// <summary>How far a harvest beam reaches past the edge of an asteroid or piece of debris.
	/// Used by the VNP and the drones.</summary>
	[Export] public float BeamRange;

	/// <summary>Fight experience gained per unit of mass harvested.</summary>
	[Export] public float ExperiencePerMass;

	/// <summary>Mass that must be harvested to build one temporary drone.</summary>
	[Export] public float MassPerDrone;

	/// <summary>Drones go to harvest asteroids and debris whose edge comes within this distance
	/// of the VNP, once there is no enemy to attack.</summary>
	[Export] public float DroneRange;

	/// <summary>How far beyond the edge of an asteroid a drone sits while harvesting it.</summary>
	[Export] public float DroneStandoff;

	/// <summary>Scale of a piece of debris with mass 1. Heavier pieces are drawn larger,
	/// and every piece shrinks as it is harvested.</summary>
	[ExportGroup("Debris look")]
	[Export] public float DebrisSize;

	[Export] public Color DebrisColor;

	/// <summary>Slowest debris tumble, in degrees per second. Every piece of debris tumbles around a random axis.</summary>
	[Export] public float DebrisTumbleSpeedMin;

	/// <summary>Fastest debris tumble, in degrees per second.</summary>
	[Export] public float DebrisTumbleSpeedMax;
}
