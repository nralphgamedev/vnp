// Checks of Arbiter on its own, with no game: the geometry of areas, when hazards hurt, and what a situation
// adds up. Run by the stage check; prints PASS or FAIL lines and exits with the number of failures.
using System;
using System.Numerics;
using Arbiter;

int fails = 0;
void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
bool Near(float a, float b) => MathF.Abs(a - b) < 1e-3f;

var circle = Area.Circle(new Vector2(0, 0), 2f, new Vector2(1, 0));
Check(Near(circle.DistanceAt(new Vector2(5, 0), 0), 3f), "circle distance now");
Check(Near(circle.DistanceAt(new Vector2(5, 0), 2), 1f), "circle distance after moving 2 s");
Check(Near(circle.DistanceAt(new Vector2(0, 0), 0), -2f), "circle centre is 2 deep");

var capsule = Area.Capsule(new Vector2(0, 0), new Vector2(10, 0), 1f);
Check(Near(capsule.DistanceAt(new Vector2(5, 3), 0), 2f), "capsule side");
Check(Near(capsule.DistanceAt(new Vector2(13, 0), 0), 2f), "capsule end");
Check(capsule.Reaches(new Vector2(5, 0.5f), 0), "capsule inside");

var ring = Area.Ring(new Vector2(0, 0), 4f, 6f);
Check(Near(ring.DistanceAt(new Vector2(0, 0), 0), 4f), "ring centre is outside by its inner radius");
Check(Near(ring.DistanceAt(new Vector2(5, 0), 0), -1f), "ring band middle");
Check(Near(ring.DistanceAt(new Vector2(8, 0), 0), 2f), "ring beyond");

var cone = Area.Cone(new Vector2(0, 0), new Vector2(10, 0), MathF.PI / 6);
Check(cone.Reaches(new Vector2(5, 1), 0), "cone inside");
Check(!cone.Reaches(new Vector2(5, 5), 0), "cone outside the angle");
Check(!cone.Reaches(new Vector2(12, 0), 0), "cone beyond its reach");
Check(Near(cone.DistanceAt(new Vector2(12, 0), 0), 2f), "cone beyond its reach by 2");
Check(!cone.Reaches(new Vector2(-3, 0), 0), "cone behind");

var shot = Hazard.Timed(Area.Circle(new Vector2(0, 0), 0f, new Vector2(10, 0)), 0f, 1f, 5f, Endangers.Agent);
Check(shot.Reaches(new Vector2(5, 0), 0.5f, 0.5f), "shot reaches a point on its path in time");
Check(!shot.Reaches(new Vector2(15, 0), 1.5f, 0.5f), "shot gone after its life");
var lance = Hazard.Timed(Area.Capsule(new Vector2(0, 0), new Vector2(20, 0), 1f), 0.8f, 0.3f, 10f, Endangers.Both);
Check(!lance.Reaches(new Vector2(10, 0), 0.5f), "readied attack harmless before it lands");
Check(lance.Reaches(new Vector2(10, 0), 0.9f), "readied attack harms when it lands");

