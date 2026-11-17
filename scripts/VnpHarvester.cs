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
	public float Rate = Tuning.VnpHarvestRate;

	private int _targetId;
	private readonly int[] _nearby = new int[256];
	private MeshInstance3D _beam;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Weapons;

		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = Tuning.ExperienceColor,
		};
		// A unit box, stretched between the VNP and its target every frame.
		_beam = Placeholder.AddShape(this, new BoxMesh { Size = Vector3.One }, material);
		_beam.Visible = false;
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
		harvestables.Harvest(target, Rate * (float)delta);
		ShowBeam(from, harvestables.PositionOf(target), harvestables.RadiusOf(target));
	}

	private void ShowBeam(Vector2 from, Vector2 to, float targetRadius)
	{
		Vector2 along = to - from;
		float distance = along.Length();
		if (distance < 0.01f)
			return;

		// End the beam just inside the target's edge.
		Vector2 end = from + along / distance * Mathf.Max(0f, distance - targetRadius * 0.5f);
		float length = from.DistanceTo(end);
		if (length < 0.01f)
			return;

		// A slight shimmer, so a beam that is working looks alive.
		float width = 0.22f + 0.06f * Mathf.Sin((float)Fight.Elapsed * 25f);

		_beam.Position = PlaneMath.ToWorld((from + end) * 0.5f, BeamHeight);
		_beam.Rotation = new Vector3(0f, PlaneMath.YawFacing(along), 0f);
		_beam.Scale = new Vector3(width, 0.1f, length);
		_beam.Visible = true;
	}
}
