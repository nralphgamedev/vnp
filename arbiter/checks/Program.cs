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
Console.WriteLine(fails == 0 ? "ALL PASSED" : $"{fails} FAILED");
return fails;
