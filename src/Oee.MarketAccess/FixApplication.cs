
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;

namespace Oee.MarketAcess;

public sealed class FixApplication : MessageCracker, IApplication
{
    private readonly ILogger<FixApplication> _logger;

    public FixApplication(ILogger<FixApplication> logger)
    {
        _logger = logger;
    }

    public void OnCreate(SessionID sessionID) { }

    public void OnLogon(SessionID sessionID) { }

    public void OnLogout(SessionID sessionID) { }

    public void FromAdmin(Message message, SessionID sessionID) { }

    public void ToAdmin(Message message, SessionID sessionID) { }

    public void FromApp(Message message, SessionID sessionID)
    {
        try
        {
            Crack(message, sessionID);
        }
        catch (UnsupportedMessageType)
        {
            _logger.LogWarning("Received unsupported FIX message type: MsgType={0} SessionID={1}", message.Header.GetString(Tags.MsgType), sessionID);
            throw;
        }
    }

    public void ToApp(Message message, SessionID sessionID) { }

    public void OnMessage(QuickFix.FIX44.NewOrderSingle message, SessionID sessionID)
    {
        _logger.LogDebug("Received NewOrderSingle: SessionID={0}", sessionID);

        // TODO: perform stateless validation for NewOrderSingle
    }

    public void OnMessage(QuickFix.FIX44.OrderCancelRequest message, SessionID sessionID)
    {
        _logger.LogDebug("Received OrderCancelRequest: SessionID={0}", sessionID);

        // TODO: perform stateless validation for OrderCancelRequest
    }

    public void OnMessage(QuickFix.FIX44.OrderCancelReplaceRequest message, SessionID sessionID)
    {
        _logger.LogDebug("Received OrderCancelReplaceRequest: SessionID={0}", sessionID);

        // TODO: perform stateless validation for OrderCancelReplaceRequest
    }

    public void OnMessage(QuickFix.FIX44.OrderStatusRequest message, SessionID sessionID)
    {
        _logger.LogDebug("Received OrderStatusRequest: SessionID={0}", sessionID);

        // TODO: perform stateless validation for OrderStatusRequest
    }
}