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
	public VnpHarvester VnpHarvester { get; private set; }
	public Effects Effects { get; private set; }
	public DamageNumbers DamageNumbers { get; private set; }
	public Sounds Sounds { get; private set; }

	/// <summary>The VNP Weapons equipped, in slot order.</summary>
	public IReadOnlyList<VnpWeapon> Weapons => _weapons;

	/// <summary>VNP Weapons equipped, out of Tuning.Vnp.WeaponSlots.</summary>
	public int WeaponCount => _weapons.Count;

	/// <summary>The seed every random choice in this fight comes from. Shown on the result screen.</summary>
	public int Seed { get; private set; }

	/// <summary>
	/// The seed for the next fight to start, for repeating a fight; null picks one at random. Used once, then
	/// cleared. Set it before the fight scene is added to the tree.
	/// </summary>
	public static int? NextSeed;

	/// <summary>What has happened in this fight so far: damage by source, drones lost, kills, level-ups.</summary>
	public readonly FightStats Stats = new();

	/// <summary>Seconds since the fight started.</summary>
	public double Elapsed { get; private set; }

	/// <summary>Minutes since the fight started. The difficulty ramp is measured in these.</summary>
	public float Minutes => (float)(Elapsed / 60.0);

	/// <summary>Enemies destroyed so far.</summary>
	public int Kills;

	/// <summary>Fight experience harvested so far.</summary>
	/// <remarks>Double precision, so it stays exact through thousands of small additions.</remarks>
	public double Experience { get; private set; }

	public int Level { get; private set; } = 1;

	/// <summary>Fight experience gathered since the last level up.</summary>
	public float ExperienceIntoLevel => (float)(Experience - _levelStartExperience);

	/// <summary>Fight experience the current level needs before the next level up.</summary>
	public float ExperienceForNextLevel => Tuning.Levels.ExperienceForFirstLevel + Tuning.Levels.ExperienceIncreasePerLevel * (Level - 1);

	/// <summary>Rerolls left for the rest of the fight.</summary>
	public int Rerolls = Tuning.Levels.FreeRerolls;

	/// <summary>Every fight upgrade taken, in order. The pause menu and result screen list these.</summary>
	public readonly List<UpgradeDefinition> TakenUpgrades = new();

	/// <summary>How far along the next temporary drone is, from 0 to 1.</summary>
	public float DroneBuildProgress => _harvestTowardDrone / Tuning.Harvesting.MassPerDrone;

	public enum Outcome { Win, Loss, Retreat }

	/// <summary>How the fight last ended. Meaningful once it has ended at least once.</summary>
	public Outcome LastOutcome { get; private set; }

	/// <summary>
	/// The pilot flying the primary VNP in place of the player, or null when the player flies it. Set by the
	/// scenario harness and batches. The level-up menu asks it to choose.
	/// </summary>
	public Pilot Pilot;

	/// <summary>True once the fight has ended and the result screen is up.</summary>
	public bool IsOver { get; private set; }

	/// <summary>
	/// True once the player has won and chosen to keep fighting. The kill target no longer ends the fight;
	/// it ends only by retreating or being destroyed, and is still a victory. No additional rewards.
	/// </summary>
	public bool IsEndless { get; private set; }

	/// <summary>Kills and fight time when the kill target was reached, for the endless result screen.</summary>
	public int VictoryKills { get; private set; }
	public double VictoryTime { get; private set; }

	/// <summary>True while the VNP is on the gateway.</summary>
	public bool OnGateway { get; private set; }

	/// <summary>Seconds the VNP has stayed on the gateway without leaving.</summary>
	public float GatewayTime { get; private set; }

	/// <summary>True once the VNP has stayed on the gateway long enough to retreat.</summary>
	public bool CanRetreat => OnGateway && GatewayTime >= Tuning.Fight.RetreatHoldSeconds;

	/// <summary>The pause menu may open only during play, not over another menu or the result screen.</summary>
	public bool CanPause => !IsOver && !_levelUpMenu.IsOpen && !StartingWeaponMenuOpen;

	/// <summary>Whether the playtest cheat's starting weapon menu is up. Never, outside the editor.</summary>
