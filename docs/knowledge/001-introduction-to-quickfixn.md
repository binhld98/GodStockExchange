# Introduction to QuickFIX/n

# OpenEquityExchange (OEE) – Market Access Layer (MAL)

**Version:** 1.1<br>
**Status:** Active<br>
**Author:** BinhLD<br>
**Date:** July 2026

---

## OVERVIEW

QuickFIX/n is a free, open-source, native C# implementation of the FIX (Financial Information eXchange) protocol for .NET. It is a C# port of the original C++ QuickFIX engine, maintained by Connamara Systems as part of the broader QuickFIX project.

QuickFIX/n implements the FIX session layer — logon, heartbeat, sequence number management, gap fill, and resend — leaving application code primarily responsible for business-level message processing. It handles TCP connectivity, message framing, parsing, checksum validation, sequence numbering, heartbeating, and session recovery.

**Key properties:**

| Property              | Description                                        |
| --------------------- | -------------------------------------------------- |
| Language              | C# / .NET                                          |
| License               | QuickFIX Software License (permissive open-source) |
| Repository            | github.com/connamara/quickfixn                     |
| Core NuGet package    | `QuickFIXn.Core`                                   |
| FIX 4.4 NuGet package | `QuickFIXn.FIX44`                                  |

Since FIX 4.4 is the only version OEE's MAL supports, the project references `QuickFIXn.FIX44` version 1.14.1:

```xml
<!-- FIX 4.4 message definitions -->
<PackageReference Include="QuickFIXn.FIX44" Version="1.14.1" />
```

> **Note on multi-version support:** QuickFIX/n's engine can host multiple FIX versions within a single process, each in an isolated session with its own sequence space and data dictionary. OEE does not use this capability today — FIX 4.4 is the exclusive target for MAL.

---

## 1. FIX PROTOCOL FUNDAMENTALS

### 1.1 What Is FIX?

The Financial Information eXchange (FIX) protocol is the industry-standard messaging format for electronic securities transactions. It defines how participants exchange orders, executions, and market data over a TCP connection.

A FIX message is a sequence of `tag=value` pairs separated by the SOH control character (`0x01`), structured into three sections: **Header**, **Body**, **Trailer**.

### 1.2 Message Structure

**Header** — mandatory fields present on every message:

| Tag | Field        | Description                              |
| --- | ------------ | ---------------------------------------- |
| 8   | BeginString  | FIX version, e.g. `FIX.4.4`              |
| 9   | BodyLength   | Byte count of the body; auto-computed    |
| 35  | MsgType      | Message type, e.g. `D` = NewOrderSingle  |
| 49  | SenderCompID | ID of the sending party                  |
| 56  | TargetCompID | ID of the intended recipient             |
| 34  | MsgSeqNum    | Monotonically increasing sequence number |
| 52  | SendingTime  | UTC timestamp of transmission            |

**Body** — fields specific to the message type.

**Trailer** — always ends with the checksum:

| Tag | Field    | Description                                                      |
| --- | -------- | ---------------------------------------------------------------- |
| 10  | CheckSum | Three-digit modulo-256 sum of all preceding bytes; auto-computed |

> QuickFIX/n populates the engine-managed header and trailer fields listed above. Application code sets body fields and may add optional header fields through the `IApplication` callbacks when required.

### 1.3 Sessions and Sequence Numbers

A FIX session is uniquely identified by the triple `(BeginString, SenderCompID, TargetCompID)`. Every message carries `MsgSeqNum (34)`, incrementing by 1 per message sent; each side tracks its own outbound counter independently.

Sequence integrity is fundamental to FIX reliability:

- If a receiver detects a gap, it sends a **ResendRequest** to obtain the missing messages.
- The sender replays the requested messages, or sends a **SequenceReset-GapFill** if the originals are unavailable.
- Sequence numbers may be reset at an agreed session boundary, commonly through `ResetSeqNumFlag (141)` during Logon. Continuous sessions with non-resetting sequence numbers are also supported.

### 1.4 Admin vs. Application Messages

| Category        | Description                                                   | Examples                                                                                                              |
| --------------- | ------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| **Admin**       | Session-level management; handled automatically by the engine | Logon (A), Logout (5), Heartbeat (0), TestRequest (1), ResendRequest (2), Reject (3), SequenceReset (4)               |
| **Application** | Business-level messages; delivered to application code        | NewOrderSingle (D), OrderCancelRequest (F), OrderCancelReplaceRequest (G), OrderCancelReject (9), ExecutionReport (8) |

---

## 2. QUICKFIX/N ARCHITECTURE

### 2.1 The Five Core Collaborators

QuickFIX/n is structured around five collaborating abstractions:

- **IApplication** — the single interface application code implements. It receives callbacks at every significant point in a session's lifecycle and for every incoming message; all business logic enters the system through this interface.
- **SessionSettings** — a configuration container parsed from the QuickFIX settings file. Holds the parameters for all sessions defined in that file and is passed to the transport endpoint at startup.
- **SocketAcceptor / SocketInitiator** — the two roles a FIX endpoint can occupy. An **acceptor** listens for inbound connections from FIX clients; an **initiator** establishes connections to a remote counterparty. OEE's MAL operates exclusively as an acceptor — specifically `ThreadedSocketAcceptor`, which dedicates one thread per client connection (see §10, Threading Model). Client trading systems connect to OEE as initiators.
- **IMessageStoreFactory** — a factory abstraction for message persistence. The store records outgoing messages and sequence numbers so the session layer can replay messages to a counterparty that issues a ResendRequest. Built-in implementations: `FileStoreFactory` (durable, on-disk) and `MemoryStoreFactory` (non-durable, in-memory).
- **ILogFactory** — a factory abstraction for diagnostic logging of raw FIX message traffic. Built-in implementations: `FileLogFactory`, `ScreenLogFactory`, and `NullLogFactory`.

These five collaborators are composed at startup:

