namespace GodStockExchange.Domain.Enums;

/// <summary>
/// Defines how an order should be matched against resting orders in the order book.
/// </summary>
public enum OrderType : byte
{
    /// <summary>
    /// Executes immediately at best available price, never rests in the order book.
    /// </summary>
    Market = 0,

    /// <summary>
    /// Executes at a specified price or better, rests in the order book until it can be fully executed or cancelled.
    /// </summary>
    Limit = 1,

    /// <summary>
    /// Rests in the order book until the market price touches the stop price, then becomes a market order.
    /// </summary>
    Stop = 2,
}