using QuickFix.Fields;
using QuickFix.FIX44;

namespace Oee.MarketAccess.Tests.Validation;

internal static class FixMessageFactory
{
    public static NewOrderSingle New(
        string symbol = "AAPL",
        char side = Side.BUY,
        char orderType = OrdType.LIMIT,
        char? timeInForce = TimeInForce.DAY,
        decimal? quantity = 100m,
        decimal? price = 330m
    )
    {
        var message = new NewOrderSingle(
            new ClOrdID("ClOrdID-Fake"),
            new Symbol(symbol),
            new Side(side),
            new TransactTime(DateTime.UtcNow),
            new OrdType(orderType)
        );

        message.SetOptionalFields(timeInForce, quantity, price);

        return message;
    }

    public static OrderCancelReplaceRequest CancelReplace(
        string symbol = "AAPL",
        char side = Side.BUY,
        char orderType = OrdType.LIMIT,
        char? timeInForce = TimeInForce.DAY,
        decimal? quantity = 100m,
        decimal? price = 330m
    )
    {
        var message = new OrderCancelReplaceRequest(
            new OrigClOrdID("OrigClOrdID-Fake"),
            new ClOrdID("ClOrdID-CancelReplace-Fake"),
            new Symbol(symbol),
            new Side(side),
            new TransactTime(DateTime.UtcNow),
            new OrdType(orderType)
        );

        message.SetOptionalFields(timeInForce, quantity, price);

        return message;
    }

    public static OrderCancelRequest Cancel(string symbol = "AAPL", char side = Side.BUY)
    {
        return new OrderCancelRequest(
            new OrigClOrdID("OrigClOrdID-Fake"),
            new ClOrdID("ClOrdID-Cancel-Fake"),
            new Symbol(symbol),
            new Side(side),
            new TransactTime(DateTime.UtcNow)
        );
    }
    
    public static OrderStatusRequest Status(string symbol = "AAPL", char side = Side.BUY)
    {
        return new OrderStatusRequest(
            new ClOrdID("ClOrdID-Status-Fake"),
            new Symbol(symbol),
            new Side(side)
        );
    }

    private static void SetOptionalFields(
        this QuickFix.Message message,
        char? timeInForce = null,
        decimal? quantity = null,
        decimal? price = null
    )
    {
        if (timeInForce.HasValue)
            message.SetField(new TimeInForce(timeInForce.Value));

        if (quantity.HasValue)
            message.SetField(new OrderQty(quantity.Value));

        if (price.HasValue)
            message.SetField(new Price(price.Value));
    }
}