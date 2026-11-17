using Godot;
using System.Collections.Generic;

/// <summary>
/// Root of the fight scene. Connects the pieces to each other, starts the fight at the gateway,
/// and keeps the fight-wide numbers: time, kills, fight experience, level and upgrades.
/// </summary>
public partial class Fight : Node3D
{
	public Vnp Vnp { get; private set; }
	public Gateway Gateway { get; private set; }
	public CameraRig Camera { get; private set; }
	public EnemySwarm Enemies { get; private set; }
	public DroneSwarm Drones { get; private set; }
	public Projectiles Projectiles { get; private set; }
	public Harvestables Harvestables { get; private set; }
	public PulseLaser PulseLaser { get; private set; }
	public VnpHarvester VnpHarvester { get; private set; }

	/// <summary>The second VNP Weapon, once the Orbital Shards upgrade is taken. Null until then.</summary>
	public OrbitalShards OrbitalShards { get; private set; }

	/// <summary>VNP Weapons equipped, out of Tuning.WeaponSlots.</summary>
	public int WeaponCount => 1 + (OrbitalShards != null ? 1 : 0);

	/// <summary>Seconds since the fight started.</summary>
	public double Elapsed { get; private set; }

	/// <summary>Minutes since the fight started. The difficulty ramp is measured in these.</summary>
	public float Minutes => (float)(Elapsed / 60.0);

	/// <summary>Enemies destroyed so far.</summary>
	public int Kills;

	/// <summary>Fight experience harvested so far.</summary>
	public float Experience { get; private set; }

	public int Level { get; private set; } = 1;

	/// <summary>Fight experience gathered since the last level up.</summary>
	public float ExperienceIntoLevel => Experience - _levelStartExperience;

	/// <summary>Fight experience the current level needs before the next level up.</summary>
	public float ExperienceForNextLevel => Tuning.ExperienceForFirstLevel + Tuning.ExperienceIncreasePerLevel * (Level - 1);

	/// <summary>Rerolls left for the rest of the fight.</summary>
	public int Rerolls = Tuning.FreeRerolls;

	/// <summary>Every fight upgrade taken, in order. The pause menu and result screen list these.</summary>
	public readonly List<string> TakenUpgrades = new();

	/// <summary>How far along the next temporary drone is, from 0 to 1.</summary>
	public float DroneBuildProgress => _harvestTowardDrone / Tuning.HarvestPerDrone;

	public enum Outcome { Win, Loss, Retreat }

	/// <summary>True once the fight has ended and the result screen is up.</summary>
	public bool IsOver { get; private set; }

	/// <summary>True while the VNP is on the gateway.</summary>
	public bool OnGateway { get; private set; }

	/// <summary>Seconds the VNP has stayed on the gateway without leaving.</summary>
	public float GatewayTime { get; private set; }

	/// <summary>True once the VNP has stayed on the gateway long enough to retreat.</summary>
	public bool CanRetreat => OnGateway && GatewayTime >= Tuning.RetreatHoldSeconds;

	/// <summary>The pause menu may open only during play, not over another menu or the result screen.</summary>
	public bool CanPause => !IsOver && !_levelUpMenu.IsOpen;

	/// <summary>Escalation steps reached: 0 before the time limit, 1 at it, 2 one step later, and so on.</summary>
	public int EscalationSteps => Elapsed < Tuning.TimeLimitSeconds
		? 0
		: (int)((Elapsed - Tuning.TimeLimitSeconds) / Tuning.EscalationStepSeconds) + 1;

	// Enemy strength now: the ramp during the fight, times escalation past the limit.
	public float EnemyHealthScale => (1f + Tuning.RampEnemyHealthPerMinute * Minutes) * (1f + Tuning.EscalationEnemyHealthPerStep * EscalationSteps);
	public float EnemyDamageScale => (1f + Tuning.RampEnemyDamagePerMinute * Minutes) * (1f + Tuning.EscalationEnemyDamagePerStep * EscalationSteps);
	public float EnemySpawnRateScale => (1f + Tuning.RampSpawnRatePerMinute * Minutes) * (1f + Tuning.EscalationSpawnRatePerStep * EscalationSteps);
	public float EnemyAggroScale => 1f + Tuning.EscalationAggroRangePerStep * EscalationSteps;

	private float _harvestTowardDrone;
	private float _levelStartExperience;
	private int _pendingLevelUps;
	private int _alertedSteps;
	private Label _debugLabel;
	private HealthBar _healthBar;
	private LevelUpMenu _levelUpMenu;
	private FightHud _fightHud;
	private ResultScreen _resultScreen;
	private double _debugRefresh;

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Fight;

