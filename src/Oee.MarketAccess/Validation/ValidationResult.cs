namespace Oee.MarketAccess.Validation;

public sealed record ValidationResult
{
    public bool IsValid { get; }

    /// <summary>
    /// Only present when <see cref="IsValid"/> is <c>false</c>.
    /// </summary>
    public ErrorCode? ErrorCode { get; }

    /// <summary>
    /// Only present when <see cref="IsValid"/> is <c>false</c>.
    /// </summary>
    public int? RefTagId { get; }

    /// <summary>
    /// Only present when <see cref="IsValid"/> is <c>false</c>.
    /// </summary>
    public string? ErrorMessage { get; }

    private ValidationResult(bool isValid, ErrorCode? errorCode, int? refTagId, string? errorMessage)
    {
        IsValid = isValid;
        ErrorCode = errorCode;
        RefTagId = refTagId;
        ErrorMessage = errorMessage;
    }

    public static ValidationResult Valid() => new(true, null, null, null);

    public static ValidationResult Invalid(ErrorCode errorCode, int refTagId, string errorMessage)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(refTagId);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new ValidationResult(false, errorCode, refTagId, errorMessage);
    }
}