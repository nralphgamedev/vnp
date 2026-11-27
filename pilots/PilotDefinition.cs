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

	/// <summary>How much more threatening an enemy that absorbs drones, such as a basic goo, is when the VNP relies on
	/// its drones: with drones at their cap, it counts this many times as much as another enemy.</summary>
	[Export] public float DroneEaterThreat;

	/// <summary>How far from the edge of an enemy that damages by touch, such as Grey Goo, it starts steering clear,
	/// whatever else it wants. Added after the designer's logs showed them taking almost no damage from Goos,
	/// while pilots took about a fifth of theirs from them.</summary>
	[Export] public float TouchAvoidDistance;

	/// <summary>Health, as a fraction of the most it can have, below which it starts being more careful: its
	/// weapons' LowHealthDistance blends in, and it keeps farther clear of enemies that damage by touch, fully so
	/// at no health. 0 turns it off. At the designer's description: at low health they give enemies a wider
	/// berth, except with short-range weapons, which they keep in range.</summary>
	[Export] public float LowHealthBelow;

	/// <summary>How much farther it keeps clear of enemies that damage by touch at no health: TouchAvoidDistance
	/// grows by this fraction of itself, in step with how far health is below LowHealthBelow. A touch that would
	/// end the fight is worth a wider margin.</summary>
	[Export] public float LowHealthTouchScale;

	/// <summary>From 0 to 1: how much it closes in on an enemy farther than its preferred distance. 1 goes after
	/// enemies to fight them; near 0 keeps its distance and lets enemies come to it and its drones, as the
	/// designer does, so a fight it can afford never pulls it away from harvesting.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float ApproachEnemies = 1f;

	/// <summary>How much an enemy near the way to some material counts as danger at that material, as a fraction
	/// of an enemy beside it. The designer watched a pilot fly straight through a group of enemies to reach a
	/// cluster on the far side: only enemies around the material counted, so the cluster looked safe.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float PathDanger;

	/// <summary>From 0 to 1, of the drone cap: with fewer drones than this, once it starts backing off it regroups,
	/// as the designer does with no drones left. It keeps backing off until no enemy is within its threat range,
	/// doesn't fight, and prefers rich clusters to nearby small rocks, until its drones reach RegroupUntil.
	/// 0 never regroups.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float RegroupBelow;

	/// <summary>How fast the pressure it feels can fall, per second. Pressure rises at once when danger comes, but
	/// falls only this fast, so once it starts backing off it follows through rather than flicking between
	/// backing off and fighting as enemies move in and out of range, which the designer saw as indecision.
	/// 0 lets it fall at once.</summary>
	[Export] public float PressureFall;

	/// <summary>While regrouping, it backs off fully while any enemy is within this distance. 0 backs off only as
	/// its pressure says, so it can harvest near a fight when the danger is low, as the designer does. The first
	/// review found that at its threat range (38), late in a fight it was never clear, and spent 64% of its
	/// decisions heading for material and 8% harvesting it, so its drones were never rebuilt.</summary>
	[Export] public float RegroupClearRange;

	/// <summary>With drones that matter, back off all or nothing: either stay in the fight with the drones, or
	/// withdraw until no enemy is within the drones' reach plus WithdrawMargin, so they break off and follow. The
	/// first review found pilots half-retreating, leaving their drones to fight packs alone.</summary>
	[Export] public bool CommitOrWithdraw;
	[Export] public float WithdrawMargin = 5f;
	/// <summary>Stops withdrawing after this long even if not yet clear, so it can't flee forever.</summary>
	[Export] public float WithdrawMaxSeconds = 8f;

	/// <summary>From 0 to 1, of the drone cap: how many drones it rebuilds before it stops regrouping.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float RegroupUntil;

	/// <summary>Below this pressure (the threat it feels over what it can afford), it holds still in a fight,
	/// leaving the drones and weapons to do the work, as the designer does when there isn't much danger. It
	/// still closes in on an enemy out of its weapons' reach. From here to half as much again it blends back
	/// into moving and circling. While harvesting, it keeps holding at its rock. 0 never holds still.</summary>
	[Export] public float HoldStillPressure;

	/// <summary>How hard it steers clear of enemies that damage by touch, at their edge.</summary>
	[Export] public float TouchAvoidWeight;

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

	// How it plans its moves (docs/dev/PILOT_SPEC.md): it perceives the threats enemies and shots declare, and
	// scores candidate paths through them with Arbiter, by the judgements above.
	[ExportGroup("Planning")]
	/// <summary>Seconds ahead each candidate path looks, predicting where it and the hazards will be.</summary>
	[Export] public float LookAheadSeconds = 1f;

	/// <summary>Directions it tries, evenly round the circle, each at a few speeds, plus stopping.</summary>
	[Export] public int PathDirections = 16;

	/// <summary>The most hazards it keeps track of at once, nearest first, as a player can only watch so much. It
	/// also bounds the planner's cost in a big crowd. 0 for no limit.</summary>
	[Export] public int Attention = 64;

	/// <summary>How much it minds harm a path would take, as a fraction of its health now, so the same hit matters
	/// more at low health. 10 makes losing a tenth of its health count as much as a full second of its best want.</summary>
	[Export] public float HarmWeight = 10f;

	/// <summary>How much it minds its drones being near hazards that hurt them, such as a drone eater, scaled by
	/// how much it relies on drones (their share of the cap).</summary>
	[Export] public float DroneDangerWeight = 1f;

	/// <summary>How much it minds turning sharply or reversing, against flicking from one way to another.</summary>
	[Export] public float TurnCost = 0.2f;

	/// <summary>While fighting and not holding still, how much it circles the enemy it fights, as a share of its
	/// fight weight, as the first pilot did.</summary>
	[Export] public float OrbitShare = 0.6f;

	/// <summary>How strongly it closes in on its harvest target once committed (HarvestCommitDistance).</summary>
	[Export] public float CommitWeight = 2f;

	/// <summary>Randomness added to every upgrade's score when it chooses, on top of its build's. Lower is more
	/// consistent.</summary>
	[ExportGroup("Choosing upgrades")]
	[Export] public float PickNoise;

	/// <summary>Seconds it looks at a level-up offer before choosing, so a watched fight can be followed. It
	/// costs a batch nothing, since the fight is paused meanwhile.</summary>
	[Export] public float UpgradeDecisionSeconds;
}
