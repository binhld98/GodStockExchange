using Oee.MarketAccess.Tests.Catalog;
using Oee.MarketAccess.Validation;
using QuickFix.Fields;

namespace Oee.MarketAccess.Tests.Validation;

public sealed class NewOrderSingleValidatorTests
{
    private readonly Validator _validator;

    public NewOrderSingleValidatorTests()
    {
        var catalog = IgnoreCaseInstrumentCatalog.New_With_Tradeable_AAPL_And_Untradeable_NVDA();
        _validator = new(catalog);
    }

    [Theory]
    [InlineData(Side.BUY)]
    [InlineData(Side.SELL)]
    public void Every_Supported_Side_Must_Be_Accepted(char side)
    {
        var actual = _validator.Validate(FixMessageFactory.New(side: side));
        AssertSuccess(actual);
    }

    [Fact]
    public void Market_Order_Without_Price_Must_Be_Accepted()
    {
        var actual = _validator.Validate(FixMessageFactory.New(orderType: OrdType.MARKET, price: null));
        AssertSuccess(actual);
    }

    [Fact]
    public void Limit_Order_With_Price_Must_Be_Accepted()
    {
        var actual = _validator.Validate(FixMessageFactory.New(orderType: OrdType.LIMIT, price: 330m));
        AssertSuccess(actual);
    }

    [Theory]
    [InlineData(TimeInForce.DAY)]
    [InlineData(TimeInForce.GOOD_TILL_CANCEL)]
    [InlineData(TimeInForce.IMMEDIATE_OR_CANCEL)]
    [InlineData(TimeInForce.FILL_OR_KILL)]
    public void Every_Supported_Time_In_Force_Must_Be_Accepted(char timeInForce)
    {
        var actual = _validator.Validate(FixMessageFactory.New(timeInForce: timeInForce));
        AssertSuccess(actual);
    }

    [Theory]
    [InlineData(100L)]
    [InlineData(100000L)]
    public void Quantity_At_Valid_Boundary_Must_Be_Accepted(long quantity)
    {
        var actual = _validator.Validate(FixMessageFactory.New(quantity: quantity));
        AssertSuccess(actual);
    }

    [Theory]
    [InlineData(300L)]
    [InlineData(360L)]
    public void Limit_Price_At_Band_Boundary_Must_Be_Accepted(long price)
    {
        var actual = _validator.Validate(FixMessageFactory.New(orderType: OrdType.LIMIT, price: price));
        AssertSuccess(actual);
    }

    [Theory]
    [InlineData(Side.AS_DEFINED)]
    [InlineData(Side.BORROW)]
    // [InlineData(Side.BUY)]
    [InlineData(Side.BUY_MINUS)]
    [InlineData(Side.CROSS)]
    [InlineData(Side.CROSS_SHORT)]
    [InlineData(Side.CROSS_SHORT_EXEMPT)]
    [InlineData(Side.LEND)]
    [InlineData(Side.OPPOSITE)]
    [InlineData(Side.REDEEM)]
    // [InlineData(Side.SELL)]
    [InlineData(Side.SELL_PLUS)]
    [InlineData(Side.SELL_SHORT)]
    [InlineData(Side.SELL_SHORT_EXEMPT)]
    [InlineData(Side.SUBSCRIBE)]
    [InlineData(Side.UNDISCLOSED)]
    public void Every_Unsupported_Side_Must_Be_Rejected(char side)
    {
        var actual = _validator.Validate(FixMessageFactory.New(side: side));
        AssertFailure(actual, ErrorCode.UnsupportedSide, Side.TAG);
    }

