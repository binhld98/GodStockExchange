namespace GodStockExchange.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of an order in the trading system, from submission to final resolution.
/// </summary>
public enum OrderStatus : byte
{
    /// <summary>
    /// The order is received by the engine but not yet validated or placed on the order book.
    /// </summary>
    New = 0,

    /// <summary>
    /// The order is accepted and resting in the order book, waiting to be filled.
    /// </summary>
    Open = 1,

    /// <summary>
    /// The order has been partially executed, with some quantity filled and the remaining quantity is still open in the order book.
    /// </summary>
    PartiallyFilled = 2,

    /// <summary>
    /// The order has been fully executed and removed from the order book. Terminal state.
    /// </summary>
    Filled = 3,

    /// <summary>
    /// The order has been cancelled by the trader or the system, and removed from the order book. Terminal state.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// The order has been rejected by the system at validation, and never placed on the order book. Terminal state.
    /// </summary>
    Rejected = 5,
}