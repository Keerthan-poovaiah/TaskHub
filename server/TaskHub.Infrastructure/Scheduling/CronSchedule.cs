using Cronos;

namespace TaskHub.Infrastructure;

// A small wrapper around the Cronos library, so the rest of the code never touches it
public static class CronSchedule
{
    // Gives the next time the schedule fires after "fromUtc".
    // Returns false if the expression is invalid.
    public static bool TryGetNext(string expression, DateTime fromUtc, out DateTime next)
    {
        next = default;

        if (!CronExpression.TryParse(expression, CronFormat.Standard, out CronExpression? cron))
            return false;

        DateTime? occurrence = cron.GetNextOccurrence(fromUtc, TimeZoneInfo.Utc);
        if (occurrence is null)
            return false;

        next = occurrence.Value;
        return true;
    }
}