		Vnp = GetNode<Vnp>("Vnp");
		Gateway = GetNode<Gateway>("Gateway");
		Camera = GetNode<CameraRig>("Camera");
		Enemies = GetNode<EnemySwarm>("Enemies");
		Drones = GetNode<DroneSwarm>("Drones");
		Projectiles = GetNode<Projectiles>("Projectiles");
		Harvestables = GetNode<Harvestables>("Harvestables");
		PulseLaser = GetNode<PulseLaser>("PulseLaser");
		VnpHarvester = GetNode<VnpHarvester>("VnpHarvester");
		_debugLabel = GetNode<Label>("Hud/DebugLabel");

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
		Drones.Fight = this;
		Drones.Add(Tuning.StartingDrones, Vnp.PlanePosition);
		Projectiles.Fight = this;
		PulseLaser.Fight = this;
		VnpHarvester.Fight = this;
		_healthBar = GetNode<HealthBar>("Hud/HealthBar");
		_healthBar.Fight = this;
		GetNode<ExperienceBar>("Hud/ExperienceBar").Fight = this;
		_levelUpMenu = GetNode<LevelUpMenu>("Hud/LevelUpMenu");
		_levelUpMenu.Fight = this;
		_fightHud = GetNode<FightHud>("Hud/FightHud");
		_fightHud.Fight = this;
		GetNode<PauseMenu>("Hud/PauseMenu").Fight = this;
		_resultScreen = GetNode<ResultScreen>("Hud/ResultScreen");
		_resultScreen.Fight = this;
	}

	/// <summary>Ends the fight: freezes it and shows the result screen.</summary>
	public void End(Outcome outcome)
	{
		if (IsOver)
			return;
		IsOver = true;

		// Nothing updates while frozen, so bring the readouts up to date first.
		RefreshDebugLabel();
		_healthBar.QueueRedraw();
		_fightHud.QueueRedraw();

		GetTree().Paused = true;
		_resultScreen.Show(outcome);
	}

	/// <summary>
	/// Called by harvest beams with the mass they drew out. Turns it into fight experience, and
	/// builds temporary drones until the cap is reached. At the cap it gives only fight experience.
	/// </summary>
	public void AddHarvest(float mass)
	{
		Experience += mass * Tuning.ExperiencePerMass;

		// Several level ups can arrive at once. Each gets its own offer, one after another.
		while (ExperienceIntoLevel >= ExperienceForNextLevel)
		{
			_levelStartExperience += ExperienceForNextLevel;
			Level++;
			_pendingLevelUps++;
		}

		if (Drones.CountWithBuilds >= Drones.Cap)
			return;

		_harvestTowardDrone += mass;
		while (_harvestTowardDrone >= Tuning.HarvestPerDrone && Drones.CountWithBuilds < Drones.Cap)
		{
			_harvestTowardDrone -= Tuning.HarvestPerDrone;
			Drones.Build();
		}
	}

	public void ApplyUpgrade(Upgrade upgrade)
	{
		upgrade.Apply(this);
		TakenUpgrades.Add(upgrade.Name);
	}

	/// <summary>Puts the Orbital Shards in the second weapon slot.</summary>
	public void EquipOrbitalShards()
	{
		if (OrbitalShards != null)
			return;
		OrbitalShards = new OrbitalShards { Name = "OrbitalShards", Fight = this };
		AddChild(OrbitalShards);
	}

	/// <summary>Builds up to <paramref name="count"/> drones at once, stopping at the cap.</summary>
	public void BuildDronesNow(int count)
	{
		for (int i = 0; i < count && Drones.CountWithBuilds < Drones.Cap; i++)
			Drones.Build();
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
		if (IsOver)
			return;

		// Past the time limit, each new escalation step flashes the screen.
		if (EscalationSteps > _alertedSteps)
		{
			_alertedSteps = EscalationSteps;
			_fightHud.ShowEscalationAlert();
		}

		// Retreat: stay on the gateway long enough and the option appears.
		OnGateway = Vnp.PlanePosition.DistanceTo(Gateway.PlanePosition) <= Tuning.GatewayRadius;
		GatewayTime = OnGateway ? GatewayTime + (float)delta : 0f;
		if (CanRetreat && Input.IsActionJustPressed("menu_select"))
		{
			End(Outcome.Retreat);
			return;
		}

		if (Kills >= Tuning.KillTarget)
		{
			End(Outcome.Win);
			return;
		}

		// Offer the next waiting level up. The offer pauses the fight until a choice is made.
		if (_pendingLevelUps > 0 && !_levelUpMenu.IsOpen && !Vnp.IsDestroyed)
		{
			_pendingLevelUps--;
			_levelUpMenu.Open(Level - _pendingLevelUps);
		}

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
		string nextDrone = Drones.Count >= Drones.Cap ? "at cap" : $"next {DroneBuildProgress * 100f:0}%";
		_debugLabel.Text =
			$"{Engine.GetFramesPerSecond():0} fps    " +
			$"time {seconds / 60}:{seconds % 60:00}    " +
			$"health {Mathf.Ceil(Vnp.Health):0}/{Vnp.MaxHealth:0}    " +
			$"level {Level}    " +
			$"xp {Experience:0}    " +
			$"drones {Drones.Count}/{Drones.Cap} ({nextDrone})    " +
			$"kills {Kills}    " +
			$"enemies {Enemies.Count}    " +
			$"asteroids {Harvestables.AsteroidCount}    " +
			$"debris {Harvestables.DebrisCount}    " +
			$"gateway {Vnp.PlanePosition.DistanceTo(Gateway.PlanePosition):0} away    " +
			$"camera: {cameraMode} (F1)";
	}

	private void OnVnpDestroyed() => End(Outcome.Loss);
}
