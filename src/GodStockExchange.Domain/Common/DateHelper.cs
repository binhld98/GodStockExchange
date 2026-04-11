using System.Runtime.CompilerServices;

namespace GodStockExchange.Domain.Common;

public static class DateHelper
{
    /// <summary>
    /// Gets the current timestamp in nanoseconds.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetCurrentTimestampNs()
        => (DateTimeOffset.UtcNow.Ticks - DateTimeOffset.UnixEpoch.Ticks) * 100L;

    /// <summary>
    /// Converts a timestamp in nanoseconds to a DateTimeOffset.
    /// </summary>
    /// <param name="timestampNs"></param>
    /// <returns></returns>
    public static DateTimeOffset FromTimestampNs(long timestampNs)
        => DateTimeOffset.UnixEpoch.AddTicks(timestampNs / 100L);
}