    [Theory]
    [InlineData(OrdType.COUNTER_ORDER_SELECTION)]
    [InlineData(OrdType.FOREX_LIMIT)]
    [InlineData(OrdType.FOREX_MARKET)]
    [InlineData(OrdType.FOREX_PREVIOUSLY_QUOTED)]
    [InlineData(OrdType.FOREX_SWAP)]
    [InlineData(OrdType.FUNARI)]
    // [InlineData(OrdType.LIMIT)]
    [InlineData(OrdType.LIMIT_ON_CLOSE)]
    [InlineData(OrdType.LIMIT_OR_BETTER)]
    [InlineData(OrdType.LIMIT_WITH_OR_WITHOUT)]
    // [InlineData(OrdType.MARKET)]
    [InlineData(OrdType.MARKET_IF_TOUCHED)]
    [InlineData(OrdType.MARKET_ON_CLOSE)]
    [InlineData(OrdType.MARKET_WITH_LEFTOVER_AS_LIMIT)]
    [InlineData(OrdType.NEXT_FUND_VALUATION_POINT)]
    [InlineData(OrdType.ON_BASIS)]
    [InlineData(OrdType.ON_CLOSE)]
    [InlineData(OrdType.PEGGED)]
    [InlineData(OrdType.PREVIOUSLY_INDICATED)]
    [InlineData(OrdType.PREVIOUSLY_QUOTED)]
    [InlineData(OrdType.PREVIOUS_FUND_VALUATION_POINT)]
    [InlineData(OrdType.STOP)]
    [InlineData(OrdType.STOP_LIMIT)]
    [InlineData(OrdType.WITH_OR_WITHOUT)]
    public void Every_Unsupported_Order_Type_Must_Be_Rejected(char orderType)
    {
        var actual = _validator.Validate(FixMessageFactory.New(orderType: orderType));
        AssertFailure(actual, ErrorCode.UnsupportedOrderType, OrdType.TAG);
    }

    [Fact]
    public void Missing_Time_In_Force_Must_Be_Rejected()
    {
        var actual = _validator.Validate(FixMessageFactory.New(timeInForce: null));
        AssertFailure(actual, ErrorCode.MissingTimeInForce, TimeInForce.TAG);
    }

    [Theory]
    [InlineData(TimeInForce.AT_CROSSING)]
    [InlineData(TimeInForce.AT_THE_CLOSE)]
    [InlineData(TimeInForce.AT_THE_OPENING)]
    // [InlineData(TimeInForce.DAY)]
    // [InlineData(TimeInForce.FILL_OR_KILL)]
    [InlineData(TimeInForce.GOOD_THROUGH_CROSSING)]
    // [InlineData(TimeInForce.GOOD_TILL_CANCEL)]
    [InlineData(TimeInForce.GOOD_TILL_CROSSING)]
    [InlineData(TimeInForce.GOOD_TILL_DATE)]
    // [InlineData(TimeInForce.IMMEDIATE_OR_CANCEL)]
    public void Every_Unsupported_Time_In_Force_Must_Be_Rejected(char timeInForce)
    {
        var actual = _validator.Validate(FixMessageFactory.New(timeInForce: timeInForce));
        AssertFailure(actual, ErrorCode.UnsupportedTimeInForce, TimeInForce.TAG);
    }

    [Fact]
    public void Unknown_Symbol_Must_Be_Rejected()
    {
        var actual = _validator.Validate(FixMessageFactory.New(symbol: "!@#$"));
        AssertFailure(actual, ErrorCode.UnknownInstrument, Symbol.TAG);
    }

    [Fact]
    public void Untradable_Instrument_Must_Be_Rejected()
    {
        var actual = _validator.Validate(FixMessageFactory.New(symbol: "NVDA"));
        AssertFailure(actual, ErrorCode.InstrumentNotTradeable, Symbol.TAG);
    }

    [Fact]
    public void Cash_Quantity_Must_Be_Rejected()
    {
        var message = FixMessageFactory.New();
        message.SetField(new CashOrderQty(100m));

        var actual = _validator.Validate(message);
        AssertFailure(actual, ErrorCode.UnsupportedQuantityRepresentation, CashOrderQty.TAG);
    }

    [Fact]
    public void Percentage_Quantity_Must_Be_Rejected()
    {
        var message = FixMessageFactory.New();
        message.SetField(new OrderPercent(10m));

        var actual = _validator.Validate(message);
        AssertFailure(actual, ErrorCode.UnsupportedQuantityRepresentation, OrderPercent.TAG);
    }

    [Fact]
    public void Missing_Quantity_Must_Be_Rejected()
    {
        var actual = _validator.Validate(FixMessageFactory.New(quantity: null));
        AssertFailure(actual, ErrorCode.MissingOrderQuantity, OrderQty.TAG);
    }

    [Fact]
    public void Non_Positive_Quantity_Must_Be_Rejected()
    {
        var actual1 = _validator.Validate(FixMessageFactory.New(quantity: 0m));
        AssertFailure(actual1, ErrorCode.NonPositiveOrderQuantity, OrderQty.TAG);

        var actual2 = _validator.Validate(FixMessageFactory.New(quantity: -10m));
        AssertFailure(actual2, ErrorCode.NonPositiveOrderQuantity, OrderQty.TAG);
    }

