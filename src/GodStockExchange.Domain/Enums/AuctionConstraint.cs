namespace GodStockExchange.Domain.Enums;

/// <summary>
/// Specifies which auction phase an order is restricted to, if any.
/// </summary>
public enum AuctionConstraint : byte
{
    /// <summary>
    /// No auction constraint, the order is valid during all phases of the trading day.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Order is valid only during the PreOpen call auction phase.
    /// </summary>
    AtOpen = 1,

    /// <summary>
    /// Order is valid only during the PreClose call auction phase.
    /// </summary>
    AtClose = 2,
}