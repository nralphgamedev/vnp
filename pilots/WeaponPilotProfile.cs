using Godot;

/// <summary>
/// How a pilot plays to a weapon's strengths, as weights on a small set of general behaviors the pilot already
/// has. Each weapon can have one, in its folder (pilot_profile.tres), named by its definition's PilotProfile.
///
/// A pilot blends the profiles of everything it has equipped, each weighted by how invested it is in that weapon:
/// 1 for having it, plus 1 for each upgrade taken for it. So weapon combinations blend without needing anything of
/// their own: a Mine Layer build with a little Scatter Shot mostly leads enemies across its mines, and closes in
/// more as Scatter Shot grows. Several behaviors for one weapon are simply several weights here. How well a pilot
/// uses all this is its skill level's TacticSkill.
/// </summary>
[GlobalClass]
public partial class WeaponPilotProfile : Resource
{
	/// <summary>The distance from an enemy's edge this weapon is best at, for the pilot to keep while fighting.
	/// 0 has no preference, leaving it to the other weapons and the pilot's own distance.</summary>
	[Export] public float PreferredDistance;

	/// <summary>From 0 to 1: how much this weapon wants the VNP always moving, such as one that only works while
	/// it moves. Instead of holding still beside a rock while harvesting, the pilot circles it.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float KeepMoving;

	/// <summary>From 0 to 1: how much this weapon wants enemies led across the spots it leaves behind, such as
	/// mines. With enemies close, the pilot steers through those spots so its chasers follow it over them.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float LeadChasers;
}
