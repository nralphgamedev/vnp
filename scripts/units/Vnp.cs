using Godot;
using System;

/// <summary>The player's ship. Moves on the plane from WASD or the left stick, and has health.</summary>
public partial class Vnp : Node3D
{
	/// <summary>Position on the gameplay plane.</summary>
	public Vector2 PlanePosition { get; private set; }

	/// <summary>Current velocity on the gameplay plane, in units per second.</summary>
	public Vector2 Velocity { get; private set; }

	public float MaxHealth { get; private set; } = Tuning.Vnp.MaxHealth;
	public float Health { get; private set; } = Tuning.Vnp.MaxHealth;
	public bool IsDestroyed { get; private set; }

	/// <summary>Top speed. Starts from Tuning; upgrades raise it for this fight.</summary>
	public float MoveSpeed = Tuning.Vnp.MoveSpeed;

	/// <summary>Raised once, when health reaches zero.</summary>
	public event Action Destroyed;

	private Node3D _model;
	private StandardMaterial3D _hullMaterial;
	private Color _hullGlow;
	private float _hullGlowStrength;
	private float _flash;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Vnp;
		PlanePosition = PlaneMath.ToPlane(Position);
		BuildModel();
	}

	/// <summary>Places the VNP at a point on the plane and stops it.</summary>
	public void PlaceAt(Vector2 planePosition)
	{
		PlanePosition = planePosition;
		Velocity = Vector2.Zero;
		Position = PlaneMath.ToWorld(PlanePosition);
	}

	/// <summary>Raises maximum health and heals by the same amount.</summary>
	public void RaiseMaxHealth(float amount)
	{
		MaxHealth += amount;
		Health = Mathf.Min(MaxHealth, Health + amount);
	}

	public void TakeDamage(float amount)
	{
		if (IsDestroyed)
			return;

		Health = Mathf.Max(0f, Health - amount);
		_flash = Tuning.Look.HitFlashTime;
		if (Health <= 0f)
		{
			IsDestroyed = true;
			_model.Visible = false;
			Destroyed?.Invoke();
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		UpdateHitFlash(dt);
		if (IsDestroyed)
			return;

		// GetVector never returns more than length 1, so diagonals are not faster,
		// and a partly tilted stick gives a partial speed.
		Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down", Tuning.Vnp.StickDeadzone);
		Vector2 wantedVelocity = input * MoveSpeed;
		float rate = input == Vector2.Zero ? Tuning.Vnp.Deceleration : Tuning.Vnp.Acceleration;

		Velocity = Velocity.MoveToward(wantedVelocity, rate * dt);
		PlanePosition += Velocity * dt;
		Position = PlaneMath.ToWorld(PlanePosition);

		// Turn the model toward the direction of travel. Holds its facing when nearly stopped.
		if (Velocity.LengthSquared() > 0.25f)
		{
			float wantedYaw = PlaneMath.YawFacing(Velocity);
			float step = Mathf.DegToRad(Tuning.Vnp.TurnSpeed) * dt;
			_model.Rotation = new Vector3(0f, Mathf.RotateToward(_model.Rotation.Y, wantedYaw, step), 0f);
		}
	}

	/// <summary>The hull glows red for a moment after a hit.</summary>
	private void UpdateHitFlash(float dt)
	{
		_flash = Mathf.Max(0f, _flash - dt);
		float strength = _flash / Tuning.Look.HitFlashTime;
		_hullMaterial.Emission = _hullGlow.Lerp(new Color(1f, 0.15f, 0.1f), strength);
		_hullMaterial.EmissionEnergyMultiplier = Mathf.Lerp(_hullGlowStrength, 2.5f, strength);
	}

	/// <summary>
	/// Puts the placeholder model under the VNP. The hull gets its own copy of its material, so the hit
	/// flash can change it.
	/// </summary>
	private void BuildModel()
	{
		_model = Placeholders.Model("vnp").Instantiate<Node3D>();
		_model.Name = "Model";
		_model.Scale = Vector3.One * Tuning.Vnp.Size;
		AddChild(_model);

		var hull = (StandardMaterial3D)Placeholders.Material("vnp_hull");
		_hullMaterial = (StandardMaterial3D)hull.Duplicate();
		_hullGlow = hull.Emission;
		_hullGlowStrength = hull.EmissionEnergyMultiplier;
		foreach (Node node in _model.FindChildren("*", "MeshInstance3D", true, false))
		{
			var shape = (MeshInstance3D)node;
			if (shape.MaterialOverride == hull)
				shape.MaterialOverride = _hullMaterial;
		}
	}
}
