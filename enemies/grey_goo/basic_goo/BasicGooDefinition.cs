using Godot;

/// <summary>
/// The basic goo, the most common Grey Goo type: a slow blob that goes after drones first, drains and absorbs
/// them, grows with each one, and splits in two once it has absorbed enough. Everything but splitting is shared
/// by the Grey Goo types (GreyGooDefinition). BasicGoo.cs runs it.
///
/// A split shares the bulk, health and absorbed drones evenly between the two halves. Sometimes the second half is
/// one of the other Grey Goo types instead, fresh at the fight's difficulty (OtherTypeChance).
/// </summary>
[GlobalClass]
public partial class BasicGooDefinition : GreyGooDefinition
{
	/// <summary>Drones it absorbs before it splits in two. Each half starts counting again from 0.</summary>
	[ExportGroup("Splitting")]
	[Export] public int DronesToSplit;

	/// <summary>Seconds it bulges before it splits, so the player can see it coming.</summary>
	[Export] public float SplitBulgeSeconds;

	/// <summary>The chance, from 0 to 1, that the second half of a split is one of the other Grey Goo types instead
	/// of another basic goo, each type equally likely. That half is a new one of its type, at the fight's difficulty,
	/// awake; it doesn't share what the basic goo had grown, and the basic goo half keeps its share. Set by the
	/// designer at about one in three.</summary>
	[Export] public float OtherTypeChance;
}