```csharp
var settings = new SessionSettings("fix_gateway.cfg");
var application = new FixApplication();
var storeFactory = new FileStoreFactory(settings);
var logFactory = new FileLogFactory(settings);
var messageFactory = new DefaultMessageFactory();

var acceptor = new ThreadedSocketAcceptor(application, storeFactory, settings, logFactory, messageFactory);
acceptor.Start();
```

Once `Start()` is called, QuickFIX/n manages all TCP connections and session state. Application code receives engine events through `IApplication` callbacks and sends messages through `Session.SendToTarget()`.

### 2.2 Component Diagram

```text
┌──────────────────────────────────────────────────────────┐
│              Your Application Code                       │
│           (Implements IApplication)                      │
└──────────────────────────────────────────────────────────┘
                          │
┌──────────────────────────────────────────────────────────┐
│               QuickFIX/n Engine Core                     │
├──────────────────────────────────────────────────────────┤
│   SessionSettings → Session → MessageStore → Logger      │
│                         │                                │
│                  ThreadedSocketAcceptor                  │
└──────────────────────────────────────────────────────────┘
                          │ TCP
                Client FIX Initiators
```

### 2.3 Key Interfaces and Classes

| Class / Interface        | Role                                                                             |
| ------------------------ | -------------------------------------------------------------------------------- |
| `IApplication`           | Contract application code implements; receives all engine events and messages    |
| `SessionSettings`        | Parses the `.cfg` configuration file; holds all session parameters               |
| `ThreadedSocketAcceptor` | Accepts inbound TCP connections; manages one dedicated thread per client session |
| `SocketInitiator`        | Connects outbound to a counterparty; not used by OEE                             |
| `IMessageStoreFactory`   | Persists outbound messages for replay on resend requests                         |
| `FileStoreFactory`       | Built-in durable, file-based message store                                       |
| `MemoryStoreFactory`     | Built-in non-durable, in-memory message store                                    |
| `ILogFactory`            | Produces loggers for raw FIX traffic                                             |
| `FileLogFactory`         | Writes FIX logs to disk                                                          |
| `Session`                | Use its static `SendToTarget()` methods to send messages                         |
| `MessageCracker`         | Base class that dispatches `FromApp` callbacks to typed `OnMessage` overloads    |

### 2.4 Acceptor vs. Initiator

The FIX protocol defines two roles for nodes participating in a session:

| Role          | Description                                                                                                                                         |
| ------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Acceptor**  | Acts as the server. Listens on a pre-defined port, accepts incoming network connections, and authenticates client sessions via the Logon handshake. |
| **Initiator** | Acts as the client. Establishes the network connection and initiates the Logon handshake.                                                           |

Because OEE is an exchange, it always runs as an acceptor (`ThreadedSocketAcceptor`); client trading systems connect to it as initiators.

---

## 3. SESSION IDENTITY & CONFIGURATION

### 3.1 Session Identity

Every FIX session is uniquely identified by a `SessionID`, the composite of `BeginString` (FIX version, e.g. `FIX.4.4`), `SenderCompID` (local identity), and `TargetCompID` (remote counterparty identity), plus an optional sub-identity for multi-entity topologies.

For OEE's MAL, the combination of `BeginString` and `TargetCompID` is sufficient to uniquely identify a participant session. `SessionID` is used throughout QuickFIX/n as a key to look up sessions, route messages, and identify the subject session in every `IApplication` callback.

### 3.2 Configuration File Format

QuickFIX/n reads a plain-text settings file with a block-structured, INI-style format. A `[DEFAULT]` section provides values shared across all sessions; each `[SESSION]` block defines one FIX session and may override any default. **Setting names are case-sensitive.**

```ini
# OEE MAL - FIX 4.4 Acceptor

[DEFAULT]
ConnectionType=acceptor
FileStorePath=./store
FileLogPath=./logs
DataDictionary=FIX44.xml
BeginString=FIX.4.4

# Session schedule - reset at 00:00 UTC
StartTime=00:00:01
EndTime=23:59:59

# Timestamps at microsecond precision
TimestampPrecision=Microseconds

# TCP tuning - disable Nagle for lower latency
SocketNodelay=Y

# Enforce strict message validation
ValidateLengthAndChecksum=Y
ValidateFieldsOutOfOrder=Y
ValidateFieldsHaveValues=Y
UseDataDictionary=Y

# Sequence reset
ResetOnLogon=N
ResetOnLogout=N
ResetOnDisconnect=N

# Redact fields in logs
RedactFieldsInLogs=554

[SESSION]
SenderCompID=OEE_EXCHANGE
TargetCompID=CLIENT_A
SocketAcceptPort=5001

[SESSION]
SenderCompID=OEE_EXCHANGE
TargetCompID=CLIENT_B
SocketAcceptPort=5001
```

Multiple sessions can share the same `SocketAcceptPort`; QuickFIX/n differentiates them by the `(SenderCompID, TargetCompID)` pair in the Logon message. In multi-port configurations, `SocketAcceptPort` must be declared per-session rather than in `[DEFAULT]`.

To extend support for an additional FIX version later, add its message-definition package and a new `[SESSION]` block with the appropriate `BeginString` and `DataDictionary`; application handlers may also need version-specific overloads.

### 3.3 Core Parameters

