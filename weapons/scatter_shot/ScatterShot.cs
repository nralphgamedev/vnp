using Godot;

/// <summary>
/// A VNP Weapon that fires a cone of small projectiles toward the nearest enemy in range. Each projectile
/// deals the weapon's damage to the first enemy it hits. More projectiles from upgrades widen the cone a
/// little, up to a limit, and fill it more densely.
/// </summary>
public partial class ScatterShot : VnpWeapon
{
	private ScatterShotDefinition Scatter => (ScatterShotDefinition)Definition;

	/// <summary>Damage compared with the start of the fight, so damage upgrades show as bigger, brighter shots.</summary>
	private float Power => Mathf.Sqrt(Mathf.Max(0.01f, Damage / Scatter.Damage));

	private float _cooldownLeft;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Weapons;
	}

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		_cooldownLeft = Mathf.Max(0f, _cooldownLeft - (float)delta);
		if (_cooldownLeft > 0f || Fight.Vnp.IsDestroyed)
			return;

		Vector2 from = Fight.Vnp.PlanePosition;
		int target = Fight.Enemies.FindNearest(from, Scatter.Range * Range);
		if (target < 0)
			return; // Nothing in range. Stay ready, and fire the moment something is.

		Vector2 toTarget = Fight.Enemies.PositionOf(target) - from;
		if (toTarget.LengthSquared() < 0.0001f)
			return;
		Fire(from, toTarget.Normalized());
		_cooldownLeft = Cooldown;
	}

	/// <summary>Fires every projectile, spread evenly across the cone around <paramref name="aim"/>.</summary>
	private void Fire(Vector2 from, Vector2 aim)
	{
		int count = Mathf.Max(1, Scatter.ProjectileCount + ExtraCount);
		float cone = Mathf.Min(Scatter.MaxConeDegrees, Scatter.ConeDegrees + ExtraCount * Scatter.ConeWidenPerProjectile);
		float coneRadians = Mathf.DegToRad(cone);
		// The pellets fly a little past the range, so a longer range also carries them further.
		float lifetime = Scatter.Range * Range * 1.2f / Scatter.ProjectileSpeed;
		Color color = Scatter.Color.Lerp(Colors.White, Mathf.Clamp((Power - 1f) * 0.4f, 0f, 0.5f));

		for (int i = 0; i < count; i++)
		{
			float spread = count == 1 ? 0f : coneRadians * ((float)i / (count - 1) - 0.5f);
			Fight.Projectiles.Fire(from, aim.Rotated(spread), Damage, Scatter.ProjectileSpeed, lifetime,
				fromEnemy: false, color, Scatter.ProjectileSize * Power);
		}

		Fight.Effects.ScatterFlash(from, aim, Scatter.Color, coneRadians, Power);
		Fight.Sounds.Play(Sound.ScatterShotFire);
	}
}
