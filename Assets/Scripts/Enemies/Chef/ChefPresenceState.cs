/// <summary>
/// The Chef's high-level presence in the kitchen hot zone. The "about to return" warning
/// phase is deliberately NOT its own state here — it is still Away for every gameplay
/// purpose (safe to be in the zone) right up until the exact tick it flips to Present; see
/// <see cref="ChefScheduleController.IsRealReturnCountdownActive"/> for that finer-grained flag.
/// </summary>
public enum ChefPresenceState
{
    Away,
    Present,
    Bedtime
}
