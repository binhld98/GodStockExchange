using System.Runtime.CompilerServices;
using GodStockExchange.Domain.Common;

namespace GodStockExchange.Domain.Values;

/// <summary>
/// Represents a valid price range for an instrument, expressed as inclusive tick boundaries.
/// </summary>
public readonly record struct PriceBand
{
    /// <summary>
    /// Mnimun acceptable price in ticks, inclusive.
    /// </summary>
    public long FloorTicks { get; }

    /// <summary>
    /// Maximum acceptable price in ticks, inclusive.
    /// </summary>
    public long CeilingTicks { get; }

    public readonly long TickRange => CeilingTicks - FloorTicks + 1;

    public PriceBand(long floorTicks, long ceilingTicks)
    {
        Guard.NonNegative(floorTicks, nameof(floorTicks));
        Guard.NonNegative(ceilingTicks, nameof(ceilingTicks));
        Guard.Requires(floorTicks <= ceilingTicks, "Floor price must be less than or equal to ceiling price.");

        FloorTicks = floorTicks;
        CeilingTicks = ceilingTicks;
    }

    /// <summary>
    /// Determines if the given price in ticks falls within the price band, including the floor and ceiling.
    /// </summary>
    /// <param name="priceTicks"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(long priceTicks)
        => priceTicks >= FloorTicks && priceTicks <= CeilingTicks;

    public override string ToString()
        => $"PriceBand [FloorTicks={FloorTicks}, CeilingTicks={CeilingTicks}]";
}