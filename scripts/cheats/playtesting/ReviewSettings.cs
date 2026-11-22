#if TOOLS
using Godot;

/// <summary>
/// Thresholds for the run review's detectors (see RunReview and docs/RUN_REVIEW_SPEC.md). Values live in
/// res://scripts/cheats/playtesting/review_settings.tres. Editor only, like everything in scripts/cheats/.
/// Distances are in world units, measured from enemies' edges; speeds are fractions of the VNP's top speed.
/// </summary>
[GlobalClass]
public partial class ReviewSettings : Resource
{
	/// <summary>A detector that fired is ready to fire again once its condition has been false this long.</summary>
	[ExportGroup("All detectors")]
	[Export] public float RearmSeconds = 2f;

	/// <summary>Touched a Goo: how far past touching still counts, to allow for a frame's movement.</summary>
	[ExportGroup("Touched a Goo")]
	[Export] public float TouchSlack = 0.2f;

	/// <summary>Flew through a group: an enemy counts as passed when the VNP comes this close to its edge.</summary>
	[ExportGroup("Flew through a group")]
	[Export] public float GroupPassDistance = 3f;
	/// <summary>Flew through a group: how many different enemies passed within GroupSeconds.</summary>
	[Export] public int GroupCount = 4;
	[Export] public float GroupSeconds = 3f;

	/// <summary>Closed in on a near enemy: an enemy this close to the VNP, which keeps moving toward it at
	/// ClosingSpeed or more for CloseSeconds.</summary>
	[ExportGroup("Closed in on a near enemy")]
	[Export] public float CloseDistance = 6f;
	[Export] public float ClosingSpeed = 0.4f;
	[Export] public float CloseSeconds = 0.6f;

	/// <summary>Indecision: the VNP's direction reverses (turns by at least ReversalDegrees between checks
	/// ReversalCheckSeconds apart, while moving at MovingSpeed or more) IndecisionReversals times within
	/// IndecisionSeconds, with an enemy within IndecisionRange.</summary>
	[ExportGroup("Indecision")]
	[Export] public float ReversalDegrees = 120f;
	[Export] public float ReversalCheckSeconds = 0.25f;
	[Export] public float MovingSpeed = 0.3f;
	[Export] public int IndecisionReversals = 3;
	[Export] public float IndecisionSeconds = 3f;
	[Export] public float IndecisionRange = 20f;

	/// <summary>Creeping: moving between CreepSpeedLow and CreepSpeedHigh of top speed for CreepSeconds, with no
	/// harvest beam working and no material within the beam's reach plus CreepHarvestMargin, so easing in on a
	/// rock doesn't count.</summary>
	[ExportGroup("Creeping")]
	[Export] public float CreepSpeedLow = 0.1f;
	[Export] public float CreepSpeedHigh = 0.7f;
	[Export] public float CreepSeconds = 1.5f;
	[Export] public float CreepHarvestMargin = 6f;

	/// <summary>Fighting without drones: no drones, an enemy within NoDronesRange, for NoDronesSeconds, without
	/// moving away from it at NoDronesFleeSpeed or more.</summary>
	[ExportGroup("Fighting without drones")]
	[Export] public float NoDronesRange = 15f;
	[Export] public float NoDronesSeconds = 3f;
	[Export] public float NoDronesFleeSpeed = 0.5f;

	/// <summary>Idle: no harvest beam working and no enemy within IdleEnemyRange, for IdleSeconds.</summary>
	[ExportGroup("Idle")]
	[Export] public float IdleSeconds = 8f;
	[Export] public float IdleEnemyRange = 25f;

	/// <summary>Drones lost in a burst: at least BurstDrones absorbed or destroyed within BurstSeconds.</summary>
	[ExportGroup("Drones lost in a burst")]
	[Export] public int BurstDrones = 6;
	[Export] public float BurstSeconds = 3f;

	/// <summary>A burst that keeps getting worse fires again, with a strip of its own, each time the drones lost
	/// within BurstSeconds reach this multiple of the count that last fired it. So a long collapse that started
	/// small still gets a strip of its worst part, rather than counting once at its start.</summary>
	[Export] public float BurstWorsens = 2f;

	/// <summary>Drones left fighting: at least LeftFightingDrones drones, an enemy within the drones' engage range
	/// of the VNP but more than LeftFightingDistance from it, and the VNP moving away from it at LeftFightingSpeed
	/// or more, for LeftFightingSeconds: the drones fight on while the VNP leaves them.</summary>
	[ExportGroup("Drones left fighting")]
	[Export] public int LeftFightingDrones = 4;
	[Export] public float LeftFightingDistance = 10f;
	[Export] public float LeftFightingSpeed = 0.4f;
	[Export] public float LeftFightingSeconds = 1.5f;

	/// <summary>Drones not rebuilt: NotRebuiltDrones or fewer for NotRebuiltSeconds, after the first minute.</summary>
	[ExportGroup("Drones not rebuilt")]
	[Export] public int NotRebuiltDrones = 2;
	[Export] public float NotRebuiltSeconds = 20f;

	/// <summary>How often the track records the VNP's position and what it was doing, in seconds.</summary>
	[ExportGroup("Maps")]
	[Export] public float TrackSeconds = 0.25f;
	/// <summary>The VNP counts as fighting with an enemy this close to it, and as moving away when it also moves
	/// away from that enemy at MovingAwaySpeed or more.</summary>
	[Export] public float FightReach = 15f;
	[Export] public float MovingAwaySpeed = 0.5f;
	/// <summary>A strip map's pictures: StripSpacing seconds apart, StripBefore of them before the moment and
	/// StripAfter after, each showing StripView units across around the VNP.</summary>
	[Export] public float StripSpacing = 1f;
	[Export] public int StripBefore = 4;
	[Export] public int StripAfter = 2;
	[Export] public float StripView = 64f;
	/// <summary>At most this many strips are saved in one fight's log, so a long fight's log stays small.</summary>
	[Export] public int MaxStripsPerFight = 24;
	/// <summary>How many overview maps a batch draws for each combination (its best, worst and a typical
	/// fight), and how many strips for each detector.</summary>
	[Export] public int OverviewsPerCombination = 3;
	[Export] public int StripsPerDetector = 3;

	/// <summary>The designer's fights record the picture around the VNP (what a strip shows) all fight long,
	/// every RecordSpacing seconds, into a compressed file beside the log, since they can't be replayed. Any
	/// moment can then be drawn afterwards with tools\maps.cmd. Pilot fights replay exactly, so they don't.</summary>
	[ExportGroup("Recording the designer's fights")]
	[Export] public bool RecordPlayerFights = true;
	[Export] public float RecordSpacing = 0.1f;

	/// <summary>Draw the designer's maps (overview and flagged moments) when the log is written. Off, they are
	/// drawn only when asked for, with tools\maps.cmd.</summary>
	[Export] public bool DrawPlayerMaps;
}

#endif
