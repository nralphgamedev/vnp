using Godot;

/// <summary>Asteroid fields, lone asteroids, and filling the arena as the VNP explores. Values live in res://tuning/asteroids.tres.</summary>
[GlobalClass]
public partial class AsteroidsTuning : Resource
{
	/// <summary>The asteroid amounts a fight can have (res://tuning/asteroid_amounts/), each scaling how many
	/// fields and lone asteroids there are; one is picked for each fight by their weights.</summary>
	[ExportGroup("Amounts")]
	[Export] public Godot.Collections.Array<AsteroidAmountDefinition> Amounts = new();

	// These place the fields around the gateway at the start of the fight. Farther out, the arena is
	// filled as the VNP explores: see "Filling the arena" below.

	/// <summary>Fewest asteroid fields in a fight.</summary>
	[ExportGroup("Fields")]
	[Export] public int FieldsMin;

	/// <summary>Most asteroid fields in a fight.</summary>
	[Export] public int FieldsMax;

	/// <summary>Closest a field's center can be to the gateway.</summary>
	[Export] public float FieldDistanceMin;

	/// <summary>Farthest a field's center can be from the gateway.</summary>
	[Export] public float FieldDistanceMax;

	/// <summary>Closest two fields' centers can be to each other.</summary>
	[Export] public float FieldSpacing;

	/// <summary>Chance that a field is a ring. The rest are clusters.</summary>
	[Export] public float RingChance;

	/// <summary>Chance that a field drifts in a straight line.</summary>
	[Export] public float FieldMovingChance;

	/// <summary>Chance that a field turns around its center. Fields that neither drift nor turn stay put.</summary>
	[Export] public float FieldTurningChance;

	/// <summary>Speed of a drifting field, in units per second.</summary>
	[Export] public float FieldDriftSpeed;

	/// <summary>Speed of a turning field, in degrees per second.</summary>
	[Export] public float FieldTurnSpeed;

	/// <summary>Fewest asteroids in a cluster.</summary>
	[Export] public int ClusterCountMin;

	/// <summary>Most asteroids in a cluster.</summary>
	[Export] public int ClusterCountMax;

	/// <summary>Smallest radius of a cluster.</summary>
	[Export] public float ClusterRadiusMin;

	/// <summary>Largest radius of a cluster.</summary>
	[Export] public float ClusterRadiusMax;

	/// <summary>Fewest asteroids in a ring.</summary>
	[Export] public int RingCountMin;

	/// <summary>Most asteroids in a ring.</summary>
	[Export] public int RingCountMax;

	/// <summary>Smallest radius of a ring.</summary>
	[Export] public float RingRadiusMin;

	/// <summary>Largest radius of a ring.</summary>
	[Export] public float RingRadiusMax;

	/// <summary>How wide the band of a ring is.</summary>
	[Export] public float RingWidth;

	/// <summary>Mass of the smallest asteroids. Harvest time scales with mass.</summary>
	[ExportGroup("Asteroids")]
	[Export] public float MassMin;

	/// <summary>Mass of the largest asteroids.</summary>
	[Export] public float MassMax;

	/// <summary>How strongly asteroid masses lean toward small. 1 spreads them evenly;
	/// 2 makes small asteroids common and large ones rare; higher leans further.
	/// Applies to lone asteroids too.</summary>
	[Export] public float SmallBias;

	/// <summary>Chance that an asteroid tumbles: it turns slowly around a random axis in 3D, on top
	/// of turning with its field. The rest only turn with their field. Applies to lone asteroids too.</summary>
	[Export] public float TumbleChance;

	/// <summary>Slowest tumble, in degrees per second.</summary>
	[Export] public float TumbleSpeedMin;

	/// <summary>Fastest tumble, in degrees per second.</summary>
	[Export] public float TumbleSpeedMax;

	// Single asteroids scattered between the fields. Like the fields, they are placed at the start
	// of the fight and stay put.

	/// <summary>How many lone asteroids there are around the gateway. At 30 there is roughly one every couple of screens.</summary>
	[ExportGroup("Lone asteroids")]
	[Export] public int LoneCount;

	/// <summary>Closest a lone asteroid can be to the gateway.</summary>
	[Export] public float LoneDistanceMin;

	/// <summary>Farthest a lone asteroid can be from the gateway.</summary>
	[Export] public float LoneDistanceMax;

	/// <summary>Closest a lone asteroid can be to a field's center, so it does not look like part of the field.</summary>
	[Export] public float LoneFieldClearance;

	/// <summary>Closest two lone asteroids can be to each other.</summary>
	[Export] public float LoneSpacing;

	/// <summary>Mass of the smallest lone asteroids. Above the field minimum, so a lone asteroid is worth a detour.</summary>
	[Export] public float LoneMassMin;

	/// <summary>Mass of the largest lone asteroids.</summary>
	[Export] public float LoneMassMax;

	// Beyond the area around the gateway (300 units with the values above), the arena is filled in
	// square cells as the VNP comes within reach of each one, long before it is on screen. Once
	// placed, asteroids stay where they are: nothing follows the VNP and nothing respawns.
	// Fields and lone asteroids keep the same spacing rules as around the gateway.

	/// <summary>Width of one cell.</summary>
	[ExportGroup("Filling the arena")]
	[Export] public float CellSize;

	/// <summary>Cells this close to the VNP are filled. Keep it well beyond half the screen width (about 50),
	/// so filling always happens off screen.</summary>
	[Export] public float FillDistance;

	/// <summary>Fewest fields in a cell. 4 to 7 matches the density around the gateway.</summary>
	[Export] public int FieldsPerCellMin;

	/// <summary>Most fields in a cell.</summary>
	[Export] public int FieldsPerCellMax;

	/// <summary>Lone asteroids in a cell. 17 matches the density around the gateway.</summary>
	[Export] public int LonePerCell;

	/// <summary>Scale of an asteroid with mass 1, about 1.8 units across. Heavier asteroids are
	/// drawn larger, and every asteroid shrinks as it is harvested.</summary>
	[ExportGroup("Look")]
	[Export] public float Size;

	/// <summary>How many different rock shapes asteroids are drawn with.</summary>
	[Export] public int ShapeVariants;

	/// <summary>How lumpy the rock shapes are. 0 is a plain ball; 0.5 is very lumpy.</summary>
	[Export] public float Bumpiness;

	/// <summary>Each asteroid is stretched or squashed along each of its own axes by a random amount
	/// between these, so asteroids with the same rock shape still differ.</summary>
	[Export] public float StretchMin;

	[Export] public float StretchMax;

	[Export] public Color Color;
}
