using Godot;

/// <summary>
/// A weapon that fires a cone of small projectiles toward the nearest enemy in range. Extra projectiles
/// from upgrades spread over a slightly wider cone, up to a limit, so more of them fill it more densely.
/// Damage is per projectile.
/// </summary>
[GlobalClass]
public partial class ScatterShotDefinition : WeaponDefinition
{
	/// <summary>Fires when an enemy is this close.</summary>
	[Export] public float Range;

	/// <summary>Projectiles in each shot.</summary>
	[Export] public int ProjectileCount;

	/// <summary>Width of the cone, in degrees, at the starting projectile count.</summary>
	[Export] public float ConeDegrees;

	/// <summary>The cone widens by this many degrees for each extra projectile.</summary>
	[Export] public float ConeWidenPerProjectile;

	/// <summary>The cone never gets wider than this, in degrees; more projectiles then fill it more densely.</summary>
	[Export] public float MaxConeDegrees;

	/// <summary>Speed of a projectile. Each flies a little past the range before it fizzles out.</summary>
	[Export] public float ProjectileSpeed;

	/// <summary>Scale of a projectile. Drone projectiles are 0.6.</summary>
	[Export] public float ProjectileSize;
}
