using Godot;

/// <summary>
/// A pilot's skill level: how it sees, reacts, and weighs what it wants, when it flies a VNP in place of a player.
/// One file per level in res://pilots/skill_levels/. Pilot.cs uses it.
///
/// At its heart is one judgement: can it afford the fight it is in? Each enemy near it adds threat, closer
/// enemies more. What it can afford grows with its health, its drones and its level, and shrinks with caution.
/// While the threat is affordable it fights and harvests; as it stops being affordable it backs off.
/// </summary>
[GlobalClass]
public partial class PilotDefinition : Resource
{
	/// <summary>Stays the same if the name changes. Written in fight logs.</summary>
	[Export] public string Id;

	[Export] public string Name;

	/// <summary>Seconds between its decisions about where to move. Lower reacts faster. It is varied a little
	/// each time, so decisions don't fall in lockstep.</summary>
	[ExportGroup("Seeing and reacting")]
	[Export] public float ReactionSeconds;

	/// <summary>How far around the VNP it sees enemies, shots and material. It only ever sees what is on screen,
	/// as a player does, so a radius that covers the screen means it sees everything a player would. The screen
	/// reaches about 68 units to the sides and 36 to 40 up and down.</summary>
	[Export] public float AwarenessRadius;

	/// <summary>Chance, at each decision, that it notices a shot coming at it and moves aside. 0 never dodges;
	/// 1 dodges every shot it sees in time.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float DodgeChance;

	/// <summary>How much its movement wanders from what it wants, in degrees either side. A little makes it
	/// look less mechanical; a lot makes it sloppy.</summary>
	[Export] public float WanderDegrees;

	/// <summary>Threat it can take on at full health with no drones, at level 1. One enemy right beside it is
	/// a threat of about 1.</summary>
	[ExportGroup("Judging a fight")]
	[Export] public float AffordableThreat;

	/// <summary>How far away an enemy still adds to the threat it feels, falling off to nothing at this distance.
	/// Kept apart from how far it sees, so seeing farther doesn't make every enemy feel closer.</summary>
	[Export] public float ThreatRange;

	/// <summary>How much each drone adds to what it can afford, as a fraction: 0.08 means ten drones let it take on
	/// 80% more.</summary>
	[Export] public float StrengthPerDrone;

	/// <summary>How much each level past the first adds to what it can afford, as a fraction.</summary>
	[Export] public float StrengthPerLevel;

	/// <summary>From 0 to 1: how early it backs off. 0 fights until the threat is all it can afford; 1 backs off
	/// at about half that.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float Caution;

	/// <summary>How much more threatening an enemy that absorbs drones, such as a Goo, is when the VNP relies on
	/// its drones: with drones at their cap, it counts this many times as much as another enemy.</summary>
	[Export] public float DroneEaterThreat;

	/// <summary>How strongly it wants to harvest the best material it can afford to reach, while its drones are
	/// below their cap.</summary>
	[ExportGroup("What it wants")]
	[Export] public float HarvestWeight;

	/// <summary>From 0 to 1: how much of its harvest weight it keeps once its drones are at their cap, when
	/// harvesting no longer builds drones but still gives fight experience. Lower leaves material alone at the
	/// cap; 1 harvests as much as ever.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float HarvestAtCap;

	/// <summary>How much more it wants to harvest a big concentration of material, such as an asteroid field,
	/// than a lone rock: up to this much extra harvest weight. So a rich field pulls it away from a fight it
	/// could take, unless the field itself is dangerous (danger scores material down first).</summary>
	[Export] public float ConcentrationPull;

	/// <summary>The worth of a concentration, its mass plus the cluster around it, that gets the full
	/// ConcentrationPull. Smaller concentrations get a share of it.</summary>
	[Export] public float ConcentrationMass;

	/// <summary>While backing off, how much enemies around a piece of material put it off heading there: its pull
	/// is divided by 1 + backing off × danger × this. Danger counts each enemy within 12 units of the material,
	/// closer ones more. 0 lets it harvest into danger it is backing away from; higher keeps it to safe rocks.</summary>
	[Export] public float HarvestDangerWhenBackingOff;

