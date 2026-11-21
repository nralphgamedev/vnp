using Godot;

/// <summary>
/// A weapon that drops mines behind the VNP while it moves. A mine explodes when an enemy touches it,
/// damaging every enemy within its blast. Cooldown is the time between mines; area of effect widens the
/// blast. Mines are dropped only while the VNP is moving. Each lasts a set time, and if a new mine would
/// go over the most allowed at once, the oldest one disappears.
/// </summary>
[GlobalClass]
public partial class MineLayerDefinition : WeaponDefinition
{
	/// <summary>Radius of a mine's blast, in units, before area of effect upgrades.</summary>
	[Export] public float BlastRadius;

	/// <summary>A mine goes off when an enemy's edge comes this close to it.</summary>
	[Export] public float TriggerRadius;

	/// <summary>Seconds a mine lasts before it fizzles out.</summary>
	[Export] public float MineLifetime;

	/// <summary>Most mines out at once. A new mine past this makes the oldest disappear.</summary>
	[Export] public int MaxMines;

	/// <summary>The VNP counts as moving, and drops mines, above this speed in units per second.</summary>
	[Export] public float MinMoveSpeed;

	/// <summary>How far behind the VNP a mine is dropped.</summary>
	[Export] public float DropDistance;

	/// <summary>Scale of a mine.</summary>
	[Export] public float MineSize;
}
