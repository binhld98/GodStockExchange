using System.Runtime.CompilerServices;
using GodStockExchange.Domain.Common;
using GodStockExchange.Domain.Values;

namespace GodStockExchange.Domain.Models;

/// <summary>
/// Represents a financial instrument that can be traded on the exchange, such as a stock, future, or cryptocurrency.
/// </summary>
public readonly struct Instrument
{
    /// <summary>
    /// Exchange-assigned unique identifier for the instrument.
    /// </summary>
    public long InstrumentId { get; }

    /// <summary>
    /// Human-readable ticker symbol for the intrument, such as "AAPL" for Apple Inc. or "BTCUSD" for Bitcoin against US Dollar.
    /// </summary>
    public string Ticker { get; }

    /// <summary>
    /// Human-readable full instrument name, such as "Apple Inc." for AAPL or "Bitcoin / US Dollar" for BTCUSD.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Value of one tick in the quote currency.
    /// </summary>
    public decimal TickSize { get; }

    /// <summary>
    /// ISO 4217 currency code, such as "USD" for US Dollar or "EUR" for Euro.
    /// </summary>
    public string QuoteCurrency { get; }

    /// <summary>
    /// Minimum order quantity and quantity increment, in lots.
    /// All order quantities must be positive integer multiples of the lot size.
    /// </summary>
    public long LotSize { get; }

    /// <summary>
    /// Maximum quantity permitted on a single order, in lots.
    /// </summary>
    public long MaxLots { get; }

    /// <summary>
    /// Circuit-breaker price band. All orders outside of this range are rejected by the exchange.
    /// </summary>
    public PriceBand PriceBand { get; }

    public Instrument(long instrumentId, string ticker, string description, decimal tickSize, string quoteCurrency, long lotSize, long maxLots, PriceBand priceBand)
    {
        Guard.NonNegative(instrumentId, nameof(instrumentId));
        Guard.NotEmpty(ticker, nameof(ticker));
        Guard.NotEmpty(description, nameof(description));
        Guard.NonNegative(tickSize, nameof(tickSize));
        Guard.NotEmpty(quoteCurrency, nameof(quoteCurrency));
        Guard.NonNegative(lotSize, nameof(lotSize));
        Guard.NonNegative(maxLots, nameof(maxLots));

        InstrumentId = instrumentId;
        Ticker = ticker;
        Description = description;
        TickSize = tickSize;
        QuoteCurrency = quoteCurrency;
        LotSize = lotSize;
        MaxLots = maxLots;
        PriceBand = priceBand;
    }

    /// <summary>
    /// Determines if the given quantity is valid for this instrument.
    /// It must be positive, a multiple of the lot size, and not exceed the maximum allowed lots.
    /// </summary>
    /// <param name="quantity"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidQuantity(long quantity)
        => quantity > 0 && quantity % LotSize == 0 && quantity / LotSize <= MaxLots;

    /// <summary>
    /// Converts a price expressed in minimum price increments (ticks) to a decimal price.
    /// </summary>
    /// <param name="priceTicks"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public decimal TicksToDecimal(long priceTicks)
        => priceTicks * TickSize;
}