#if TOOLS
	private bool StartingWeaponMenuOpen => _startingWeaponMenu != null && _startingWeaponMenu.IsOpen;
#else
	private bool StartingWeaponMenuOpen => false;
#endif

	/// <summary>Escalation steps reached: 0 before the time limit, 1 at it, 2 one step later, and so on.</summary>
	public int EscalationSteps => Elapsed < Tuning.Fight.TimeLimitSeconds
		? 0
		: (int)((Elapsed - Tuning.Fight.TimeLimitSeconds) / Tuning.Difficulty.EscalationStepSeconds) + 1;

	// Enemy strength now: the ramp during the fight, times escalation past the limit. "Enemy" is the basic
	// enemy; the Goo has its own. Spawn rate and aggro range are shared by every type.
	public float EnemyHealthScale => (1f + Tuning.Difficulty.RampEnemyHealthPerMinute * Minutes) * (1f + Tuning.Difficulty.EscalationEnemyHealthPerStep * EscalationSteps);
	public float EnemyDamageScale => (1f + Tuning.Difficulty.RampEnemyDamagePerMinute * Minutes) * (1f + Tuning.Difficulty.EscalationEnemyDamagePerStep * EscalationSteps);
	public float EnemySpawnRateScale => (1f + Tuning.Difficulty.RampSpawnRatePerMinute * Minutes) * (1f + Tuning.Difficulty.EscalationSpawnRatePerStep * EscalationSteps);
	public float GooHealthScale => (1f + Tuning.Difficulty.RampGooHealthPerMinute * Minutes) * (1f + Tuning.Difficulty.EscalationGooHealthPerStep * EscalationSteps);
	public float GooDamageScale => (1f + Tuning.Difficulty.RampGooDamagePerMinute * Minutes) * (1f + Tuning.Difficulty.EscalationGooDamagePerStep * EscalationSteps);
	public float EnemyAggroScale => 1f + Tuning.Difficulty.EscalationAggroRangePerStep * EscalationSteps;

	/// <summary>Accuracy added to enemies spawned now, from the ramp and escalation, on the 0 to 1 scale.</summary>
	public float EnemyAccuracyBonus => Tuning.Difficulty.RampEnemyAccuracyPerMinute * Minutes
		+ Tuning.Difficulty.EscalationEnemyAccuracyPerStep * EscalationSteps;

	private readonly List<VnpWeapon> _weapons = new();
	private Node3D _weaponHolder;
	private float _harvestTowardDrone;
	private double _levelStartExperience;
	private int _pendingLevelUps;
	private int _alertedSteps;
	private int _warnedWave = -1;
	private Label _debugLabel;
	private HealthBar _healthBar;
	private LevelUpMenu _levelUpMenu;
#if TOOLS
	private StartingWeaponMenu _startingWeaponMenu;
	private FightLog _fightLog;
