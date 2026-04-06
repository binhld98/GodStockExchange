namespace GodStockExchange.Domain.Enums;

/// <summary>
/// Indicates whether an order is a buy (bid) or a sell (ask).
/// </summary>
public enum OrderSide : byte
{
    Buy = 0,
    Sell = 1
}