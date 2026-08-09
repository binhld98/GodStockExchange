namespace Oee.MarketAccess.Validation;

public interface IValidator<TMessage>
{
    /// <summary>
    /// Apply fail-fast checks against the messsage.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    ValidationResult Validate(TMessage message);
}