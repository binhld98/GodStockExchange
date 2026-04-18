namespace GodStockExchange.MatchingEngine.DataStructures;

/// <summary>
/// Represents an integer index with a well-defined "null" value.
/// </summary>
public readonly struct Index
{
    /// <summary>
    /// The integer value of this index.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// A sentinel value representing a null index.
    /// </summary>
    public static readonly Index NullIndex = new(-1);

    public Index(int value)
    {
        Value = value;
    }

    /// <summary>
    /// Implicit converts an <see cref="Index"/> to an <see cref="int"/>.
    /// </summary>
    /// <param name="index"></param>
    public static implicit operator int(Index index) => index.Value;

    /// <summary>
    /// Implicit converts an <see cref="int"/> to an <see cref="Index"/>.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator Index(int value) => new(value);
}