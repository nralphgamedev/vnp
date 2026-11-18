/// <summary>
/// The order the parts of the fight update in each frame, lowest first.
/// Each part sets its ProcessPriority from here, so the order is decided in one place.
/// </summary>
public static class UpdateOrder
{
	public const int Fight = -10;
	public const int Vnp = 0;
	public const int Harvestables = 1;
	public const int Enemies = 2;
	public const int Drones = 3;
	public const int Projectiles = 4;
	public const int Weapons = 5;
	public const int Effects = 6;
	public const int Audio = 7;
	public const int Camera = 10;
	public const int Ground = 15;
	public const int Hud = 20;
}
