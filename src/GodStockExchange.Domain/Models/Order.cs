using GodStockExchange.Domain.Common;
using GodStockExchange.Domain.Enums;

namespace GodStockExchange.Domain.Models;

/// <summary>
/// Represents an immutable value-type of a single order.
/// </summary>
public struct Order
{
    public long OrderId { get; }

    public long ClientOrderId { get; }

    public long InstrumentId { get; }

    public OrderSide Side { get; }

    public OrderType Type { get; }

    public TimeInForce TimeInForce { get; }

    public AuctionConstraint AuctionConstraint { get; }

    /// <summary>
    /// Price of the order, expressed in minimun price increments (ticks).
    /// For example, if the price is $123.45 and the tick size is $0.01, then PriceTicks would be 12345.
    /// For order of type <see cref="OrderType.Market"/>, this is the limit price.
    /// For order of type <see cref="OrderType.Limit"/>, this value is typically ignored and should be set to 0.
    /// </summary>
    public long PriceTicks { get; }

    /// <summary>
    /// The total requested quantity in lots when the order was created.
    /// This value remains constant throughout the lifecycle of the order amd does not decrease as the order is filled.
    /// </summary>
    public long OrigQty { get; }

    /// <summary>
    /// The remaining quantity of the order that is still open and can be filled
    /// </summary>
    public long LeavesQty { get; private set; }

    /// <summary>
    /// The cumulative quantity of the order that has been filled so far.
    /// </summary>
    public readonly long CumQty => OrigQty - LeavesQty;

    public OrderStatus Status { get; private set;}

    /// <summary>
    /// UTC timestamp in nanoseconds when the order was received by the gateway.
    /// </summary>
    public long ReceivedAtNs { get; }

    /// <summary>
    /// <c>true</c> if the order is in a terminal state, meaning it can no longer be modified or filled. Terminal states include Filled, Cancelled, and Rejected.
    /// </summary>
    public readonly bool IsTerminal => Status is OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Rejected;

    /// <summary>
    /// <c>true</c> if the order is restricted to an auction phase, meaning it can only be executed during the opening or closing auction.
    /// </summary>
    public readonly bool IsAuctionOrder => AuctionConstraint != AuctionConstraint.None;

    public Order
    (
        long orderId,
        long clientOrderId,
        long instrumentId,
        OrderSide side,
        OrderType type,
        TimeInForce timeInForce,
        AuctionConstraint auctionConstraint,
        long priceTicks,
        long origQty,
        long leavesQty,
        OrderStatus status,
        long receivedAtNs
    )
    {
        OrderId = orderId;
        ClientOrderId = clientOrderId;
        InstrumentId = instrumentId;
        Side = side;
        Type = type;
        TimeInForce = timeInForce;
        AuctionConstraint = auctionConstraint;
        PriceTicks = priceTicks;
        OrigQty = origQty;
        LeavesQty = leavesQty;
        Status = status;
        ReceivedAtNs = receivedAtNs;
    }

    public Order
    (
        long orderId,
        long clientOrderId,
        long instrumentId,
        OrderSide side,
        OrderType type,
        TimeInForce timeInForce,
        AuctionConstraint auctionConstraint,
        long priceTicks,
        long origQty
    )
    {
        OrderId = orderId;
        ClientOrderId = clientOrderId;
        InstrumentId = instrumentId;
        Side = side;
        Type = type;
        TimeInForce = timeInForce;
        AuctionConstraint = auctionConstraint;
        PriceTicks = priceTicks;
        OrigQty = origQty;
        LeavesQty = origQty;
        Status = OrderStatus.New;
        ReceivedAtNs = DateHelper.GetCurrentTimestampNs();
    }

    /// <summary>
    /// Returns a copy of this order with <see cref="Status"/> set to <see cref="OrderStatus.Open"/>
    /// </summary>
    /// <returns></returns>
    public readonly Order AsOpen()
    {
        Guard.Requires(Status == OrderStatus.New, "Only orders in 'New' status can be opened.");
        var copy = this;
        copy.Status = OrderStatus.Open;
        return copy;
    }

    /// <summary>
    /// Returns a copy of this order with <see cref="Status"/> set to <see cref="OrderStatus.Cancelled"/>
    /// </summary>
    /// <returns></returns>
    public readonly Order AsCancelled()
    {
        Guard.Requires(!IsTerminal, "Only orders that are not in a terminal state can be cancelled.");
        var copy = this;
        copy.Status = OrderStatus.Cancelled;
        return copy;
    }

    /// <summary>
    /// Returns a copy of this order with <see cref="Status"/> set to <see cref="OrderStatus.Rejected"/>
    /// </summary>
    /// <returns></returns>
    public readonly Order AsRejected()
    {
        Guard.Requires(!IsTerminal, "Only orders that are not in a terminal state can be rejected.");
        var copy = this;
        copy.Status = OrderStatus.Rejected;
        return copy;
    }

    /// <summary>
    /// Returns a copy of this order with <paramref name="executedQty"/> deducted from <see cref="LeavesQty"/> and <see cref="Status"/> updated accordingly.
    /// </summary>
    /// <param name="executedQty"></param>
    /// <returns></returns>
    public readonly Order WithFill(long executedQty)
    {
        Guard.Positive(executedQty, nameof(executedQty));
        Guard.Requires(!IsTerminal, "Cannot fill an order that is in a terminal state.");
        Guard.Requires(executedQty <= LeavesQty, "Executed quantity cannot exceed the remaining quantity");

        var copy = this;
        copy.LeavesQty -= executedQty;
        copy.Status = copy.LeavesQty == 0 ? OrderStatus.Filled : OrderStatus.PartiallyFilled;
        return copy;
    }
}