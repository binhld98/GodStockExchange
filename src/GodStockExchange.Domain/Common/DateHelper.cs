namespace GodStockExchange.Domain.Common;

public static class DateHelper
{
    /// <summary>
    /// Gets the current timestamp in nanoseconds.
    /// </summary>
    public static long GetCurrentTimestampNs()
    {
        return (DateTimeOffset.UtcNow.Ticks - DateTimeOffset.UnixEpoch.Ticks) * 100;
    }
}