namespace GodStockExchange.Domain.Enums;

public enum MarketPhase : byte
{
    /// <summary>
    /// PreOpen call auction phase.
    /// </summary>
    PreOpen = 0,

    /// <summary>
    /// Continuous trading phase.
    /// </summary>
    Open = 1,

    /// <summary>
    /// PreClose call auction phase.
    /// </summary>
    PreClose = 2,

    /// <summary>
    /// Closed trading phase.
    /// </summary>
    Closed = 3,

    /// <summary>
    /// Halted phase, trading is temporarily suspended due to a circuit-breaker, regulatory action, or operational incident.
    /// Resting orders are preserved, but new orders are rejected.
    /// </summary>
    Halted = 4,

    /// <summary>
    /// Suspended phase, the instrument is permanently suspended or delisted from trading.
    /// Resting orders are cancelled, and new orders are rejected.
    /// </summary>
    Suspended = 5,
}