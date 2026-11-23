using Godot;

/// <summary>
/// Converts between Godot's vectors and the plain .NET vectors that Arbiter, the pilots' decision library, uses
/// (arbiter/README.md). Arbiter knows nothing of Godot, so the game converts at the boundary.
/// </summary>
public static class ArbiterBridge
{
	public static System.Numerics.Vector2 ToArbiter(this Vector2 v) => new(v.X, v.Y);

	public static Vector2 ToGodot(this System.Numerics.Vector2 v) => new(v.X, v.Y);
}

/// <summary>
/// What VNP's enemies do, as the tags on the bodies they declare to Arbiter (EnemyBehavior.DeclareThreats). Arbiter
/// passes tags through without reading them; VNP's own scoring terms read them.
/// </summary>
public static class ThreatTags
{
	/// <summary>Damages the VNP by touching it, as every Grey Goo type does.</summary>
	public const ulong HarmsByTouch = 1;

	/// <summary>Drains and absorbs drones that touch it, as the basic goo does.</summary>
	public const ulong DrainsDrones = 2;

	/// <summary>Fires shots, as the basic enemy does.</summary>
	public const ulong Shoots = 4;

	/// <summary>Launches itself along a line, as the Lancer does.</summary>
	public const ulong Launches = 8;
}
