using Godot;

/// <summary>What the Fight Director starts from (docs/dev/FIGHT_DIRECTOR_SPEC.md). Values live in
/// res://director/fight_director.tres.</summary>
[GlobalClass]
public partial class FightDirectorTuning : Resource
{
	/// <summary>The faction a fight is against when its brief names none, which is every fight until the galactic
	/// map chooses. Its file holds the plans its fights can follow.</summary>
	[Export] public FactionDefinition DefaultFaction;
}
