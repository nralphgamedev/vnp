using Godot;

/// <summary>
/// The VNP's starting weapon. Whenever it is off cooldown and an enemy is in range,
/// it fires a straight beam at the nearest enemy and damages that enemy only.
/// </summary>
public partial class PulseLaser : VnpWeapon
{
	private const float BeamHeight = 0.3f;

	private PulseLaserDefinition Laser => (PulseLaserDefinition)Definition;

	public override float Reach => Laser.Range * Range;

	/// <summary>It hits what comes within its reach around the VNP.</summary>
	public override void DeclareZones(Arbiter.Situation situation, int source) => DeclareReach(situation, source);

	/// <summary>How much damage upgrades show: the beam and its flashes grow with damage, gently.</summary>
	private float Power => Mathf.Sqrt(Mathf.Max(0.01f, Damage / Laser.Damage));

	private float _cooldownLeft;
	private float _beamTimeLeft;
	private MeshInstance3D _beam;
	private StandardMaterial3D _beamMaterial;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Weapons;

		// Its own copy of the material, because the beam fades by changing its colour.
		_beamMaterial = (StandardMaterial3D)Placeholders.Material("pulse_laser_beam").Duplicate();
		_beamMaterial.AlbedoColor = Laser.Color;
		// A unit box, stretched along its length each time the laser fires.
		_beam = new MeshInstance3D
		{
			Mesh = Placeholders.Mesh("unit_box"),
			MaterialOverride = _beamMaterial,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Visible = false,
		};
		AddChild(_beam);
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
		int target = Fight.Enemies.FindNearest(from, Laser.Range);
		if (target < 0)
			return; // Nothing in range. Stay ready, and fire the moment something is.

		Vector2 targetPosition = Fight.Enemies.PositionOf(target);
		Fight.Enemies.Damage(target, Damage, Source);
		ShowBeam(from, targetPosition);
		Fight.Effects.MuzzleFlash(from, targetPosition - from, Laser.Color, Power);
		Fight.Sounds.Play(Sound.PulseLaserFire);
		Fight.Effects.Impact(targetPosition, Laser.Color, 1.2f * Power);
		_cooldownLeft = Cooldown;
	}

#if RECORDING
	// The beam last shown, while it is still visible, for CollectPlaced.
	private Vector2 _beamFrom, _beamTo;

	/// <summary>Its beam, while it shows after a shot.</summary>
	public override void CollectPlaced(System.Collections.Generic.List<PlacedObject> placed)
	{
		if (_beam != null && _beam.Visible)
			placed.Add(PlacedObject.Line(_beamFrom, _beamTo));
	}
#endif

	private void ShowBeam(Vector2 from, Vector2 to)
	{
#if RECORDING
		_beamFrom = from;
		_beamTo = to;
#endif
		Vector2 along = to - from;
		float length = along.Length();
		if (length < 0.01f)
			return;

		_beam.Position = PlaneMath.ToWorld((from + to) * 0.5f, BeamHeight);
		_beam.Rotation = new Vector3(0f, PlaneMath.YawFacing(along), 0f);
		_beam.Scale = new Vector3(Laser.BeamWidth * Power, 0.15f, length);
		_beam.Visible = true;
		_beamTimeLeft = Laser.BeamTime;
		_beamMaterial.AlbedoColor = Laser.Color;
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
		_beamMaterial.AlbedoColor = new Color(Laser.Color, _beamTimeLeft / Laser.BeamTime);
	}
}
