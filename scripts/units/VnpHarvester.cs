using Godot;

/// <summary>
/// The VNP's harvest beam. Whenever an asteroid or piece of debris is within reach, it locks on to
/// the nearest one and draws mass out of it until it is used up or out of reach. It works while
/// the VNP moves and fights.
/// </summary>
public partial class VnpHarvester : Node3D
{
	private const float BeamHeight = 0.25f;

	public Fight Fight;

	/// <summary>Mass drawn out per second. Belongs to the beam, so upgrades can change it.</summary>
	public float Rate = Tuning.Harvesting.VnpRate;

	private int _targetId;

	/// <summary>Whether the beam is on something this frame.</summary>
	public bool Beaming => _targetId != 0;
	private readonly int[] _nearby = new int[256];
	private MeshInstance3D _beam;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Weapons;

		// The beam is the colour of fight experience.
		var material = (StandardMaterial3D)Placeholders.Material("harvest_beam").Duplicate();
		material.AlbedoColor = Tuning.Look.ExperienceColor;
		// A unit box, stretched between the VNP and its target every frame.
		_beam = new MeshInstance3D
		{
			Mesh = Placeholders.Mesh("unit_box"),
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Visible = false,
		};
		AddChild(_beam);
	}

	public override void _Process(double delta)
	{
		_beam.Visible = false;
		if (Fight == null || Fight.Vnp.IsDestroyed)
			return;

		Harvestables harvestables = Fight.Harvestables;
		Vector2 from = Fight.Vnp.PlanePosition;
		int count = harvestables.CollectNear(from, harvestables.BeamRange, _nearby);

		// Stay on the current target while it lasts and stays in reach. Otherwise take the nearest.
		int target = -1;
		float nearest = float.MaxValue;
		for (int c = 0; c < count; c++)
		{
			int i = _nearby[c];
			if (harvestables.IdOf(i) == _targetId)
			{
				target = i;
				break;
			}
			float edge = harvestables.EdgeDistance(i, from);
			if (edge < nearest)
			{
				nearest = edge;
				target = i;
			}
		}

		if (target < 0)
		{
			_targetId = 0;
			return;
		}

		_targetId = harvestables.IdOf(target);
		Vector2 targetPosition = harvestables.PositionOf(target);
		float targetRadius = harvestables.RadiusOf(target);
		harvestables.Harvest(target, Rate * (float)delta);
		ShowBeam(from, targetPosition, harvestables.HeightOf(target));

		Fight.Sounds.Hold(Sound.HarvestLoop);

		// Particles flow from the material's edge back to the VNP.
		Vector2 toVnp = (from - targetPosition).Normalized();
		Fight.Effects.HarvestFlow(targetPosition + toVnp * targetRadius * 0.6f, from, Tuning.Look.ExperienceColor,
			Tuning.Feedback.VnpHarvestParticles, (float)delta);
	}

	/// <summary>Draws the beam from inside the VNP down into the middle of the material, so its end is
	/// hidden inside whatever it is harvesting.</summary>
	private void ShowBeam(Vector2 from, Vector2 to, float targetHeight)
	{
		if (from.DistanceTo(to) < 0.01f)
			return;

		// A slight shimmer, so a beam that is working looks alive.
		float width = 0.22f + 0.06f * Mathf.Sin((float)Fight.Elapsed * 25f);

		_beam.Transform = PlaneMath.Bar(PlaneMath.ToWorld(from, BeamHeight), PlaneMath.ToWorld(to, targetHeight), width, 0.1f);
		_beam.Visible = true;
	}
}
