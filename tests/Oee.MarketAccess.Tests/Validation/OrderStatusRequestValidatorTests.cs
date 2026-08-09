using Oee.MarketAccess.Tests.Catalog;
using Oee.MarketAccess.Validation;
using QuickFix.Fields;

namespace Oee.MarketAccess.Tests.Validation;

public class OrderStatusRequestValidatorTests
{
    private readonly Validator _validator;

    public OrderStatusRequestValidatorTests()
    {
        var catalog = IgnoreCaseInstrumentCatalog.New_With_Tradeable_AAPL_And_Untradeable_NVDA();
        _validator = new Validator(catalog);
    }
    
    [Theory]
    [InlineData(Side.BUY)]
    [InlineData(Side.SELL)]
    public void Every_Supported_Side_Must_Be_Accepted(char side)
    {
        var actual = _validator.Validate(FixMessageFactory.Status(side: side));
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
        var actual = _validator.Validate(FixMessageFactory.Status(side: side));
        AssertFailure(actual, ErrorCode.UnsupportedSide, Side.TAG);
    }
    
    [Fact]
    public void Unknown_Symbol_Must_Be_Rejected()
    {
        var actual = _validator.Validate(FixMessageFactory.Status(symbol: "!@#$"));
        AssertFailure(actual, ErrorCode.UnknownInstrument, Symbol.TAG);
    }

    [Fact]
    public void Untradable_Instrument_Must_Be_Rejected()
    {
        var actual = _validator.Validate(FixMessageFactory.Status(symbol: "NVDA"));
        AssertFailure(actual, ErrorCode.InstrumentNotTradeable, Symbol.TAG);
    }
    
    private static void AssertFailure(ValidationResult actualResult, ErrorCode expectedErrorCode, int expectedRefTagId)
    {
        Assert.False(actualResult.IsValid);
        Assert.Equal(expectedErrorCode, actualResult.ErrorCode);
        Assert.Equal(expectedRefTagId, actualResult.RefTagId);
        Assert.False(string.IsNullOrEmpty(actualResult.ErrorMessage));
    }

    private static void AssertSuccess(ValidationResult actualResult)
    {
        Assert.True(actualResult.IsValid);
        Assert.Null(actualResult.ErrorCode);
        Assert.Null(actualResult.RefTagId);
        Assert.True(string.IsNullOrEmpty(actualResult.ErrorMessage));
    }
}