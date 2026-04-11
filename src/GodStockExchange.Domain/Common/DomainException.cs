namespace GodStockExchange.Domain.Common;

/// <summary>
/// Base class for all exceptions dedicated to business logic errors.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}