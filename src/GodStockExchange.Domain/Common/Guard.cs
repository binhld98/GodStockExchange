using System.Runtime.CompilerServices;

namespace GodStockExchange.Domain.Common;

/// <summary>
/// Lightweight static helpers for enforcing domain invariants inline.
/// </summary>
public static class Guard
{
    
    /// <summary>
    /// Throws a <see cref="DomainException"/> when <paramref name="value"/> is less than or equal to zero.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="name"></param>
    /// <exception cref="DomainException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Positive(long value, string name)
    {
        if (value <= 0)
            throw new DomainException($"{name} must be positive. Got {value}.");
    }

    /// <summary>
    /// Throws a <see cref="DomainException"/> when <paramref name="value"/> is less than or equal to zero.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="name"></param>
    /// <exception cref="DomainException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Positive(decimal value, string name)
    {
        if (value <= 0)
            throw new DomainException($"{name} must be positive. Got {value}.");
    }

    /// <summary>
    /// Throws a <see cref="DomainException"/> when <paramref name="value"/> is less than zero.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="name"></param>
    /// <exception cref="DomainException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NonNegative(long value, string name)
    {
        if (value < 0)
            throw new DomainException($"{name} must be non-negative. Got {value}.");
    }

    /// <summary>
    /// Throws a <see cref="DomainException"/> when <paramref name="value"/> is less than zero.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="name"></param>
    /// <exception cref="DomainException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NonNegative(decimal value, string name)
    {
        if (value < 0)
            throw new DomainException($"{name} must be non-negative. Got {value}.");
    }

    /// <summary>
    /// Throws a <see cref="DomainException"/> when <paramref name="value"/> or <paramref name="factor"/> is less than or equal to zero, or <paramref name="value"/> is not a multiple of <paramref name="factor"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="factor"></param>
    /// <param name="name"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PositiveAndMultipleOf(long value, long factor, string name)
    {
        if (value <= 0)
            throw new DomainException($"{name} must be positive. Got {value}.");
        
        if (factor <= 0)
            throw new DomainException($"Factor must be positive. Got {factor}.");
        
        if (value % factor != 0)
            throw new DomainException($"{name} must be a multiple of {factor}. Got {value}.");
    }

    /// <summary>
    /// Throws a <see cref="DomainException"/> when <paramref name="value"/> or <paramref name="factor"/> is less than or equal to zero,
    /// or <paramref name="value"/> is not a multiple of <paramref name="factor"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="factor"></param>
    /// <param name="name"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PositiveAndMultipleOf(decimal value, decimal factor, string name)
    {
        if (value <= 0)
            throw new DomainException($"{name} must be positive. Got {value}.");
        
        if (factor <= 0)
            throw new DomainException($"Factor must be positive. Got {factor}.");
        
        if (value % factor != 0)
            throw new DomainException($"{name} must be a multiple of {factor}. Got {value}.");
    }

    /// <summary>
    /// Throws a <see cref="DomainException"/> when <paramref name="value"/> is null, empty, or contains only whitespace.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="name"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotEmpty(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{name} must not be empty.");
    }

    /// <summary>
    /// Throws a <see cref="DomainException"/> when <paramref name="condition"/> is false.
    /// </summary>
    /// <param name="condition"></param>
    /// <param name="message"></param>
    /// <exception cref="DomainException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Requires(bool condition, string message)
    {
        if (!condition)
            throw new DomainException(message);
    }
}