	/// <summary>Once its harvest target is within beam reach plus this many units, and it isn't backing off, it
	/// commits: it closes in to harvest rather than weighing the other wants, so it doesn't hover just out of
	/// reach. 0 never commits.</summary>
	[Export] public float HarvestCommitDistance;

	/// <summary>How much Debris reward upgrades make it value debris: 1 values debris exactly as much more as
	/// the upgrades make it give (two +25% upgrades, +56%), 0 ignores them.</summary>
	[Export] public float DebrisRewardInfluence;

	/// <summary>How much Harvest rate upgrades raise how much it wants to harvest: 1 by the same fraction as the
	/// upgrades speed harvesting, 0.5 by half that, 0 not at all.</summary>
	[Export] public float HarvestRateInfluence;

	/// <summary>How strongly it wants to move so its weapons and drones reach enemies, with its drones at their cap.</summary>
	[Export] public float FightWeight;

	/// <summary>From 0 to 1: how much of its fight weight it keeps with no drones at all, scaling up to the full
	/// weight at the cap. Lower means a pilot that has lost its drones goes harvesting to rebuild them rather than
	/// fighting with its VNP weapons alone.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float FightWithoutDrones;

	/// <summary>How much nearby asteroids add to a piece of material's worth, as a fraction of their mass, so it
	/// prefers clusters to lone rocks. 0 ignores them.</summary>
	[Export] public float ClusterWeight;

	/// <summary>How much nearby debris adds to a piece of material's worth, as a fraction of its mass. Keep it
	/// below ClusterWeight: debris piles up where the fighting is, so counting it fully draws the pilot into
	/// fights. 0 ignores it.</summary>
	[Export] public float DebrisClusterWeight;

	/// <summary>How far around a rock other material counts toward its cluster.</summary>
	[Export] public float ClusterRadius;

	/// <summary>How strongly it pulls away from a fight it can't afford.</summary>
	[Export] public float BackOffWeight;

	/// <summary>How strongly it moves aside from a shot it noticed.</summary>
	[Export] public float DodgeWeight;

	/// <summary>The distance it likes to keep from the enemy it is fighting: it closes in from farther, and edges
	/// away from closer.</summary>
	[Export] public float PreferredEnemyDistance;

	/// <summary>
	/// With nothing in sight, it either returns to material it remembers or explores for more. The more mass it
	/// remembers, the likelier it returns: it returns with a chance of remembered mass ÷ (remembered mass + this),
	/// with farther material counting for less (see ReturnHalfDistance). Higher explores more; lower sticks to
	/// what it knows.
	/// </summary>
	[ExportGroup("Exploring")]
	[Export] public float ExploreUrge;

	/// <summary>Seconds it roams before thinking again about returning to remembered material.</summary>
	[Export] public float ExploreRethinkSeconds;

	/// <summary>How much distance puts it off returning to remembered material: material this far away counts half
	/// as much as material beside it, both in whether it returns and where to. Very high returns by mass alone.</summary>
	[Export] public float ReturnHalfDistance;

	/// <summary>From 0 to 1: how well it plays to its weapons' strengths, as their pilot profiles describe. 0
	/// ignores the profiles and fights its own way; 1 follows them fully.</summary>
	[ExportGroup("Weapons")]
	[Export(PropertyHint.Range, "0,1,0.01")] public float TacticSkill;

	/// <summary>How strongly it steers through its weapons' lure spots, such as its mines, when enemies are
	/// chasing it, at full TacticSkill and a profile's full LeadChasers.</summary>
	[Export] public float LeadWeight;

	/// <summary>Randomness added to every upgrade's score when it chooses, on top of its build's. Lower is more
	/// consistent.</summary>
	[ExportGroup("Choosing upgrades")]
	[Export] public float PickNoise;

	/// <summary>Seconds it looks at a level-up offer before choosing, so a watched fight can be followed. It
	/// costs a batch nothing, since the fight is paused meanwhile.</summary>
	[Export] public float UpgradeDecisionSeconds;
}
