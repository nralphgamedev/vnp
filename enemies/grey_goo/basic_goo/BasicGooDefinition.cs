using Godot;

/// <summary>
/// The basic goo, the most common Grey Goo type: a slow blob that goes after drones first, drains and absorbs
/// them, grows with each one, and splits in two once it has absorbed enough. Everything but splitting is shared
/// by the Grey Goo types (GreyGooDefinition). BasicGoo.cs runs it.
///
/// A split shares the bulk, health and absorbed drones evenly between the two halves.
/// </summary>
[GlobalClass]
public partial class BasicGooDefinition : GreyGooDefinition
{
	/// <summary>Drones it absorbs before it splits in two. Each half starts counting again from 0.</summary>
	[ExportGroup("Splitting")]
	[Export] public int DronesToSplit;

	/// <summary>Seconds it bulges before it splits, so the player can see it coming.</summary>
	[Export] public float SplitBulgeSeconds;
}
