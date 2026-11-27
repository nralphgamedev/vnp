/// <summary>
/// One fight's plan: what the Fight Director picked for it from its brief (docs/dev/FIGHT_DIRECTOR_SPEC.md). Fight
/// logs record it.
/// </summary>
public sealed class FightPlan
{
	/// <summary>The faction the fight is against: its enemy types, their weights and its limits.</summary>
	public FactionDefinition Faction;

	/// <summary>The plan followed, from the faction's plans, or null if there was none to pick.</summary>
	public FightPlanDefinition Definition;

	/// <summary>How many asteroids the fight has. The playtest cheat's asteroid amount page changes it as the fight
	/// starts.</summary>
	public AsteroidAmountDefinition AsteroidAmount;
}
