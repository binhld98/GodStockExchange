using Oee.MarketAccess.Catalog;
using QuickFix.Fields;
using QuickFix.FIX44;

namespace Oee.MarketAccess.Validation;

public sealed class Validator : IValidator<Message>
{
    private readonly IInstrumentCatalog _catalog;

    public Validator(IInstrumentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    public ValidationResult Validate(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.Header.GetString(MsgType.TAG) switch
        {
            MsgType.NEWORDERSINGLE => ValidateNew(message),
            MsgType.ORDERCANCELREPLACEREQUEST => ValidateCancelReplace(message),
            MsgType.ORDERCANCELREQUEST => ValidateCancel(message),
            MsgType.ORDERSTATUSREQUEST => ValidateStatus(message),
            _ => throw new NotImplementedException(),
        };
    }

    private ValidationResult ValidateNew(Message message)
    {
        ValidationResult? failure = null;

        failure = ValidateSide(message);
        if (failure is not null) return failure;

        failure = ValidateOrderType(message);
        if (failure is not null) return failure;

        failure = ValidateTimeInForce(message);
        if (failure is not null) return failure;

        failure = ValidateInstrument(message, out var profile);
        if (failure is not null) return failure;

        failure = ValidateQuantity(message, profile);
        if (failure is not null) return failure;

        failure = ValidatePrice(message, profile);
        if (failure is not null) return failure;

        return ValidationResult.Valid();
    }
    
    private ValidationResult ValidateCancelReplace(Message message)
    {
        ValidationResult? failure = null;

        failure = ValidateSide(message);
        if (failure is not null) return failure;

        failure = ValidateOrderType(message);
        if (failure is not null) return failure;

        failure = ValidateTimeInForce(message);
        if (failure is not null) return failure;

        failure = ValidateInstrument(message, out var profile);
        if (failure is not null) return failure;

        failure = ValidateQuantity(message, profile);
        if (failure is not null) return failure;

        failure = ValidatePrice(message, profile);
        if (failure is not null) return failure;

        return ValidationResult.Valid();
    }

    private ValidationResult ValidateCancel(Message message)
    {
        ValidationResult? failure = null;
        failure = ValidateSide(message);
        if (failure is not null) return failure;

        failure = ValidateInstrument(message, out _);
        if (failure is not null) return failure;

        return ValidationResult.Valid();
    }
    
    private ValidationResult ValidateStatus(Message message)
    {
        ValidationResult? failure = null;
        failure = ValidateSide(message);
        if (failure is not null) return failure;

        failure = ValidateInstrument(message, out _);
        if (failure is not null) return failure;

        return ValidationResult.Valid();
    }

    #region Validation Rules

    private ValidationResult? ValidateSide(Message message)
    {
        if (!message.IsSetField(Side.TAG))
            return ValidationResult.Invalid(ErrorCode.MissingSide, Side.TAG, "Side is required.");

        return message.GetChar(Side.TAG) is Side.BUY or Side.SELL
            ? null
            : ValidationResult.Invalid(ErrorCode.UnsupportedSide, Side.TAG, "Side is not supported.");
    }

    private ValidationResult? ValidateOrderType(Message message)
    {
        if (!message.IsSetField(OrdType.TAG))
            return ValidationResult.Invalid(ErrorCode.MissingOrderType, OrdType.TAG, "OrdType is required.");

        return message.GetChar(OrdType.TAG) is OrdType.MARKET or OrdType.LIMIT
            ? null
            : ValidationResult.Invalid(ErrorCode.UnsupportedOrderType, OrdType.TAG, "Order type is not supported.");
    }

    private ValidationResult? ValidateTimeInForce(Message message)
    {
        if (!message.IsSetField(TimeInForce.TAG))
            return ValidationResult.Invalid(ErrorCode.MissingTimeInForce, TimeInForce.TAG, "TimeInForce is required.");

        return message.GetChar(TimeInForce.TAG) is TimeInForce.DAY or TimeInForce.GOOD_TILL_CANCEL
            or TimeInForce.IMMEDIATE_OR_CANCEL or TimeInForce.FILL_OR_KILL
            ? null
            : ValidationResult.Invalid(ErrorCode.UnsupportedTimeInForce, TimeInForce.TAG,
                "TimeInForce is not supported.");
    }

    private ValidationResult? ValidateInstrument(Message message, out SimpleInstrumentProfile profile)
    {
        profile = default;
        if (!message.IsSetField(Symbol.TAG))
            return ValidationResult.Invalid(ErrorCode.MissingSymbol, Symbol.TAG, "Symbol is required.");

        if (!_catalog.TryGetBySymbol(message.GetString(Symbol.TAG), out profile))
            return ValidationResult.Invalid(ErrorCode.UnknownInstrument, Symbol.TAG, "Symbol is not known.");

        if (!profile.IsTradeable)
            return ValidationResult.Invalid(ErrorCode.InstrumentNotTradeable, Symbol.TAG,
                "Instrument is not tradable.");

        return null;
    }

    private ValidationResult? ValidateQuantity(QuickFix.Message message, SimpleInstrumentProfile profile)
    {
        if (message.IsSetField(CashOrderQty.TAG))
            return ValidationResult.Invalid(ErrorCode.UnsupportedQuantityRepresentation, CashOrderQty.TAG,
                "CashOrderQty is not supported.");

        if (message.IsSetField(OrderPercent.TAG))
            return ValidationResult.Invalid(ErrorCode.UnsupportedQuantityRepresentation, OrderPercent.TAG,
                "OrderPercent is not supported.");

        if (!message.IsSetField(OrderQty.TAG))
            return ValidationResult.Invalid(ErrorCode.MissingOrderQuantity, OrderQty.TAG, "OrderQty is required.");

        decimal qtyM = message.GetDecimal(OrderQty.TAG);

        if (qtyM <= 0)
            return ValidationResult.Invalid(ErrorCode.NonPositiveOrderQuantity, OrderQty.TAG,
                "OrderQty must be greater than zero.");

        if (qtyM != decimal.Truncate(qtyM))
            return ValidationResult.Invalid(ErrorCode.FractionalOrderQuantity, OrderQty.TAG,
                "OrderQty must be a whole number.");

        if (qtyM > long.MaxValue)
            return ValidationResult.Invalid(ErrorCode.OrderQuantityTooLarge, OrderQty.TAG, "OrderQty is too large.");

        long qtyL = (long)qtyM;

        if (qtyL % profile.LotSize != 0)
            return ValidationResult.Invalid(ErrorCode.OrderQuantityNotLotAligned, OrderQty.TAG,
                "OrderQty is not lot aligned.");

        if (qtyL / profile.LotSize > profile.MaxLots)
            return ValidationResult.Invalid(ErrorCode.OrderQuantityExceedsMaximum, OrderQty.TAG,
                "OrderQty exceeds instrument maximum.");

        return null;
    }

    private ValidationResult? ValidatePrice(Message message, SimpleInstrumentProfile profile)
    {
        if (message.GetChar(OrdType.TAG) is OrdType.MARKET)
        {
            if (message.IsSetField(Price.TAG))
                return ValidationResult.Invalid(ErrorCode.UnexpectedPrice, Price.TAG,
                    "Price is not allowed for Market orders.");

            return null;
        }

        if (message.GetChar(OrdType.TAG) is OrdType.LIMIT)
        {
            if (!message.IsSetField(Price.TAG))
                return ValidationResult.Invalid(ErrorCode.MissingPrice, Price.TAG,
                    "Price is required for Limit orders.");

            decimal priceM = message.GetDecimal(Price.TAG);

            if (priceM <= 0)
                return ValidationResult.Invalid(ErrorCode.NonPositivePrice, Price.TAG,
                    "Price must be greater than or equal to zero.");

            if (priceM % profile.TickSize != 0)
                return ValidationResult.Invalid(ErrorCode.PriceNotTickAligned, Price.TAG, "Price is not tick aligned.");

            decimal priceTickM;
            try
            {
                priceTickM = priceM / profile.TickSize;
            }
            catch (OverflowException)
            {
                return ValidationResult.Invalid(ErrorCode.PriceOutsideBand, Price.TAG,
                    "Price is outside the permitted band.");
            }

            if (priceTickM < profile.PriceBandFloorTicks || priceTickM > profile.PriceBandCeilingTicks)
                return ValidationResult.Invalid(ErrorCode.PriceOutsideBand, Price.TAG,
                    "Price is outside the permitted band.");

            return null;
        }

        throw new NotSupportedException();
    }

    #endregion
}