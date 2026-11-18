using Godot;

/// <summary>Colours and effects that more than one part of the fight uses, and the gateway indicator. Values live in res://tuning/look.tres.</summary>
[GlobalClass]
public partial class LookTuning : Resource
{
	/// <summary>Seconds an enemy or the VNP flashes after being hit.</summary>
	[ExportGroup("Shared")]
	[Export] public float HitFlashTime;

	/// <summary>Colour of fight experience: harvest beams, and the tint on whatever they are harvesting.</summary>
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
}
