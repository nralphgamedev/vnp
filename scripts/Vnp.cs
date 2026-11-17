using Godot;
using System;

/// <summary>The player's ship. Moves on the plane from WASD or the left stick, and has health.</summary>
public partial class Vnp : Node3D
{
	/// <summary>Position on the gameplay plane.</summary>
	public Vector2 PlanePosition { get; private set; }

	/// <summary>Current velocity on the gameplay plane, in units per second.</summary>
	public Vector2 Velocity { get; private set; }

	public float MaxHealth { get; private set; } = Tuning.VnpMaxHealth;
	public float Health { get; private set; } = Tuning.VnpMaxHealth;
	public bool IsDestroyed { get; private set; }

	/// <summary>Top speed. Starts from Tuning; upgrades raise it for this fight.</summary>
	public float MoveSpeed = Tuning.VnpMoveSpeed;

	/// <summary>Raised once, when health reaches zero.</summary>
	public event Action Destroyed;

	private Node3D _model;
	private StandardMaterial3D _hullMaterial;
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
		_flash = Tuning.HitFlashTime;
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
		Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down", Tuning.StickDeadzone);
		Vector2 wantedVelocity = input * MoveSpeed;
		float rate = input == Vector2.Zero ? Tuning.VnpDeceleration : Tuning.VnpAcceleration;

		Velocity = Velocity.MoveToward(wantedVelocity, rate * dt);
		PlanePosition += Velocity * dt;
		Position = PlaneMath.ToWorld(PlanePosition);

		// Turn the model toward the direction of travel. Holds its facing when nearly stopped.
		if (Velocity.LengthSquared() > 0.25f)
		{
			float wantedYaw = PlaneMath.YawFacing(Velocity);
			float step = Mathf.DegToRad(Tuning.VnpTurnSpeed) * dt;
			_model.Rotation = new Vector3(0f, Mathf.RotateToward(_model.Rotation.Y, wantedYaw, step), 0f);
		}
	}

	/// <summary>The hull glows red for a moment after a hit.</summary>
	private void UpdateHitFlash(float dt)
	{
		_flash = Mathf.Max(0f, _flash - dt);
		float strength = _flash / Tuning.HitFlashTime;
		_hullMaterial.Emission = Tuning.VnpHullColor.Lerp(new Color(1f, 0.15f, 0.1f), strength);
		_hullMaterial.EmissionEnergyMultiplier = Mathf.Lerp(0.1f, 2.5f, strength);
	}

	/// <summary>Placeholder: an arrowhead hull with two side pods and a glowing core.</summary>
	private void BuildModel()
	{
		_model = new Node3D { Name = "Model", Scale = Vector3.One * Tuning.VnpSize };
		AddChild(_model);

		_hullMaterial = Placeholder.Material(Tuning.VnpHullColor);
		StandardMaterial3D core = Placeholder.Material(Tuning.VnpCoreColor, glow: 0.8f);

		// A prism stands upright by default, so tip it forward to make the point face -Z.
		Placeholder.AddShape(_model, new PrismMesh { Size = new Vector3(2.2f, 3.0f, 0.8f) }, _hullMaterial,
			rotationDegrees: new Vector3(-90f, 0f, 0f));

		var pod = new BoxMesh { Size = new Vector3(0.5f, 0.6f, 1.5f) };
		Placeholder.AddShape(_model, pod, _hullMaterial, new Vector3(-1.25f, 0f, 1.0f));
		Placeholder.AddShape(_model, pod, _hullMaterial, new Vector3(1.25f, 0f, 1.0f));

		Placeholder.AddShape(_model, new SphereMesh { Radius = 0.45f, Height = 0.9f }, core,
			new Vector3(0f, 0.45f, 0.55f));
	}
}