    [Fact]
    public void Fractional_Quantity_Must_Be_Rejected()
    {
        var actual = _validator.Validate(FixMessageFactory.New(quantity: 10.5m));
        AssertFailure(actual, ErrorCode.FractionalOrderQuantity, OrderQty.TAG);
    }

    [Fact]
    public void Quantity_Larger_Than_Long_Must_Be_Rejected()
    {
        var actual = _validator.Validate(FixMessageFactory.New(quantity: decimal.MaxValue));
        AssertFailure(actual, ErrorCode.OrderQuantityTooLarge, OrderQty.TAG);
    }

    [Fact]
    public void Quantity_Not_Aligned_To_Lot_Must_Be_Rejected()
    {
        var actual = _validator.Validate(FixMessageFactory.New(quantity: 101m));
        AssertFailure(actual, ErrorCode.OrderQuantityNotLotAligned, OrderQty.TAG);
    }
    
    [Fact]
    public void Quantity_Above_Maximum_Be_Rejected()
    {
        var actual = _validator.Validate(FixMessageFactory.New(quantity: 100100m));
        AssertFailure(actual, ErrorCode.OrderQuantityExceedsMaximum, OrderQty.TAG);
    }
    
    [Fact]
    public void Market_With_Price_Must_Be_Rejected()
    {
        var actual = _validator.Validate(FixMessageFactory.New(orderType: OrdType.MARKET, price: 300m));
        AssertFailure(actual, ErrorCode.UnexpectedPrice, Price.TAG);
    }
    
    [Fact]
    public void Limit_Without_Price_Must_Be_Rejected()
    {
        var actual = _validator.Validate(FixMessageFactory.New(orderType: OrdType.LIMIT, price: null));
        AssertFailure(actual, ErrorCode.MissingPrice, Price.TAG);
    }
    
    [Fact]
    public void Non_Positive_Price_Must_Be_Rejected()
    {
        var actual1 = _validator.Validate(FixMessageFactory.New(orderType: OrdType.LIMIT, price: 0));
        AssertFailure(actual1, ErrorCode.NonPositivePrice, Price.TAG);
        
        var actual2 = _validator.Validate(FixMessageFactory.New(orderType: OrdType.LIMIT, price: -0.01m));
        AssertFailure(actual2, ErrorCode.NonPositivePrice, Price.TAG);
    }
    
    [Fact]
    public void Non_Tick_Aligned_Price_Must_Be_Rejected()
    {
        var actual = _validator.Validate(FixMessageFactory.New(orderType: OrdType.LIMIT, price: 300.001m));
        AssertFailure(actual, ErrorCode.PriceNotTickAligned, Price.TAG);
    }
    
    [Fact]
    public void Price_Outside_Instrument_Band_Must_Be_Rejected()
    {
        var actual1 = _validator.Validate(FixMessageFactory.New(orderType: OrdType.LIMIT, price: 299.01m));
        AssertFailure(actual1, ErrorCode.PriceOutsideBand, Price.TAG);
        
        var actual2 = _validator.Validate(FixMessageFactory.New(orderType: OrdType.LIMIT, price: 360.01m));
        AssertFailure(actual2, ErrorCode.PriceOutsideBand, Price.TAG);
        
        var actual3 = _validator.Validate(FixMessageFactory.New(orderType: OrdType.LIMIT, price: decimal.MaxValue));
        AssertFailure(actual3, ErrorCode.PriceOutsideBand, Price.TAG);
    }
    
    private static void AssertSuccess(ValidationResult actual)
    {
        Assert.True(actual.IsValid);
        Assert.Null(actual.ErrorCode);
        Assert.Null(actual.RefTagId);
        Assert.True(string.IsNullOrEmpty(actual.ErrorMessage));
    }

    private static void AssertFailure(ValidationResult actual, ErrorCode expectedErrorCode, int expectedRefTagId)
    {
        Assert.False(actual.IsValid);
        Assert.Equal(expectedErrorCode, actual.ErrorCode);
        Assert.Equal(expectedRefTagId, actual.RefTagId);
        Assert.False(string.IsNullOrEmpty(actual.ErrorMessage));
    }
}