- **ConnectionType** — `acceptor` or `initiator`. OEE's MAL always uses `acceptor`.
- **BeginString** — the FIX version string. OEE's MAL supports `FIX.4.4` only; other versions are out of scope for now.
- **DataDictionary** — path to the XML data dictionary for the session's FIX version; used to validate all incoming messages at runtime.
- **SenderCompID / TargetCompID** — form the session identity. On the acceptor side, `SenderCompID` is the exchange's identifier and `TargetCompID` is the participant's identifier.
- **SocketAcceptPort** — TCP port for the acceptor.
- **HeartBtInt** — heartbeat interval in seconds. This setting configures initiators; an acceptor uses the value supplied in the initiator's Logon.
- **FileStorePath / FileLogPath** — root directories for the file-based message store and FIX traffic log; each session gets its own prefixed files.
- **ResetOnLogon / ResetOnLogout / ResetOnDisconnect** — control whether sequence numbers reset to 1 on the corresponding event. Must be `N` in production to preserve sequence continuity across reconnections; may be `Y` in isolated test environments to avoid sequence desync between test runs.
- **StartTime / EndTime** — daily session window in UTC (`HH:MM:SS`) unless `TimeZone` is set. The acceptor rejects session traffic outside this window; use `NonStopSession=Y` instead of these settings for a continuously active session.
- **UseDataDictionary** — must remain `Y` in production; disabling it bypasses all field-level validation.
- **SocketNodelay** — enables `TCP_NODELAY` (disables Nagle's algorithm), trading increased packet count for lower latency.

### 3.4 Key Configuration Reference

**Session**

| Setting                 | Description                                                        | Default        |
| ----------------------- | ------------------------------------------------------------------ | -------------- |
| `BeginString`           | FIX version. Use `FIX.4.4` for OEE                                 | N/A            |
| `SenderCompID`          | This acceptor's identity                                           | N/A            |
| `TargetCompID`          | Expected client identity                                           | N/A            |
| `ConnectionType`        | `acceptor` or `initiator`                                          | N/A            |
| `StartTime` / `EndTime` | Session active window; UTC unless `TimeZone` is set                | N/A            |
| `HeartBtInt`            | Initiator heartbeat interval; acceptors use the value from Logon   | N/A            |
| `NonStopSession`        | `Y` = continuous session with no scheduled boundary                | `N`            |
| `ResetOnLogon`          | `Y` = reset sequence number to 1 on Logon                          | `N`            |
| `ResetOnLogout`         | `Y` = reset sequence number to 1 after a clean Logout              | `N`            |
| `ResetOnDisconnect`     | `Y` = reset sequence number to 1 after an abnormal disconnect      | `N`            |
| `RefreshOnLogon`        | `Y` = restore session state from the store on Logon (hot failover) | `N`            |
| `TimestampPrecision`    | `Seconds`, `Milliseconds`, `Microseconds`, or `Nanoseconds`        | `Milliseconds` |

**Validation**

| Setting                     | Description                                         | Default |
| --------------------------- | --------------------------------------------------- | ------- |
| `UseDataDictionary`         | `Y` = enable XML-based message validation           | `Y`     |
| `DataDictionary`            | Path to `FIX44.xml`                                 | N/A     |
| `ValidateLengthAndChecksum` | Reject messages with bad `BodyLength` or `CheckSum` | `Y`     |
| `ValidateFieldsOutOfOrder`  | Reject messages with fields in the wrong order      | `Y`     |
| `ValidateFieldsHaveValues`  | Reject messages with empty tag values               | `Y`     |
| `CheckLatency`              | Reject messages older than `MaxLatency`             | `Y`     |
| `MaxLatency`                | Maximum allowed message age, in seconds             | `120`   |

**Acceptor**

| Setting            | Description           | Default                    |
| ------------------ | --------------------- | -------------------------- |
| `SocketAcceptPort` | TCP port to listen on | N/A                        |
| `SocketAcceptHost` | Bind to a specific IP | `0.0.0.0` (all interfaces) |

**Storage**

| Setting           | Description                                                      | Default |
| ----------------- | ---------------------------------------------------------------- | ------- |
| `FileStorePath`   | Directory for sequence number and message files                  | N/A     |
| `PersistMessages` | `N` = don't persist messages; send GapFills instead of replaying | `Y`     |

**Socket Performance**

| Setting                   | Description                                                                   | Default |
| ------------------------- | ----------------------------------------------------------------------------- | ------- |
| `SocketNodelay`           | `Y` = disable Nagle's algorithm for lower latency; must be set in `[DEFAULT]` | `Y`     |
| `SocketSendBufferSize`    | TCP send buffer, in bytes                                                     | `8192`  |
| `SocketReceiveBufferSize` | TCP receive buffer, in bytes                                                  | `8192`  |

**Logging**

| Setting              | Description                                                          | Default |
| -------------------- | -------------------------------------------------------------------- | ------- |
| `FileLogPath`        | Directory for raw FIX message log files                              | N/A     |
| `RedactFieldsInLogs` | Comma-separated tag numbers to redact in logs, e.g. `554` (Password) | N/A     |

---

## 4. THE IAPPLICATION INTERFACE

`IApplication` defines seven callbacks. Application code must implement all seven, even when a callback is a no-op for a given implementation.

```csharp
public interface IApplication
{
    void OnCreate(SessionID sessionID);
    void OnLogon(SessionID sessionID);
    void OnLogout(SessionID sessionID);
    void ToAdmin(Message message, SessionID sessionID);
    void FromAdmin(Message message, SessionID sessionID);
    void ToApp(Message message, SessionID sessionID);
    void FromApp(Message message, SessionID sessionID);
}
```

### 4.1 OnCreate

Fires when the session object is instantiated — during `ThreadedSocketAcceptor` construction for configured sessions, before `Start()` opens any listener. Use this to initialise per-session data structures keyed by `SessionID`.

### 4.2 OnLogon

Fires when a logon handshake completes successfully; the signal that the session is active and application messages may be sent. Application code should gate outbound business messages on having received this callback for the target session.

### 4.3 OnLogout

Fires when a session ends — locally initiated, by the counterparty, or due to network failure. Application code should stop sending business messages until `OnLogon` fires again for that session.

### 4.4 ToAdmin

Fires immediately before the session layer sends an administrative message (Logon, Logout, Heartbeat, TestRequest, ResendRequest, SequenceReset, or Reject). Use this to inject fields into the outgoing message — most commonly, authentication fields on the outgoing Logon:

```csharp
public void ToAdmin(Message message, SessionID sessionID)
{
    if (message.Header.GetString(Tags.MsgType) != MsgType.LOGON)
        return;

    message.SetField(new Username("my-username"));
    message.SetField(new Password("my-password"));
}
```

### 4.5 FromAdmin

Fires when an administrative message arrives from the counterparty. Throwing `RejectLogon` here causes the session layer to reject the incoming Logon and close the connection — the correct extension point for participant credential validation:

```csharp
public void FromAdmin(Message message, SessionID sessionID)
{
    if (message.Header.GetString(Tags.MsgType) != MsgType.LOGON)
        return;

    var participantId = sessionID.TargetCompID;

    if (!_participantRegistry.IsActive(participantId))
        throw new RejectLogon($"Participant {participantId} is unknown or inactive");
}
```

`FromAdmin` also fires for Heartbeat, TestRequest, and other administrative messages; filter by `MsgType` to act only on the relevant types. Application code rarely needs to do work here — the engine handles admin messages automatically.

### 4.6 ToApp

Fires immediately before an outbound application-level message is sent. Throwing `DoNotSend` suppresses the message without causing a session error. Use this to stamp common fields on every outbound message.

### 4.7 FromApp

Fires when an inbound application-level message has been fully parsed and validated against the data dictionary — the primary entry point for business message processing. The exceptions `FromApp` is permitted to throw carry distinct protocol effects:

| Exception                | Protocol Effect                                            |
| ------------------------ | ---------------------------------------------------------- |
| `UnsupportedMessageType` | Sends BusinessMessageReject (MsgType=j)                    |
| `FieldNotFoundException` | Sends BusinessMessageReject (MsgType=j)                    |
| `IncorrectDataFormat`    | Sends session-level Reject (MsgType=3), bad format         |
| `IncorrectTagValue`      | Sends session-level Reject (MsgType=3), value out of range |

Application code should catch exceptions from downstream processing inside `FromApp` and translate protocol errors to one of the four exceptions above. Other failures should be logged and handled without escaping the callback; an unhandled exception terminates the client-handler thread and disconnects the client (see §12, Common Pitfalls).

### 4.8 Callback Ordering at Logon

When a FIX client connects, the acceptor's callbacks fire in strict order:

1. **`FromAdmin`** — delivers the client's incoming Logon to the application; throwing `RejectLogon` here aborts the handshake before any response is sent.
2. **`ToAdmin`** — fires with the acceptor's outgoing Logon response before transmission, letting the application inject additional fields.
3. **`OnLogon`** — fires last, once the outgoing Logon has been sent and the session is fully active. Application messages must not be sent before this fires.

### 4.9 Reference Implementation

```csharp
public class FixApplication : MessageCracker, IApplication
{
    public void OnCreate(SessionID sessionID) { /* initialise per-session state */ }

    public void OnLogon(SessionID sessionID) { /* mark session active; enable order flow */ }

    public void OnLogout(SessionID sessionID) { /* cancel open orders if required; release state */ }

    public void FromApp(Message msg, SessionID sessionID)
    {
        Crack(msg, sessionID); // dispatch to typed OnMessage overloads
    }

    public void FromAdmin(Message msg, SessionID sessionID) { }

    public void ToApp(Message msg, SessionID sessionID) { }

    public void ToAdmin(Message msg, SessionID sessionID)
    {
        if (msg is QuickFix.FIX44.Logon)
        {
            // e.g. message.SetField(new Password("secret"));
        }
    }
}
```

---

## 5. MESSAGE MODEL & FIELD ACCESS

### 5.1 Class Hierarchy

All FIX messages derive from `QuickFix.Message`, which itself derives from `QuickFix.FieldMap`. The `QuickFix.FIX44` namespace contains a strongly typed class for every FIX 4.4 message type:

```text
QuickFix.FieldMap
└── QuickFix.Message
    ├── QuickFix.FIX44.NewOrderSingle
    ├── QuickFix.FIX44.ExecutionReport
    ├── QuickFix.FIX44.OrderCancelRequest
    ├── QuickFix.FIX44.OrderCancelReplaceRequest
    ├── QuickFix.FIX44.OrderCancelReject
    ├── QuickFix.FIX44.OrderStatusRequest
    ├── QuickFix.FIX44.BusinessMessageReject
    └── ... (one class per FIX 4.4 MsgType)
```

A `Message` exposes three `FieldMap` regions:

- **Header** (`.Header`) — session-level routing fields present on every message.
- **Body** (the `Message` object itself, accessed directly) — business fields specific to the message type.
- **Trailer** (`.Trailer`) — the `CheckSum` (10) and optional signature fields.

### 5.2 Field Types

Every FIX tag corresponds to a strongly typed class in `QuickFix.Fields`, deriving from one of: `StringField`, `IntField`, `DecimalField`, `BooleanField`, `DateTimeField`, or `CharField`. Each field carries the tag number as a compile-time constant, with the typed value accessible via `.Value`.

### 5.3 Building Messages

Strongly typed constructors accept the fields the FIX 4.4 specification designates as required; optional fields are added via direct property assignment or the `.Set()` method on the strongly typed message class. For the generic message class, use `.SetField()` instead.

```csharp
var report = new QuickFix.FIX44.ExecutionReport(
    new OrderID("OEE-ORD-00001"),
    new ExecID("OEE-EXE-00001"),
    new ExecType(ExecType.NEW),
    new OrdStatus(OrdStatus.NEW),
    new Symbol("AAPL"),
    new Side(Side.BUY),
    new LeavesQty(500m),
    new CumQty(0m),
    new AvgPx(0m)
);

// Direct property assignment on the strongly typed message class
report.ClOrdID = new ClOrdID("CLI-ORD-00001");
report.TransactTime = new TransactTime(DateTime.UtcNow);

// Or the generated .Set() method on the strongly typed message class
report.Set(new LastQty(0m));

// On the general message class — using generic SetField() method
report.SetField(new LastPx(0m));
```

QuickFIX/n populates engine-managed header and trailer fields such as `BeginString`, `BodyLength`, `MsgSeqNum`, `SendingTime`, and `CheckSum`; application code should not set those fields manually.

### 5.4 Reading Fields

Two patterns exist for reading fields from an incoming message:

```csharp
// Pattern 1: direct access on the strongly typed message class (required fields)
var clOrdId = order.ClOrdID.Value;

// Pattern 2: conditional check before access (optional fields)

// - on the strongly typed message class
if (order.IsSetPrice())
{
    var price = order.Price.Value;
}

// - on the general message class
if (message.IsSetField(Tags.Price))
{
    decimal price = message.GetDecimal(Tags.Price);
}
```

Direct access is appropriate for required fields, since their absence indicates a malformed message that data dictionary validation should already have rejected before `FromApp` is called. Optional fields must always be guarded with `IsSetX()` or `IsSetField()` — direct access on an absent optional field throws `FieldNotFoundException`.

### 5.5 Reading Header Fields

Header fields such as `MsgType`, `MsgSeqNum`, and `SenderCompID` are accessed through the `Header` child map, not the message root:

```csharp
var msgType = message.Header.GetString(Tags.MsgType);
var seqNum = message.Header.GetULong(Tags.MsgSeqNum);
var sender = message.Header.GetString(Tags.SenderCompID);
```

The `Tags` class provides integer constants for all standard FIX tag numbers.

### 5.6 Repeating Groups

Repeating groups are accessed through strongly typed group classes nested within the parent message class; a delimiter field holds the count of group instances. **The group index passed to `GetGroup` is 1-based** — passing `0` throws `FieldNotFoundException`. The loop range for a group with N instances is `1` to `N` inclusive.

The `NoPartyIDs` group on `NewOrderSingle` is the standard mechanism for conveying participant identity alongside an order. Common `PartyRole` values relevant to an exchange:

| PartyRole      | Value | Meaning                           |
| -------------- | ----- | --------------------------------- |
| Executing Firm | 1     | The firm submitting the order     |
| Client ID      | 3     | The end client behind the order   |
| Clearing Firm  | 4     | The firm responsible for clearing |

Reading the parties group from an incoming `NewOrderSingle`:

```csharp
if (order.IsSetNoPartyIDs())
{
    var group = new QuickFix.FIX44.NewOrderSingle.NoPartyIDsGroup();
    int count = order.NoPartyIDs.Value;

    for (int i = 1; i <= count; i++)
    {
        order.GetGroup(i, group);
        var partyId = group.PartyID.Value;
        var partyRole = group.PartyRole.Value;
    }
}
```

Writing a parties group onto an outgoing `ExecutionReport`:

```csharp
var parties = new QuickFix.FIX44.ExecutionReport.NoPartyIDsGroup();
parties.Set(new PartyID("PARTY1"));
parties.Set(new PartyIDSource(PartyIDSource.PROPRIETARY));
parties.Set(new PartyRole(PartyRole.EXECUTING_FIRM));
report.AddGroup(parties);
```

---

## 6. THE MESSAGECRACKER PATTERN

`QuickFix.MessageCracker` is a base class implementing a visitor-style dispatch mechanism: it uses the `MsgType` field of an incoming message to route it to the correct typed `OnMessage(T, SessionID)` overload via reflection, eliminating manual switch statements on `MsgType`.

The pattern: inherit from `MessageCracker`, call `Crack(msg, sessionID)` inside `FromApp`, and implement overloaded `OnMessage` handlers for each message type requiring processing. If `Crack` encounters a `MsgType` with no matching overload, it throws `UnsupportedMessageType`, which QuickFIX/n automatically converts to a BusinessMessageReject (MsgType=j). This makes `MessageCracker` the recommended approach for receiving application messages.

```csharp
public class FixApplication : MessageCracker, IApplication
{
    public void FromApp(Message msg, SessionID sessionID)
    {
        try
        {
            Crack(msg, sessionID); // dispatch to typed OnMessage overloads
        }
        catch (UnsupportedMessageType)
        {
            throw; // log and rethrow
        }
    }

    public void OnMessage(QuickFix.FIX44.NewOrderSingle order, SessionID sessionID)
    {
        // Process new order
    }

    public void OnMessage(QuickFix.FIX44.OrderCancelRequest cancel, SessionID sessionID)
    {
        // Process cancel request
    }
}
```

---

## 7. SENDING MESSAGES & SESSION LIFECYCLE

### 7.1 Sending via Session.SendToTarget

Messages are sent through the static `Session.SendToTarget` methods, which provide thread-safe access to registered sessions:

```csharp
Session.SendToTarget(message, sessionID);
```

The call executes synchronously on the calling thread. QuickFIX/n assigns the outbound sequence number, invokes `ToApp`, serialises and persists the message, and writes it through the session's responder. It returns `true` when the responder accepts the send. It returns `false` when `DoNotSend` suppresses the message, no responder is attached, or the responder rejects the send. `SessionNotFound` is thrown only when the supplied `SessionID` is not registered.

### 7.2 Resolving a SessionID

`Session.SendToTarget` needs a `SessionID`. The recommended pattern for an acceptor is to cache it from `OnLogon`, keyed by `TargetCompID`, in a `ConcurrentDictionary` for thread-safe lookup from any consumer task:

```csharp
private readonly ConcurrentDictionary<string, SessionID> _sessions = new();

public void OnLogon(SessionID sessionID)
{
    _sessions[sessionID.TargetCompID] = sessionID;
}

public void OnLogout(SessionID sessionID)
{
    _sessions.TryRemove(sessionID.TargetCompID, out _);
}
```

### 7.3 Heartbeat and TestRequest

QuickFIX/n manages heartbeats autonomously. It sends a Heartbeat after an interval with no outbound traffic and a TestRequest after an interval with no inbound traffic. If the counterparty remains silent past the engine's timeout, the connection is closed; sending a Logout first is optional through `SendLogoutBeforeDisconnectFromTimeout`. Application code does not need to handle heartbeat responses — `FromAdmin` fires for incoming Heartbeat and TestRequest messages, but the session layer handles the protocol response.

### 7.4 Sequence Number Management

Each session maintains two independent counters: `NextSenderMsgSeqNum` (next outbound message) and `NextTargetMsgSeqNum` (next expected inbound message). On each received message, the session layer compares its `MsgSeqNum` against `NextTargetMsgSeqNum`:

| Received MsgSeqNum                | Session layer action                                                                |
| --------------------------------- | ----------------------------------------------------------------------------------- |
| Equals `NextTargetMsgSeqNum`      | Message accepted; counter advances by one                                           |
| Higher than `NextTargetMsgSeqNum` | Gap detected; ResendRequest sent for the missing range                              |
| Lower than `NextTargetMsgSeqNum`  | If `PossDupFlag=N`, Logout is sent; possible duplicates are validated and discarded |

`FileStoreFactory` persists both counters to disk. On reconnection with `ResetOnLogon=N`, the incoming Logon `MsgSeqNum` is compared with the stored `NextTargetMsgSeqNum`; a high value triggers a ResendRequest. QuickFIX/n replays stored outbound application messages when the counterparty sends its own ResendRequest.

### 7.5 Sequence Reset

A `SequenceReset` with `GapFillFlag=N` resets the sequence counter without replaying the gap. This bypasses the resend requirement and is a last-resort recovery tool — it must not be used during normal operation.

### 7.6 Logout

Either side may initiate logout by sending a Logout message. The receiving side sends a Logout acknowledgement and closes the connection. `OnLogout` fires on the receiving side after it sends the ack, and on the initiating side after it receives the ack.

---

## 8. EXECTYPE, ORDSTATUS & EXECUTIONREPORT

`ExecutionReport` is the primary message OEE sends to clients — it acknowledges order receipt, reports fills, and communicates cancellations. It uses two fields together to describe the full order state:

- `ExecType` — the event that triggered this report.
- `OrdStatus` — the current state of the order after that event.

These two fields must always be consistent; OEE's order management layer is responsible for maintaining valid state transitions and setting both fields correctly on every `ExecutionReport` it emits.

### 8.1 Common (ExecType, OrdStatus) Pairings

| Scenario              | ExecType           | OrdStatus                           |
| --------------------- | ------------------ | ----------------------------------- |
| Order accepted        | `NEW` (0)          | `NEW` (0)                           |
| Partial fill          | `TRADE` (F)        | `PARTIALLY_FILLED` (1)              |
| Full fill             | `TRADE` (F)        | `FILLED` (2)                        |
| Cancel confirmed      | `CANCELED` (4)     | `CANCELED` (4)                      |
| Amend confirmed       | `REPLACED` (5)     | `NEW` (0) or `PARTIALLY_FILLED` (1) |
| Order rejected        | `REJECTED` (8)     | `REJECTED` (8)                      |
| Expiry                | `EXPIRED` (C)      | `EXPIRED` (C)                       |
| Status query response | `ORDER_STATUS` (I) | Current order status                |

### 8.2 Building and Sending an ExecutionReport: Acknowledgment

```csharp
public void SendAck(SessionID sessionID, QuickFix.FIX44.NewOrderSingle order, string orderID)
{
    var report = new QuickFix.FIX44.ExecutionReport(
        new OrderID(orderID),
        new ExecID(Guid.NewGuid().ToString("N")),
        new ExecType(ExecType.NEW),
        new OrdStatus(OrdStatus.NEW),
        order.Symbol,
        order.Side,
        new LeavesQty(order.OrderQty.Value),
        new CumQty(0),
        new AvgPx(0)
    );

    report.ClOrdID = new ClOrdID(order.ClOrdID.Value);
    report.OrderQty = new OrderQty(order.OrderQty.Value);
    report.TransactTime = new TransactTime(DateTime.UtcNow);

    Session.SendToTarget(report, sessionID);
}
```

### 8.3 Building and Sending an ExecutionReport: Fill

```csharp
public void SendFill(SessionID sessionID, string clOrdID, string orderID, string symbol,
    char side, decimal fillQty, decimal fillPx, decimal leavesQty, decimal cumQty,
    decimal avgPx)
{
    var report = new QuickFix.FIX44.ExecutionReport(
        new OrderID(orderID),
        new ExecID(Guid.NewGuid().ToString("N")),
        new ExecType(ExecType.TRADE),
        new OrdStatus(leavesQty == 0m ? OrdStatus.FILLED : OrdStatus.PARTIALLY_FILLED),
        new Symbol(symbol),
        new Side(side),
        new LeavesQty(leavesQty),
        new CumQty(cumQty),
        new AvgPx(avgPx)
    );

    report.ClOrdID = new ClOrdID(clOrdID);
    report.LastQty = new LastQty(fillQty);
    report.LastPx = new LastPx(fillPx);
    report.TransactTime = new TransactTime(DateTime.UtcNow);

    Session.SendToTarget(report, sessionID);
}
```

> **Deprecated-value pitfall:** `PARTIAL_FILL` (1) and `FILL` (2) are deprecated `ExecType` values in FIX 4.4. Fill execution reports use `TRADE` (F) with the appropriate `OrdStatus`, as shown above. An order with `LeavesQty > 0` and `ExecType=TRADE` carries `OrdStatus=PARTIALLY_FILLED`; an order with `LeavesQty=0` carries `OrdStatus=FILLED`. Inconsistent pairs may be rejected by counterparty validation.

### 8.4 Sending an OrderCancelReject

When a cancel or amend request cannot be fulfilled, OEE responds with `OrderCancelReject` rather than `ExecutionReport`. `OrdStatus` reports the order's current status, while `CxlRejResponseTo` distinguishes whether the reject is in response to an `OrderCancelRequest` (`'1'`) or an `OrderCancelReplaceRequest` (`'2'`):

```csharp
public void SendCancelReject(SessionID sessionID, string clOrdID, string origClOrdID,
    string orderID, char ordStatus, char responseTo, string reason)
{
    var reject = new QuickFix.FIX44.OrderCancelReject(
        new OrderID(orderID),
        new ClOrdID(clOrdID),
        new OrigClOrdID(origClOrdID),
        new OrdStatus(ordStatus),
        new CxlRejResponseTo(responseTo)
    );

    reject.Text = new Text(reason);
    reject.TransactTime = new TransactTime(DateTime.UtcNow);

    Session.SendToTarget(reject, sessionID);
}
```

---

## 9. VALIDATION & REJECT HANDLING

### 9.1 Inbound Message Validation Scope

OEE accepts four inbound business message types from clients. Each requires a distinct rejection response when validation fails:

| Inbound Message             | Typical Validation                                                                                                                                                                                                                                                 | Reject Response                                                          |
| --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------ |
| `NewOrderSingle`            | **Stateless:** required fields present, instrument valid and tradeable, `OrdType`/`TimeInForce` supported, `Price`/`OrderQty` positive. **Stateful:** no duplicate `ClOrdID`, participant credit/position limits not breached, market open for trading.            | `ExecutionReport` with `ExecType=REJECTED` and `OrdStatus=REJECTED`      |
| `OrderCancelRequest`        | **Stateless:** required fields present, `OrigClOrdID` provided. **Stateful:** order exists and belongs to this participant, order is in a cancellable state (not fully filled, not already cancelled).                                                             | `OrderCancelReject` with `CxlRejResponseTo=ORDER_CANCEL_REQUEST`         |
| `OrderCancelReplaceRequest` | **Stateless:** required fields present, immutable fields `Symbol`/`Side` unchanged from original. **Stateful:** order exists and belongs to this participant, order is in an amendable state, new `OrderQty >= CumQty`, amended price/quantity within risk limits. | `OrderCancelReject` with `CxlRejResponseTo=ORDER_CANCEL_REPLACE_REQUEST` |
| `OrderStatusRequest`        | **Stateless:** required fields present (`ClOrdID`, `Symbol`, `Side`). **Stateful:** order exists and belongs to this participant.                                                                                                                                  | `BusinessMessageReject` with `BusinessRejectReason=UNKNOWN_ID`           |

### 9.2 Data Dictionary Validation

When `UseDataDictionary=Y`, QuickFIX/n validates every incoming message against the data dictionary before calling `FromApp`. Messages that fail validation are rejected at the session level automatically and never reach the application. Validation covers presence of required fields, correct field data types, valid enumerated values, and correct message structure including repeating groups.

### 9.3 Sending BusinessMessageReject Explicitly

Use `BusinessMessageReject` when application logic cannot process a structurally valid message and FIX defines no more specific response. For example, an unknown order referenced by `OrderStatusRequest` can be rejected with `BusinessRejectReason=UNKNOWN_ID`:

```csharp
public void OnMessage(QuickFix.FIX44.OrderStatusRequest request, SessionID sessionID)
{
    if (!_orderRegistry.Contains(request.ClOrdID.Value))
    {
        var reject = new QuickFix.FIX44.BusinessMessageReject(
            new RefMsgType(request.Header.GetString(Tags.MsgType)),
            new BusinessRejectReason(BusinessRejectReason.UNKNOWN_ID)
        );

        reject.RefSeqNum = new RefSeqNum(request.Header.GetULong(Tags.MsgSeqNum));
        reject.BusinessRejectRefID = new BusinessRejectRefID(request.ClOrdID.Value);
        reject.Text = new Text($"Unknown ClOrdID: {request.ClOrdID.Value}");
        Session.SendToTarget(reject, sessionID);
        return;
    }
    // continue processing
}
```

### 9.4 OrderCancelReject

See §8.4 above for a worked example. `CxlRejResponseTo` must correctly identify whether the reject responds to a cancel or a cancel/replace request.

### 9.5 Data Dictionaries

A data dictionary is an XML file declaring all valid message types, field tags, field types, and required/optional field designations for a given FIX version. QuickFIX/n uses it at runtime to validate incoming messages: admin messages are validated before `FromAdmin` fires; application messages before `FromApp` fires. Outbound messages are not subject to dictionary validation.

`FIX44.xml` is included in the `QuickFIXn.FIX44` NuGet package under `DataDictionary/`, but NuGet does not copy it to the application's output directory automatically. OEE keeps a copy in the project, declares it as build content with `CopyToOutputDirectory=PreserveNewest`, and references it through the `DataDictionary` setting. A missing or inaccessible dictionary causes QuickFIX/n to throw at startup.

Custom fields must use tag numbers in the user-defined range **5000–9999** to avoid conflicts with standardised FIX tags. They must also be declared in a modified copy of the standard dictionary — otherwise QuickFIX/n rejects any incoming message that contains them. The `ValidateUserDefinedFields` setting controls whether custom tags in this range are validated against the dictionary; setting it to `Y` enforces validation and is the correct production setting.

---

## 10. THREADING MODEL

QuickFIX/n's `ThreadedSocketAcceptor` creates a reactor for each listening endpoint and a dedicated `ClientHandlerThread` for each accepted client connection. An idle handler spends most of its time blocked on socket reads, but capacity should still be tested against the expected number of sessions and workload.

`OnCreate` fires while configured `Session` objects are created during acceptor construction, before `Start()` opens listeners. Inbound callbacks run on the connection's client-handler thread. Outbound callbacks such as `ToApp` run synchronously on the thread that calls `Session.SendToTarget`, and disconnect paths can invoke `OnLogout` from other threads. Callbacks must therefore be treated as concurrent.

Three rules follow from this model:

- **Protect shared state.** Multiple sessions and outbound callers can execute callbacks concurrently. Access mutable shared state through a concurrent collection, atomic operations, immutable data, or explicit synchronisation.
- **Keep inbound callbacks short.** A blocked `ClientHandlerThread` cannot read the next incoming message or respond to a TestRequest, which can cause the counterparty to time out and disconnect. Offload database calls, order processing, and downstream I/O to a worker such as a channel consumer.
- **Account for synchronous sends.** `Session.SendToTarget` is thread-safe but performs callback, persistence, serialisation, and responder work on the calling thread. Calling it from a consumer task keeps that work off the inbound session thread.

The recommended MAL pattern: receive messages in `FromApp`, enqueue them to a bounded `System.Threading.Channels.Channel`, and consume them on a dedicated task started at service initialisation. This isolates the session thread from all order-processing latency and provides natural backpressure.

---

## 11. PERSISTENCE, REPLAY & LOGGING

### 11.1 Message Store

With `PersistMessages=Y`, the message store records **all outbound messages** and **both sequence counters**. Inbound messages are not stored — each side is responsible only for replaying its own outbound messages. If OEE detects a gap in the messages it received from a client, it sends that client a ResendRequest, and the client replays from its own store. OEE's store exists to honour the same obligation in the other direction — replaying to a client that detects a gap in what OEE sent. Both counters are persisted, since both are needed for gap detection on reconnection.

`FileStoreFactory` writes each session to a file set: a body file containing serialised FIX messages, a header index mapping sequence numbers to byte offsets, a sequence-number file, and a session-state file. On startup, QuickFIX/n restores sequence context from these files; on a ResendRequest, it uses the index to retrieve messages for replay.

`MemoryStoreFactory` holds messages and sequence numbers in memory only — both are lost on process termination. It is appropriate for tests that do not exercise persistence or restart recovery; persistence and replay tests should use an isolated temporary file store.

Custom persistence backends are supported by implementing `IMessageStore` and `IMessageStoreFactory` — the extension point for integrating an external durable store when file-based persistence doesn't meet operational requirements.

### 11.2 Logging

`ILogFactory` produces `ILog` instances that record raw FIX wire-format traffic. Each entry is timestamped and contains the complete tag-value string of the sent or received message; this is distinct from application-level structured logging. Three built-in implementations:

- `FileLogFactory` — writes a `messages.current.log` file containing both incoming and outgoing messages and a separate `event.current.log` file for each session; it does not perform daily rotation.
- `ScreenLogFactory` — writes to standard output; appropriate for development environments.
- `NullLogFactory` — discards FIX traffic output; useful for tests where FIX-level logging is not needed.

QuickFIX/n 1.14 also provides `ThreadedSocketAcceptor` and `SocketInitiator` constructors that accept `Microsoft.Extensions.Logging.ILoggerFactory`. The legacy `QuickFix.Logger.ILogFactory` API remains supported; OEE currently uses its built-in factories.

---

## 12. COMMON PITFALLS

- **`FieldNotFoundException` on optional fields.** Direct access on an absent optional field throws. Guard optional fields with `IsSetX()` or `IsSetField()` before access.
- **Using deprecated `ExecType` values.** `PARTIAL_FILL` (1) and `FILL` (2) are deprecated in FIX 4.4. Fill execution reports use `TRADE` (F) with the appropriate `OrdStatus`.
- **Mismatched `ExecType` and `OrdStatus`.** An order with `LeavesQty > 0` and `ExecType=TRADE` carries `OrdStatus=PARTIALLY_FILLED`; `LeavesQty=0` carries `OrdStatus=FILLED`. Counterparties may reject inconsistent pairs.
- **Unhandled exceptions escaping `FromApp`.** QuickFIX/n catches only four protocol exceptions from `FromApp` (`UnsupportedMessageType`, `FieldNotFoundException`, `IncorrectDataFormat`, `IncorrectTagValue`) and converts them to the appropriate FIX reject. Any other exception propagates, terminates the `ClientHandlerThread`, and disconnects the client. Wrap downstream calls in try/catch and either translate the error to one of the four exceptions or send an explicit reject via `SendToTarget` and return normally.
- **Blocking the client-handler thread.** A blocking call inside an inbound callback prevents QuickFIX/n from reading the next message or responding to a TestRequest, which can cause the counterparty to time out and disconnect. Offload blocking or latency-sensitive work to a channel consumer task.
- **Sending before `OnLogon`.** If the session exists but no responder is attached, `Session.SendToTarget` returns `false`; it throws `SessionNotFound` only for an unregistered `SessionID`. Gate business sends on verified logon state and check the return value.
- **1-based repeating group index.** The index passed to `GetGroup` is 1-based; passing `0` throws an exception. The loop range for a group with N instances is 1 to N inclusive.
- **Missing data dictionary file.** If the `DataDictionary` path doesn't exist at runtime, QuickFIX/n throws at startup. `FIX44.xml` must be present in the build output directory.
- **Sequence number desync after store loss.** Deleting or corrupting the file store between sessions causes the counterparty's expected sequence numbers to diverge. Production recovery requires coordinating a sequence reset with the counterparty. Use `MemoryStoreFactory` or an isolated temporary file store to prevent state leaking between unrelated test runs.
- **Manual sequence-field assignment.** Application code should not set `MsgSeqNum`, `PossDupFlag`, or `PossResend`; QuickFIX/n manages these session fields, including during replay and gap fill. Incorrect manipulation corrupts session state and may force a coordinated reset.
- **Clock skew near session boundaries.** When `StartTime`/`EndTime` are configured, clock drift between hosts can cause connection refusals near boundaries. All gateway hosts must synchronise clocks via NTP.

---

## REFERENCES

- FIX 4.4 Standard Specification — authoritative field/message reference
- [QuickFIX/n official documentation](https://quickfixengine.org/n/documentation/)
- [QuickFIX/n source repository](https://github.com/connamara/quickfixn)
