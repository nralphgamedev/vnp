using Godot;

/// <summary>Colours and effects that more than one part of the fight uses, and the gateway indicator. Values live in res://tuning/look.tres.</summary>
[GlobalClass]
public partial class LookTuning : Resource
{
	/// <summary>Colour of fight experience: harvest beams, and the tint on whatever they are harvesting.</summary>
	[ExportGroup("Shared")]
	[Export] public Color ExperienceColor;

	/// <summary>Colour of the gateway arrow and the gateway text on screen. The gateway model's own
	/// colour is in its material files in res://placeholder/materials/.</summary>
	[Export] public Color GatewayColor;

	/// <summary>Gap in pixels at 1920 by 1080 between the edge of the screen and the tip of the arrow.</summary>
	[ExportGroup("Gateway indicator")]
	[Export] public float IndicatorEdgeMargin;

	/// <summary>Length of the arrow in pixels at 1920 by 1080.</summary>
	[Export] public float IndicatorSize;

	/// <summary>False hides the arrow while the gateway itself is on screen.
	/// True keeps it at the screen edge at all times.</summary>
	[Export] public bool IndicatorAlwaysVisible;

	// Each offer card takes the colour of its upgrade's Category: on its border, as a light tint, and on
	// its top line. See "Offer card colours" in docs/design/PROTOTYPE_2_SPEC.md.

	/// <summary>Colour of offer cards for VNP Weapons and their upgrades.</summary>
	[ExportGroup("Offer cards")]
	[Export] public Color VnpWeaponCardColor;

	/// <summary>Colour of offer cards for Drone Weapons and upgrades to how drones attack.</summary>
	[Export] public Color DroneWeaponCardColor;

	/// <summary>Colour of offer cards for VNP stats and harvesting.</summary>
	[Export] public Color StatsCardColor;

	/// <summary>Colour of offer cards for how many drones there are: the drone cap and new drones.</summary>
	[Export] public Color DronesCardColor;
}
