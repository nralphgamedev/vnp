/// <summary>
/// What a fight is asked to be, before it starts: the Fight Director makes the fight's plan from it alone
/// (docs/dev/FIGHT_DIRECTOR_SPEC.md). The game sets it for now; later the galactic map will, from the sector being
/// fought over. The difficulty joins it when the director first uses it.
///
/// Everything in it is a request: what is left unset, the director picks, by the seed.
/// </summary>
public sealed class FightBrief
{
	/// <summary>The fight's seed, or null for one picked at random as the fight starts.</summary>
	public int? Seed;

	/// <summary>The faction the fight is against, or null for DefaultFaction in
	/// res://director/fight_director.tres.</summary>
	public FactionDefinition Faction;

	/// <summary>A plan by its ID, from the faction's plans, or null for one picked by their weights. The scenario
	/// harness and batches set it.</summary>
	public string PlanId;

	/// <summary>An asteroid amount by its ID, or null for the plan to pick one by the amounts' weights. The
	/// scenario harness sets it.</summary>
	public string AsteroidAmountId;
}
