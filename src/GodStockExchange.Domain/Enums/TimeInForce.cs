namespace GodStockExchange.Domain.Enums;

/// <summary>
/// Defines how long an unfilled order should remain active before it is automatically cancelled.
/// </summary>
public enum TimeInForce : byte
{
    /// <summary>
    /// Day - Expires at the end of the current trading day session. Default value for orders.
    /// </summary>
    Day = 0,

    /// <summary>
    /// Good Till Cancelled - Remains active until explicitly cancelled or fully executed.
    /// </summary>
    GTC = 1,

    /// <summary>
    /// Immediate Or Cancel - Executes any portion of the order that can be filled immediately, and cancels the rest.
    /// </summary>
    IOC = 2,

    /// <summary>
    /// Fill Or Kill - Executes the entire order immediately, or cancels it entirely if it cannot be filled in one go.
    /// </summary>
    FOK = 3,
}