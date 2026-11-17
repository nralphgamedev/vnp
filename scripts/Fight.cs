using Godot;

/// <summary>
/// Root of the fight scene. Connects the pieces to each other, starts the fight at the gateway,
/// and keeps the fight-wide numbers: time, kills and fight experience.
/// </summary>
public partial class Fight : Node3D
{
	public Vnp Vnp { get; private set; }
	public Gateway Gateway { get; private set; }
	public CameraRig Camera { get; private set; }
	public EnemySwarm Enemies { get; private set; }
	public ProbeSwarm Probes { get; private set; }
	public Projectiles Projectiles { get; private set; }
	public Harvestables Harvestables { get; private set; }

	/// <summary>Seconds since the fight started.</summary>
	public double Elapsed { get; private set; }

	/// <summary>Minutes since the fight started. The difficulty ramp is measured in these.</summary>
	public float Minutes => (float)(Elapsed / 60.0);

	/// <summary>Enemies destroyed so far.</summary>
	public int Kills;

	/// <summary>Fight experience harvested so far.</summary>
	public float Experience { get; private set; }

	/// <summary>How far along the next temporary probe is, from 0 to 1.</summary>
	public float ProbeBuildProgress => _harvestTowardProbe / Tuning.HarvestPerProbe;

	private float _harvestTowardProbe;
	private Label _debugLabel;
	private Label _message;
	private HealthBar _healthBar;
	private double _debugRefresh;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Fight;

		Vnp = GetNode<Vnp>("Vnp");
		Gateway = GetNode<Gateway>("Gateway");
		Camera = GetNode<CameraRig>("Camera");
		Enemies = GetNode<EnemySwarm>("Enemies");
		Probes = GetNode<ProbeSwarm>("Probes");
		Projectiles = GetNode<Projectiles>("Projectiles");
		Harvestables = GetNode<Harvestables>("Harvestables");
		_debugLabel = GetNode<Label>("Hud/DebugLabel");
		_message = GetNode<Label>("Hud/Message");

		// The fight starts at the gateway.
		Vnp.PlaceAt(Gateway.PlanePosition);
		Vnp.Destroyed += OnVnpDestroyed;

		Camera.Target = Vnp;
		Camera.SnapToTarget();
		GetNode<GroundGrid>("Ground").Camera = Camera;

		var indicator = GetNode<GatewayIndicator>("Hud/GatewayIndicator");
		indicator.Camera = Camera;
		indicator.Gateway = Gateway;

		Harvestables.Fight = this;
		Enemies.Fight = this;
		Probes.Fight = this;
		Probes.Add(Tuning.StartingProbes, Vnp.PlanePosition);
		Projectiles.Fight = this;
		GetNode<PulseLaser>("PulseLaser").Fight = this;
		GetNode<VnpHarvester>("VnpHarvester").Fight = this;
		_healthBar = GetNode<HealthBar>("Hud/HealthBar");
		_healthBar.Fight = this;
	}

	/// <summary>
	/// Called by harvest beams with the mass they drew out. Turns it into fight experience, and
	/// builds temporary probes until the cap is reached. At the cap it gives only fight experience.
	/// </summary>
	public void AddHarvest(float mass)
	{
		Experience += mass * Tuning.ExperiencePerMass;

		if (Probes.CountWithBuilds >= Probes.Cap)
			return;

		_harvestTowardProbe += mass;
		while (_harvestTowardProbe >= Tuning.HarvestPerProbe && Probes.CountWithBuilds < Probes.Cap)
		{
			_harvestTowardProbe -= Tuning.HarvestPerProbe;
			Probes.Build();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Playtest shortcut: switch between the perspective and orthographic cameras.
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F1 })
			Tuning.CameraOrthographic = !Tuning.CameraOrthographic;
	}

	public override void _Process(double delta)
	{
		Elapsed += delta;

		_debugRefresh -= delta;
		if (_debugRefresh > 0)
			return;
		_debugRefresh = 0.25;
		RefreshDebugLabel();
	}

	private void RefreshDebugLabel()
	{
		int seconds = (int)Elapsed;
		string cameraMode = Tuning.CameraOrthographic ? "orthographic" : "perspective";
		string nextProbe = Probes.Count >= Probes.Cap ? "at cap" : $"next {ProbeBuildProgress * 100f:0}%";
		_debugLabel.Text =
			$"{Engine.GetFramesPerSecond():0} fps    " +
			$"time {seconds / 60}:{seconds % 60:00}    " +
			$"health {Mathf.Ceil(Vnp.Health):0}/{Vnp.MaxHealth:0}    " +
			$"xp {Experience:0}    " +
			$"probes {Probes.Count}/{Probes.Cap} ({nextProbe})    " +
			$"kills {Kills}    " +
			$"enemies {Enemies.Count}    " +
			$"asteroids {Harvestables.AsteroidCount}    " +
			$"debris {Harvestables.DebrisCount}    " +
			$"gateway {Vnp.PlanePosition.DistanceTo(Gateway.PlanePosition):0} away    " +
			$"camera: {cameraMode} (F1)";
	}

	/// <summary>
	/// Stand-in until the result screen exists (stage 6): freeze the fight, say what happened,
	/// then start a new fight.
	/// </summary>
	private async void OnVnpDestroyed()
	{
		_message.Text = "VNP destroyed. Restarting...";
		_message.Visible = true;

		// Nothing updates while frozen, so bring the readouts up to date first.
		RefreshDebugLabel();
		_healthBar.QueueRedraw();

		SceneTree tree = GetTree();
		tree.Paused = true;
		await ToSignal(tree.CreateTimer(2.0, processAlways: true), SceneTreeTimer.SignalName.Timeout);
		tree.Paused = false;
		tree.ReloadCurrentScene();
	}
}