var s = new Situation();
s.Add(Hazard.Lasting(Area.Circle(new Vector2(0, 0), 2f), 6f, Endangers.Agent));
s.Add(Hazard.Lasting(Area.Circle(new Vector2(0, 0), 2f), 12f, Endangers.Allies, perSecond: true));
Check(Near(s.HarmAt(new Vector2(2.5f, 0), 0, 0.6f, Endangers.Agent), 6f), "situation: agent harm counts agent hazards only");
Check(Near(s.HarmAt(new Vector2(2.5f, 0), 0, 0.6f, Endangers.Allies), 12f), "situation: ally harm counts ally hazards only");
Check(Near(s.HarmAt(new Vector2(5, 0), 0, 0.6f, Endangers.Both), 0f), "situation: nothing reaches a point clear of it");
var zone = Zone.ReachAround(9f);
Check(zone.Area.Moved(new Vector2(100, 0)).Reaches(new Vector2(108, 0), 0), "a following zone moves with the agent");
// ── Path planning: paths follow the agent's movement, and hazards along them count.
var mover = new Mover { MaxSpeed = 20f, Acceleration = 80f, Deceleration = 60f, ControlRate = 0f };
var planner = new PathPlanner { Horizon = 1f, Steps = 10, Directions = 8, Strengths = new[] { 1f } };
planner.Build(new Motion { Position = Vector2.Zero }, mover);
Check(planner.Count == 9, "planner: 8 directions at one strength, and stopping");
Check(planner.End(0).X > 15f && planner.End(0).X < 20f && Near(planner.End(0).Y, 0f), $"planner: from rest, accelerating to top speed ({planner.End(0).X:0.0} units in 1 s)");
Check(Near(planner.End(8).X, 0f) && Near(planner.End(8).Y, 0f), "planner: stopping from rest stays put");
var danger = new Situation();
danger.Add(Hazard.Lasting(Area.Circle(new Vector2(8f, 0f), 2f), 10f, Endangers.Agent));
planner.Assess(danger, 0.5f, 5f, 0f);
Check(planner.HarmOf(0) >= 10f, "planner: the path through a hazard is harmed");
Check(Near(planner.HarmOf(4), 0f), "planner: the path away from it is not");
Check(planner.ClosenessOf(0) > planner.ClosenessOf(1) && planner.ClosenessOf(1) > 0f && Near(planner.ClosenessOf(2), 0f), "planner: closeness falls off sideways (45 degrees comes near, 90 never does)");
Check(Near(planner.ClosenessOf(4), 0f), "planner: and is nothing far away");
var drain = new Situation();
drain.Add(Hazard.Lasting(Area.Circle(new Vector2(6f, 0f), 2f), 12f, Endangers.Allies, perSecond: true));
planner.Assess(drain, 0.5f, 0f, 6f);
Check(planner.AllyDangerOf(0) > planner.AllyDangerOf(4), "planner: danger to allies is worse heading toward a drain");
Check(Near(planner.HarmOf(0), 0f), "planner: a hazard to allies does the agent no harm");
var slow = new PathPlanner { Horizon = 1f, Steps = 10, Directions = 4, Strengths = new[] { 1f }, IncludeStop = false };
slow.Build(new Motion { Position = Vector2.Zero, Velocity = new Vector2(20f, 0f), Control = new Vector2(1f, 0f) },
	new Mover { MaxSpeed = 20f, Acceleration = 80f, Deceleration = 60f, ControlRate = 6f });
Check(slow.Point(2, 1).X > 0f && slow.End(2).X < 0f, "planner: turning back takes time (still going forward after 0.2 s, back past the start by 1 s)");

// ── Decisions: weighted terms, the best taken, explained.
var decision = new Decision("move");
float[] near = { 0f, 2f, 5f };
float[] safe = { -1f, 0f, -3f };
int toward = decision.AddTerm("toward", o => near[o]);
int harm = decision.AddTerm("harm", o => safe[o]);
decision.SetWeight(toward, 1f);
decision.SetWeight(harm, 2f);
Check(decision.Choose(3) == 1, "decision: the best total wins (2 - 0 beats 5 - 6)");
decision.SetWeight(harm, 0.1f);
Check(decision.Choose(3) == 2, "decision: changing a weight changes the choice");
Check(decision.Explain(2).StartsWith("2 +4.70 (toward +5.00 harm -0.30)"), $"decision: explained ({decision.Explain(2)})");
decision.SetWeight(harm, 2f);
Check(decision.Choose(3, 10f, new Never()) == 1 && Near(decision.NoiseOf(1), 9.9f) && Near(decision.TotalOf(1), 11.9f),
	"decision: noise is added to each option's total, and kept");
Check(decision.Explain(1).Contains("noise +9.90"), $"decision: explained with its noise ({decision.Explain(1)})");
var shots = new Situation();
shots.Add(Hazard.Timed(Area.Circle(Vector2.Zero, 0f), 0f, 1f, 5f, Endangers.Agent));
shots.Add(Hazard.Lasting(Area.Circle(Vector2.Zero, 1f), 5f, Endangers.Agent));
shots.Overlook(0f, new Never());
Check(shots.Hazards[0].Overlooked && !shots.Hazards[1].Overlooked, "situation: a fleeting hazard can be overlooked, a lasting one can't");

Console.WriteLine(fails == 0 ? "ALL PASSED" : $"{fails} FAILED");
return fails;

/// <summary>Randomness that always rolls high: nothing is noticed by chance.</summary>
sealed class Never : IRandom
{
	public float Value() => 0.99f;
}
