using GodStockExchange.Domain.Common;

namespace GodStockExchange.Domain.Models;

/// <summary>
/// Represents a trade execution between a maker (resting) order and a taker (aggressor) order.
/// </summary>
public readonly struct Trade
{
    public long InstrumentId { get; }

    /// <summary>
    /// Order ID of the maker (resting) order.
    /// </summary>
    public long MakerOrderId { get; }

    /// <summary>
    /// Order ID of the taker (aggressor) order.
    /// </summary>
    public long TakerOrderId { get; }

    /// <summary>
    /// maker's price ticks.
    /// </summary>
    public long PriceTicks { get; }

    /// <summary>
    /// Quantity exchanged in lots.
    /// </summary>
    public long Quantity { get; }

    /// <summary>
    /// UTC timestamp when the trade was executed, in nanoseconds.
    /// </summary>
    public long ExecutedAtNs { get; }

    public Trade(long instrumentId, long makerOrderId, long takerOrderId, long priceTicks, long quantity, long executedAtNs)
    {
        Guard.NonNegative(instrumentId, nameof(instrumentId));
        Guard.NonNegative(makerOrderId, nameof(makerOrderId));
        Guard.NonNegative(takerOrderId, nameof(takerOrderId));
        Guard.NonNegative(priceTicks, nameof(priceTicks));
        Guard.NonNegative(quantity, nameof(quantity));
        Guard.NonNegative(executedAtNs, nameof(executedAtNs));

        InstrumentId = instrumentId;
        MakerOrderId = makerOrderId;
        TakerOrderId = takerOrderId;
        PriceTicks = priceTicks;
        Quantity = quantity;
        ExecutedAtNs = executedAtNs;
    }

    /// <summary>
    /// <see cref="ExecutedAtNs"/> = current timestamp.
    /// </summary>
    /// <param name="instrumentId"></param>
    /// <param name="makerOrderId"></param>
    /// <param name="takerOrderId"></param>
    /// <param name="priceTicks"></param>
    /// <param name="quantity"></param>
    public Trade(long instrumentId, long makerOrderId, long takerOrderId, long priceTicks, long quantity)
    {
        Guard.NonNegative(instrumentId, nameof(instrumentId));
        Guard.NonNegative(makerOrderId, nameof(makerOrderId));
        Guard.NonNegative(takerOrderId, nameof(takerOrderId));
        Guard.NonNegative(priceTicks, nameof(priceTicks));
        Guard.NonNegative(quantity, nameof(quantity));

        InstrumentId = instrumentId;
        MakerOrderId = makerOrderId;
        TakerOrderId = takerOrderId;
        PriceTicks = priceTicks;
        Quantity = quantity;
        ExecutedAtNs = DateHelper.GetCurrentTimestampNs();
    }

    public override string ToString()
        => $"Trade [InstrumentId={InstrumentId}, MakerOrderId={MakerOrderId}, TakerOrderId={TakerOrderId}, PriceTicks={PriceTicks}, Quantity={Quantity}, ExecutedAtNs={ExecutedAtNs}]";
}