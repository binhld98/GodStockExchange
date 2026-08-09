namespace Oee.MarketAccess.Validation
{
    public enum ErrorCode
    {
        MissingSide,
        UnsupportedSide,

        MissingOrderType,
        UnsupportedOrderType,

        MissingTimeInForce,
        UnsupportedTimeInForce,

        MissingSymbol,
        UnknownInstrument,
        InstrumentNotTradeable,

        UnsupportedQuantityRepresentation,
        MissingOrderQuantity,
        NonPositiveOrderQuantity,
        FractionalOrderQuantity,
        OrderQuantityTooLarge,
        OrderQuantityNotLotAligned,
        OrderQuantityExceedsMaximum,

        UnexpectedPrice,
        MissingPrice,
        NonPositivePrice,
        PriceNotTickAligned,
        PriceOutsideBand
    }
}