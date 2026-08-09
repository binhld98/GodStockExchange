namespace Oee.MarketAccess.Catalog;

public readonly record struct SimpleInstrumentProfile
{
    public long InstrumentId { get; }

    public string Symbol { get; }

    public bool IsTradeable { get; }

    public decimal TickSize { get; }

    public long LotSize { get; }

    public long MaxLots { get; }

    public long PriceBandFloorTicks { get; }

    public long PriceBandCeilingTicks { get; }

    public SimpleInstrumentProfile
    (
        long instrumentId,
        string symbol,
        bool isTradeable,
        decimal tickSize,
        long lotSize,
        long maxLots,
        long priceBandFloorTicks,
        long priceBandCeilingTicks
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(instrumentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tickSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lotSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lotSize);
        ArgumentOutOfRangeException.ThrowIfNegative(maxLots);
        ArgumentOutOfRangeException.ThrowIfNegative(priceBandFloorTicks);

        if (priceBandFloorTicks > priceBandCeilingTicks)
            throw new ArgumentException("Price-band floor must not exceed its ceiling.");

        InstrumentId = instrumentId;
        Symbol = symbol;
        IsTradeable = isTradeable;
        TickSize = tickSize;
        LotSize = lotSize;
        MaxLots = maxLots;
        PriceBandFloorTicks = priceBandFloorTicks;
        PriceBandCeilingTicks = priceBandCeilingTicks;
    }
}