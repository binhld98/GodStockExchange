using GodStockExchange.Domain.Common;
using GodStockExchange.Domain.Enums;

namespace GodStockExchange.Domain.Models;

/// <summary>
/// Represents a trading session for a specific instrument.
/// </summary>
public struct TradingSession
{
    /// <summary>
    /// Exchange-assigned unique identifier for the trading session.
    /// </summary>
    public long TradingSessionId { get; }

    public Instrument Instrument { get; }

    public MarketPhase CurrentPhase { get; private set;}

    public long LastChangedAtNs { get; private set; }

    private readonly Dictionary<MarketPhase, MarketPhase[]> _allowedTransitions = new()
    {
        [MarketPhase.Closed] = [MarketPhase.PreOpen, MarketPhase.Halted],
        [MarketPhase.PreOpen] = [MarketPhase.Open, MarketPhase.Halted],
        [MarketPhase.Open] = [MarketPhase.PreClose, MarketPhase.Halted],
        [MarketPhase.PreClose] = [MarketPhase.Closed, MarketPhase.Halted],
        [MarketPhase.Halted] = [MarketPhase.Open, MarketPhase.Closed, MarketPhase.Suspended],
        [MarketPhase.Suspended] = [],
    };

    public TradingSession(long tradingSessionId, Instrument instrument, MarketPhase initialPhase, long lastChangedAtNs)
    {
        Guard.NonNegative(tradingSessionId, nameof(tradingSessionId));
        Guard.NonNegative(lastChangedAtNs, nameof(lastChangedAtNs));

        TradingSessionId = tradingSessionId;
        Instrument = instrument;
        CurrentPhase = initialPhase;
        LastChangedAtNs = lastChangedAtNs;
    }

    public void TransitionTo(MarketPhase newPhase, long changedAtNs)
    {
        var allowed = _allowedTransitions[CurrentPhase];
        if (!allowed.Contains(newPhase))
        {
            string allowedStr = allowed.Length > 0 ? string.Join(", ", allowed) : "None";
            throw new DomainException($"Invalid market phase transition from {CurrentPhase} to {newPhase}. Allowed transitions: {allowedStr}");
        }

        CurrentPhase = newPhase;
        LastChangedAtNs = changedAtNs;
    }

    public void TransitionTo(MarketPhase newPhase)
        => TransitionTo(newPhase, DateHelper.GetCurrentTimestampNs());

    public readonly bool IsCurrentPhaseCompatibleWith(AuctionConstraint auction)
        => auction switch
        {
            AuctionConstraint.None => CurrentPhase == MarketPhase.Open,
            AuctionConstraint.AtOpen => CurrentPhase == MarketPhase.PreOpen,
            AuctionConstraint.AtClose => CurrentPhase == MarketPhase.PreClose,
            _ => false,
        };
}