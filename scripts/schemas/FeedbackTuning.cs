using Godot;

/// <summary>How hits, kills, harvesting and level ups look: flashes, damage numbers, screen shake and effects.
/// Values live in res://tuning/feedback.tres.</summary>
[GlobalClass]
public partial class FeedbackTuning : Resource
{
	/// <summary>Seconds an enemy, a drone or the VNP flashes after being hit.</summary>
	[ExportGroup("Hits")]
	[Export] public float HitFlashTime;

	/// <summary>Numbers rise from damaged enemies. Off hides them.</summary>
	[ExportGroup("Damage numbers")]
	[Export] public bool DamageNumbers;

	/// <summary>Font size in pixels at 1920 by 1080.</summary>
	[Export] public int DamageNumberSize;

	/// <summary>Seconds a number stays up.</summary>
	[Export] public float DamageNumberTime;

	/// <summary>How fast a number rises, in pixels per second.</summary>
	[Export] public float DamageNumberRise;

	/// <summary>Hits on the same enemy this soon after a number appeared are added to it rather than
	/// shown separately, so fast beams do not bury the screen in ones.</summary>
	[Export] public float DamageNumberMergeSeconds;

	/// <summary>Most numbers on screen at once. When full, new hits are not shown.</summary>
	[Export] public int DamageNumberMax;

	[Export] public Color DamageNumberColor;

	/// <summary>Off turns all screen shake off.</summary>
	[ExportGroup("Screen shake")]
	[Export] public bool ScreenShake;

	/// <summary>How far the camera moves at the strongest shake, in world units. At the default camera
	/// height the screen is about 130 units wide.</summary>
	[Export] public float ShakeStrength;

	/// <summary>Seconds a shake takes to die away.</summary>
	[Export] public float ShakeTime;

	/// <summary>Shake when the VNP takes damage, from 0 to 1 of the strongest.</summary>
	[Export] public float ShakeOnVnpDamage;

	/// <summary>Shake when a wave arrives, from 0 to 1 of the strongest.</summary>
	[Export] public float ShakeOnWave;

	/// <summary>Shake at the time limit and each escalation step, from 0 to 1 of the strongest.</summary>
	[Export] public float ShakeOnEscalation;

	/// <summary>Below this fraction of its health, the VNP's health bar pulses and the VNP flashes now and then.</summary>
	[ExportGroup("Low health")]
	[Export] public float LowHealthFraction;

	/// <summary>Seconds between low-health flashes.</summary>
	[Export] public float LowHealthPulseSeconds;

	/// <summary>Seconds a destroyed enemy bursts apart before its debris appears.</summary>
	[ExportGroup("Deaths")]
	[Export] public float DebrisDelay;

	/// <summary>Pieces a destroyed enemy bursts into.</summary>
	[Export] public int EnemyDeathChunks;

	/// <summary>Sparks when an enemy is destroyed.</summary>
	[Export] public int EnemyDeathSparks;

	/// <summary>Sparks when a drone is destroyed.</summary>
	[Export] public int DronePopSparks;

	/// <summary>Size of every burst, spark and ring, and how far pieces fly. 1 is the size they were
	/// designed at; raise it if effects look small at the camera height.</summary>
	[ExportGroup("Effects")]
	[Export] public float EffectSize;

	/// <summary>Sparks where a shot, beam or shard hits.</summary>
	[ExportGroup("Weapons")]
	[Export] public int ImpactSparks;

	/// <summary>Sparks where a shot is fired from.</summary>
	[Export] public int MuzzleSparks;

	/// <summary>How fast sparks fly out, in units per second.</summary>
	[Export] public float SparkSpeed;

	/// <summary>Seconds a spark lasts.</summary>
	[Export] public float SparkTime;

	/// <summary>Orbital Shards leave a short trail.</summary>
	[Export] public bool ShardTrails;

	/// <summary>How long an Orbital Shards trail is, as seconds of the shard's travel. Damage upgrades
	/// lengthen it, up to three times this.</summary>
	[Export] public float ShardTrailTime;

	/// <summary>Particles per second flowing along the VNP's harvest beam.</summary>
	[ExportGroup("Harvesting")]
	[Export] public float VnpHarvestParticles;

	/// <summary>Particles per second flowing along each drone's harvest beam.</summary>
	[Export] public float DroneHarvestParticles;

	/// <summary>How fast harvest particles travel, in units per second.</summary>
	[Export] public float HarvestParticleSpeed;

	/// <summary>Pieces an asteroid or piece of debris pops into when it is used up.</summary>
	[Export] public int MaterialPopChunks;

	/// <summary>Seconds the ring from the VNP takes to spread when a level-up offer appears.</summary>
	[ExportGroup("Level up")]
	[Export] public float LevelUpPulseTime;

	/// <summary>Most effect particles at once, across sparks, pieces and rings. When full, new ones are
	/// skipped, which keeps big fights from slowing down.</summary>
	[ExportGroup("Performance")]
	[Export] public int MaxParticles;
}