#endif
	private FightHud _fightHud;
	private ResultScreen _resultScreen;
	private double _debugRefresh;

	public override void _EnterTree()
	{
		// Seed before any part of the fight is ready, since the asteroids are placed as soon as they are.
		Seed = NextSeed ?? Rng.NewSeed();
		NextSeed = null;
		Rng.Seed(Seed);
	}

	public override void _Ready()
	{
		ProcessPriority = UpdateOrder.Fight;

		Vnp = GetNode<Vnp>("Vnp");
		Gateway = GetNode<Gateway>("Gateway");
		Camera = GetNode<CameraRig>("Camera");
		Enemies = GetNode<EnemySwarm>("Enemies");
		Drones = GetNode<DroneSwarm>("Drones");
		Projectiles = GetNode<Projectiles>("Projectiles");
		Effects = GetNode<Effects>("Effects");
		DamageNumbers = GetNode<DamageNumbers>("Hud/DamageNumbers");
		Sounds = GetNode<Sounds>("Sounds");
		Harvestables = GetNode<Harvestables>("Harvestables");
		_weaponHolder = GetNode<Node3D>("Weapons");
		VnpHarvester = GetNode<VnpHarvester>("VnpHarvester");
		_debugLabel = GetNode<Label>("Hud/DebugLabel");

		// The fight starts at the gateway.
		Vnp.PlaceAt(Gateway.PlanePosition);
		Vnp.Destroyed += OnVnpDestroyed;
		Vnp.Damaged += (amount, source) =>
		{
			Stats.VnpDamaged(source, amount);
			Camera.Shake(Tuning.Feedback.ShakeOnVnpDamage);
			Sounds.Play(Sound.VnpDamage);
		};
		Vnp.LowHealthPulse += () => Sounds.Play(Sound.LowHealth);

		Camera.Target = Vnp;
		Camera.SnapToTarget();
		GetNode<GroundGrid>("Ground").Camera = Camera;

		var indicator = GetNode<GatewayIndicator>("Hud/GatewayIndicator");
		indicator.Camera = Camera;
		indicator.Gateway = Gateway;

		Harvestables.Fight = this;
		Enemies.Fight = this;
		Drones.Fight = this;
		Drones.Add(Tuning.Drones.StartingCount, Vnp.PlanePosition);
		Stats.DronesStarted = Tuning.Drones.StartingCount;
		Projectiles.Fight = this;
		// The starting weapons come from vnp.tres, unless a playtest cheat in the editor lets the player choose.
		bool chooseStartingWeapon = false;
#if TOOLS
		chooseStartingWeapon = PlaytestCheats.ChoosesStartingWeapon;
#endif
		if (!chooseStartingWeapon)
		{
			foreach (WeaponDefinition weapon in Tuning.Vnp.StartingWeapons)
				Equip(weapon);
		}
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
		DamageNumbers.Fight = this;
		GetNode<WeaponSlots>("Hud/WeaponSlots").Fight = this;

#if TOOLS
		_startingWeaponMenu = PlaytestCheats.OpenStartingWeaponMenu(this, GetNode("Hud"), _resultScreen);
		// Editor only: the fight is logged to a file for playtesting analysis.
		_fightLog = FightLog.Attach(this);
#endif
	}

	/// <summary>Ends the fight: freezes it and shows the result screen.</summary>
	public void End(Outcome outcome)
	{
		if (IsOver)
			return;
		IsOver = true;
		LastOutcome = outcome;
		if (outcome == Outcome.Win)
		{
			VictoryKills = Kills;
			VictoryTime = Elapsed;
		}

		// Nothing updates while frozen, so bring the readouts up to date first.
		RefreshDebugLabel();
		_healthBar.QueueRedraw();
		_fightHud.QueueRedraw();

		GetTree().Paused = true;
		// A level-up offer that opened in the same moment would otherwise show through behind the result screen.
		_levelUpMenu.Visible = false;
		_resultScreen.Show(outcome);
#if TOOLS
		_fightLog?.FightEnded(outcome);
#endif
	}

	/// <summary>
	/// Called by harvest beams with the mass they drew out. Turns it into fight experience, and
	/// builds temporary drones until the cap is reached. At the cap it gives only fight experience.
	/// </summary>
	public void AddHarvest(float mass, bool fromDebris = false)
	{
		// Debris reward upgrades raise the experience from enemy debris only, not from asteroids.
		float experience = mass * Tuning.Harvesting.ExperiencePerMass * (fromDebris ? DebrisExperience : 1f);
		Experience += experience;
		if (fromDebris)
			Stats.ExperienceFromDebris += experience;
		else
			Stats.ExperienceFromAsteroids += experience;

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
		while (_harvestTowardDrone >= Tuning.Harvesting.MassPerDrone && Drones.CountWithBuilds < Drones.Cap)
		{
			_harvestTowardDrone -= Tuning.Harvesting.MassPerDrone;
			Drones.Build();
		}
	}

	public void ApplyUpgrade(UpgradeDefinition upgrade)
	{
		Upgrades.Apply(this, upgrade);
		TakenUpgrades.Add(upgrade);
	}

	/// <summary>Puts a VNP Weapon in the next free slot. Does nothing if it is already equipped or no slot is free.</summary>
	public void Equip(WeaponDefinition definition)
	{
		if (definition == null || FindWeapon(definition) != null || _weapons.Count >= Tuning.Vnp.WeaponSlots)
			return;
		VnpWeapon weapon = VnpWeapon.Create(this, definition);
		if (weapon == null)
			return;
		_weapons.Add(weapon);
		_weaponHolder.AddChild(weapon);
	}

	/// <summary>The equipped weapon made from this definition, or null if it is not equipped.</summary>
	public VnpWeapon FindWeapon(WeaponDefinition definition)
	{
		if (definition == null)
			return null;
		foreach (VnpWeapon weapon in _weapons)
		{
			if (weapon.Definition.Id == definition.Id)
				return weapon;
		}
		return null;
	}

	/// <summary>Builds up to <paramref name="count"/> drones at once, stopping at the cap. Returns how many it built.</summary>
	public int BuildDronesNow(int count)
	{
		int built = 0;
		for (; built < count && Drones.CountWithBuilds < Drones.Cap; built++)
			Drones.Build();
		return built;
	}

	/// <summary>Fight experience from enemy debris, as a multiple starting at 1. Debris reward upgrades raise it.</summary>
	public float DebrisExperience = 1f;

	/// <summary>Drones built by upgrades so far, which can be fewer than they offered if the cap was near.</summary>
	public int DronesBuiltByUpgrades;

	public override void _Notification(int what)
	{
		// Closing the window quits the game: clean up first, so it doesn't crash on the way out.
		if (what == NotificationWMCloseRequest)
			ShutdownCleanup.Run();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Playtest shortcut: switch between the perspective and orthographic cameras.
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F1 })
			Tuning.Camera.Orthographic = !Tuning.Camera.Orthographic;
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
			Sounds.Play(Sound.TimeLimit);
			Camera.Shake(Tuning.Feedback.ShakeOnEscalation);
		}

		// The wave warning sounds once, as the warning appears on screen.
		if (Enemies.WavesSent != _warnedWave
			&& Enemies.NextWaveTime - Elapsed <= Tuning.EnemySpawning.WaveWarningSeconds)
		{
			_warnedWave = Enemies.WavesSent;
			Sounds.Play(Sound.WaveWarning);
		}

		// Retreat: stay on the gateway long enough and the option appears.
		OnGateway = Vnp.PlanePosition.DistanceTo(Gateway.PlanePosition) <= Tuning.Fight.GatewayRadius;
		GatewayTime = OnGateway ? GatewayTime + (float)delta : 0f;
		if (CanRetreat && Input.IsActionJustPressed("menu_select"))
		{
			End(Outcome.Retreat);
			return;
		}

		if (!IsEndless && Kills >= Tuning.Fight.KillTarget)
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
		string cameraMode = Tuning.Camera.Orthographic ? "orthographic" : "perspective";
		string nextDrone = Drones.Count >= Drones.Cap ? "at cap" : $"next {DroneBuildProgress * 100f:0}%";
		_debugLabel.Text =
			$"{Engine.GetFramesPerSecond():0} fps    " +
			$"seed {Seed}    " +
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

	/// <summary>Picks the won fight back up in endless mode, from where it stopped.</summary>
	public void ContinueEndless()
	{
		if (!IsOver || IsEndless)
			return;
		IsEndless = true;
		IsOver = false;
		GetTree().Paused = false;
	}
}
