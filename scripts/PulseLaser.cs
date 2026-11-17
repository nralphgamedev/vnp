using Godot;

/// <summary>
/// The VNP's starting weapon. Whenever it is off cooldown and an enemy is in range,
/// it fires a straight beam at the nearest enemy and damages that enemy only.
/// </summary>
public partial class PulseLaser : Node3D
{
	private const float BeamHeight = 0.3f;

	public Fight Fight;

	// These belong to the weapon, so upgrades can change them without touching the VNP.
	public float Damage = Tuning.PulseLaserDamage;
	public float Cooldown = Tuning.PulseLaserCooldown;
	public float Range = Tuning.PulseLaserRange;

	private float _cooldownLeft;
	private float _beamTimeLeft;
	private MeshInstance3D _beam;
	private StandardMaterial3D _beamMaterial;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Weapons;

		_beamMaterial = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = Tuning.PulseLaserColor,
		};
		// A unit box, stretched along its length each time the laser fires.
		_beam = Placeholder.AddShape(this, new BoxMesh { Size = Vector3.One }, _beamMaterial);
		_beam.Visible = false;
	}

	public override void _Process(double delta)
	{
		if (Fight == null)
			return;

		float dt = (float)delta;
		FadeBeam(dt);

		_cooldownLeft = Mathf.Max(0f, _cooldownLeft - dt);
		if (_cooldownLeft > 0f || Fight.Vnp.IsDestroyed)
			return;

		Vector2 from = Fight.Vnp.PlanePosition;
		int target = Fight.Enemies.FindNearest(from, Range);
		if (target < 0)
			return; // Nothing in range. Stay ready, and fire the moment something is.

		Vector2 targetPosition = Fight.Enemies.PositionOf(target);
		Fight.Enemies.Damage(target, Damage);
		ShowBeam(from, targetPosition);
		_cooldownLeft = Cooldown;
	}

	private void ShowBeam(Vector2 from, Vector2 to)
	{
		Vector2 along = to - from;
		float length = along.Length();
		if (length < 0.01f)
			return;

		_beam.Position = PlaneMath.ToWorld((from + to) * 0.5f, BeamHeight);
		_beam.Rotation = new Vector3(0f, PlaneMath.YawFacing(along), 0f);
		_beam.Scale = new Vector3(Tuning.PulseLaserBeamWidth, 0.15f, length);
		_beam.Visible = true;
		_beamTimeLeft = Tuning.PulseLaserBeamTime;
		_beamMaterial.AlbedoColor = Tuning.PulseLaserColor;
	}

	private void FadeBeam(float dt)
	{
		if (!_beam.Visible)
			return;

		_beamTimeLeft -= dt;
		if (_beamTimeLeft <= 0f)
		{
			_beam.Visible = false;
			return;
		}
		_beamMaterial.AlbedoColor = new Color(Tuning.PulseLaserColor, _beamTimeLeft / Tuning.PulseLaserBeamTime);
	}
}
