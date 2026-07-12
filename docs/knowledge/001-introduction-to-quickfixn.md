# Introduction to QuickFIX/n

# OpenEquityExchange (OEE) – Market Access Layer (MAL)

**Version:** 1.0  
**Status:** Draft – Under Review  
**Author:** BinhLD  
**Date:** July 2026

---

## OVERVIEW

QuickFIX/n is a free, open-source, native C# implementation of the FIX (Financial Information eXchange) protocol for .NET. It is the official .NET port of the original C++ QuickFIX engine, maintained by Connamara Systems, and is the de-facto standard FIX engine for .NET-based trading systems.

QuickFIX/n implements the full FIX session layer — logon, heartbeat, sequence number management, gap fill, and resend — so application code only needs to handle business-level message processing. It handles all low-level FIX concerns automatically: TCP connectivity, message framing, parsing, checksum validation, sequence numbering, heartbeating, and session recovery.

**Key properties:**

| Property              | Description                                        |
| --------------------- | -------------------------------------------------- |
| Language              | C# / .NET                                          |
| License               | QuickFIX Software License (permissive open-source) |
| Repository            | github.com/connamara/quickfixn                     |
| Core NuGet package    | `QuickFIXn.Core`                                   |
| FIX 4.4 NuGet package | `QuickFIXn.FIX44`                                  |

Since FIX 4.4 is the only version OEE's MAL supports, the project references `QuickFIXn.Core` and `QuickFIXn.FIX44`, both at version 1.14.x:

```xml
<!-- Core Engine -->
<PackageReference Include="QuickFIXn.Core" Version="1.14.*" />

<!-- FIX 4.4 message definitions -->
<PackageReference Include="QuickFIXn.FIX44" Version="1.14.*" />
```

> **Note on multi-version support:** QuickFIX/n's engine can host multiple FIX versions within a single process, each in an isolated session with its own sequence space and data dictionary. OEE does not use this capability today — FIX 4.4 is the exclusive target for MAL — but extending to another version later only requires adding a new `[SESSION]` block with the appropriate `BeginString` and `DataDictionary`, with no code changes required.

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

> QuickFIX/n populates all header and trailer fields automatically. Application code only ever sets body fields.

### 1.3 Sessions and Sequence Numbers

A FIX session is uniquely identified by the triple `(BeginString, SenderCompID, TargetCompID)`. Every message carries `MsgSeqNum (34)`, incrementing by 1 per message sent; each side tracks its own outbound counter independently.

Sequence integrity is fundamental to FIX reliability:

- If a receiver detects a gap, it sends a **ResendRequest** to obtain the missing messages.
- The sender replays the requested messages, or sends a **SequenceReset-GapFill** if the originals are unavailable.
- Sequence numbers are typically reset at the start of each trading day via a coordinated Logout/Logon, though continuous-market configurations using non-resetting sequence numbers are also supported.

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

Once `Start()` is called, QuickFIX/n manages all TCP connections and session state. Application code interacts with the library only through `IApplication` callbacks and the static `Session` class when sending messages.

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
| `Session`                | Static class used to send messages via `Session.SendToTarget()`                  |
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

# Heartbeat interval in seconds, and acceptable range
HeartBtInt=30
MinHeartBtInt=30
MaxHeartBtInt=60

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

To extend support for an additional FIX version later, add a new `[SESSION]` block with the appropriate `BeginString` and `DataDictionary`; no existing configuration or code changes are required.

### 3.3 Core Parameters

- **ConnectionType** — `acceptor` or `initiator`. OEE-MAL always uses `acceptor`.
- **BeginString** — the FIX version string. OEE-MAL supports `FIX.4.4` only; other versions are out of scope for now.
- **DataDictionary** — path to the XML data dictionary for the session's FIX version; used to validate all incoming messages at runtime.
- **SenderCompID / TargetCompID** — form the session identity. On the acceptor side, `SenderCompID` is the exchange's identifier and `TargetCompID` is the participant's identifier.
- **SocketAcceptPort** — TCP port for the acceptor.
- **HeartBtInt** — heartbeat interval in seconds. The acceptor should adopt the value proposed by the initiator's Logon unless it falls outside the configured `MinHeartBtInt` / `MaxHeartBtInt` range, in which case the Logon is rejected.
- **FileStorePath / FileLogPath** — root directories for the file-based message store and FIX traffic log; each session gets its own subdirectory/files.
- **ResetOnLogon / ResetOnLogout / ResetOnDisconnect** — control whether sequence numbers reset to 1 on the corresponding event. Must be `N` in production to preserve sequence continuity across reconnections; may be `Y` in isolated test environments to avoid sequence desync between test runs.
- **StartTime / EndTime** — daily session window in UTC (`HH:MM:SS`). The acceptor refuses connections outside this window; omitting them makes the session permanently active.
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
| `HeartBtInt`            | Seconds between heartbeats                                         | N/A            |
| `NonStopSession`        | `Y` = never reset sequence numbers or disconnect                   | `N`            |
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
| `SocketBufferSize`        | TCP send buffer, in bytes                                                     | `8192`  |
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

Fires when the session object is instantiated — during `acceptor.Start()`, on the thread that called `Start()`, before any listener or client thread exists. Use this to initialise per-session data structures keyed by `SessionID`.

### 4.2 OnLogon

Fires when a logon handshake completes successfully; the signal that the session is active and application messages may be sent. Application code must gate all outbound sends on having received this callback for the target session.

### 4.3 OnLogout

Fires when a session ends — locally initiated, by the counterparty, or due to network failure. Application code must not send messages after this callback fires until `OnLogon` fires again for that session.

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

    var participantId = message.GetString(Tags.TargetCompID);

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
| `FieldNotFoundException` | Sends session-level Reject (MsgType=3), tag missing        |
| `IncorrectDataFormat`    | Sends session-level Reject (MsgType=3), bad format         |
| `IncorrectTagValue`      | Sends session-level Reject (MsgType=3), value out of range |

Application code must catch exceptions from downstream processing inside `FromApp` and translate them to one of the four exceptions above, rather than letting unhandled exceptions propagate through the callback. An unhandled exception crashes the session I/O thread and disconnects the client (see §12, Common Pitfalls).

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

// Direct property assignment on the strongly typed message clas
report.ClOrdID = new ClOrdID("CLI-ORD-00001");
report.TransactTime = new TransactTime(DateTime.UtcNow);

// Or the generated .Set() method on the strongly typed message clas
report.Set(new LastQty(0m));

// On the general message class — using generic SetField() method
report.SetField(new LastPx(0m));
```

Standard header and trailer fields are populated automatically by QuickFIX/n itself; application code must not set these manually.

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

The pattern: inherit from `MessageCracker`, call `Crack(msg, sessionID)` inside `FromApp`, and implement overloaded `OnMessage` handlers for each message type you wish to process. If `Crack` encounters a `MsgType` with no matching overload, it throws `UnsupportedMessageType`, which QuickFIX/n automatically converts to a BusinessMessageReject (MsgType=j). This makes `MessageCracker` the recommended approach for receiving application messages.

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

Messages are sent through the static `Session` class, which provides thread-safe access to any active session:

```csharp
Session.SendToTarget(message, sessionID);
```

This call is non-blocking. QuickFIX/n enqueues the message, assigns the outbound sequence number, serialises it to FIX wire format, persists it to the message store, and transmits it on the session I/O thread. If the session is not currently logged on, `SessionNotFound` is thrown.

An alternative overload accepts component identifier strings directly:

```csharp
Session.SendToTarget(message, senderCompID, targetCompID);
```

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

QuickFIX/n manages heartbeats autonomously. Every `HeartBtInt` seconds of receive silence, the session layer sends a TestRequest; if the counterparty does not respond with a matching Heartbeat within `HeartBtInt` plus tolerance, the session layer initiates logout. Application code does not need to handle heartbeats — `FromAdmin` fires when Heartbeat/TestRequest messages arrive, but the session layer handles the protocol response itself.

### 7.4 Sequence Number Management

Each session maintains two independent counters: `NextSenderMsgSeqNum` (next outbound message) and `NextTargetMsgSeqNum` (next expected inbound message). On each received message, the session layer compares its `MsgSeqNum` against `NextTargetMsgSeqNum`:

| Received MsgSeqNum                | Session layer action                                         |
| --------------------------------- | ------------------------------------------------------------ |
| Equals `NextTargetMsgSeqNum`      | Message accepted; counter advances by one                    |
| Higher than `NextTargetMsgSeqNum` | Gap detected; ResendRequest sent for the missing range       |
| Lower than `NextTargetMsgSeqNum`  | Uncoordinated reset detected; Logout sent, connection closed |

`FileStoreFactory` persists both counters to disk. On reconnection with `ResetOnLogon=N`, the initiator's Logon `MsgSeqNum` is compared against the stored `NextTargetMsgSeqNum` to detect gaps accumulated while disconnected, and QuickFIX/n replays missing messages from the file store automatically.

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

For the full set of canonical scenarios (stateless/stateful rejection, acknowledgment, partial/full fill, cancel and cancel-replace confirmation) and their field-level rules, see the ExecutionReport Domain Guide (`003-execution-report-domain-guide.md`).

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
    char side, decimal fillQty, decimal fillPx, decimal leavesQty, decimal cumQty)
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
        new AvgPx(fillPx)
    );

    report.ClOrdID = new ClOrdID(clOrdID);
    report.LastQty = new LastQty(fillQty);
    report.LastPx = new LastPx(fillPx);
    report.TransactTime = new TransactTime(DateTime.UtcNow);

    Session.SendToTarget(report, sessionID);
}
```

> **Deprecated-value pitfall:** `PARTIAL_FILL` (1) and `FILL` (2) are deprecated `ExecType` values in FIX 4.4. All fill execution reports must use `TRADE` (F) with the appropriate `OrdStatus`, as shown above. An order with `LeavesQty > 0` and `ExecType=TRADE` must carry `OrdStatus=PARTIALLY_FILLED`; an order with `LeavesQty=0` must carry `OrdStatus=FILLED`. Inconsistent pairs will be rejected by counterparty validation.

### 8.4 Sending an OrderCancelReject

When a cancel or amend request cannot be fulfilled, OEE must respond with `OrderCancelReject` rather than `ExecutionReport`. The `CxlRejResponseTo` field distinguishes whether the reject is in response to an `OrderCancelRequest` (`'1'`) or an `OrderCancelReplaceRequest` (`'2'`):

```csharp
public void SendCancelReject(SessionID sessionID, string clOrdID, string origClOrdID,
    string orderID, string reason)
{
    var reject = new QuickFix.FIX44.OrderCancelReject(
        new OrderID(orderID),
        new ClOrdID(clOrdID),
        new OrdStatus(OrdStatus.REJECTED),
        new CxlRejResponseTo(CxlRejResponseTo.ORDER_CANCEL_REQUEST)
    );

    reject.OrigClOrdID = new OrigClOrdID(origClOrdID);
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
| `OrderStatusRequest`        | **Stateless:** required fields present (`ClOrdID`, `Symbol`, `Side`). **Stateful:** order exists and belongs to this participant.                                                                                                                                  | `BusinessMessageReject` with `BusinessRejectReason=UNKNOWN_ORDER`        |

### 9.2 Data Dictionary Validation

When `UseDataDictionary=Y`, QuickFIX/n validates every incoming message against the data dictionary before calling `FromApp`. Messages that fail validation are rejected at the session level automatically and never reach the application. Validation covers presence of required fields, correct field data types, valid enumerated values, and correct message structure including repeating groups.

### 9.3 Sending BusinessMessageReject Explicitly

When application logic determines a message is structurally valid but semantically unacceptable — for example, a duplicate `ClOrdID` — OEE must send an explicit `BusinessMessageReject` rather than throwing from `FromApp`:

```csharp
public void OnMessage(QuickFix.FIX44.NewOrderSingle order, SessionID sessionID)
{
    if (_orderRegistry.IsDuplicate(order.ClOrdID.Value))
    {
        var reject = new QuickFix.FIX44.BusinessMessageReject(
            new RefSeqNum(order.Header.GetInt(Tags.MsgSeqNum)),
            new RefMsgType(order.Header.GetString(Tags.MsgType)),
            new BusinessRejectReason(BusinessRejectReason.DUPLICATE_IDENTIFIER),
            new Text($"Duplicate ClOrdID: {order.ClOrdID.Value}")
        );
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

`FIX44.xml` is not bundled in the `QuickFIXn.FIX44` NuGet package — it must be sourced from the QuickFIX/n source repository (`spec/FIX44.xml`), committed to the project, declared as a build content file with `CopyToOutputDirectory=PreserveNewest`, and referenced by path in the `DataDictionary` setting. A missing or inaccessible dictionary causes QuickFIX/n to throw at startup.

Custom fields must use tag numbers in the user-defined range **5000–9999** to avoid conflicts with standardised FIX tags, and must be declared in a modified copy of the standard dictionary — otherwise QuickFIX/n rejects any incoming message that contains them. The `ValidateUserDefinedFields` setting controls whether custom tags in this range are validated against the dictionary; setting it to `Y` enforces validation and is the correct production setting.

---

## 10. THREADING MODEL

QuickFIX/n's `ThreadedSocketAcceptor` does **not** use the .NET thread pool — it creates one dedicated `System.Threading.Thread` per client connection. Each thread spends most of its life blocked on `socket.Read()`, consuming zero CPU while waiting. Because there is no CPU affinity, the OS scheduler moves threads between cores freely — 50 clients on a 4-core machine is completely fine.

`OnCreate` fires during `acceptor.Start()`, when `Session` objects are constructed, on the thread that called `Start()` — before any listener or client thread exists. The six other callbacks are invoked on the `ClientHandlerThread` for that session; callbacks for different sessions therefore execute concurrently on different threads.

Three rules follow from this model:

- **Protect shared state.** All callbacks except `OnCreate` run on `ClientHandlerThread` threads, and multiple sessions execute concurrently. Any mutable state shared across sessions must be accessed through a thread-safe mechanism — a concurrent collection, atomic operations, immutable data, or explicit synchronisation.
- **Never block a callback thread.** A blocked `ClientHandlerThread` cannot read the next incoming message or respond to a TestRequest, causing the counterparty to time out and disconnect. All latency-sensitive work — database calls, order processing, downstream I/O — must be offloaded immediately to a channel consumer task.
- **Call `Session.SendToTarget` from a consumer task.** Even though `SendToTarget` is thread-safe, calling it inside callbacks is inadvisable. Instead, enqueue the outbound message and let the consumer call `SendToTarget` off the session thread.

The recommended MAL pattern: receive messages in `FromApp`, enqueue them to a bounded `System.Threading.Channels.Channel`, and consume them on a dedicated task started at service initialisation. This isolates the session thread from all order-processing latency and provides natural backpressure.

---

## 11. PERSISTENCE, REPLAY & LOGGING

### 11.1 Message Store

The message store records **all outbound messages** and **both sequence counters**. Inbound messages are not stored — each side is responsible only for replaying its own outbound messages. If OEE needs a client to resend messages it missed, it sends a ResendRequest and the client replays from its own store; OEE's store exists so it can honour the same obligation in the other direction. Both counters are persisted, since both are needed for gap detection on reconnection.

`FileStoreFactory` writes each outbound message to a per-session file set: a body file with serialised FIX message strings, a header index file mapping sequence numbers to byte offsets in the body file, and a state file recording the current sender/target sequence numbers. On startup, QuickFIX/n reads the state file to restore sequence context; on a ResendRequest, it seeks into the body file via the header index to retransmit the referenced messages.

`MemoryStoreFactory` holds messages and sequence numbers in memory only — both are lost on process termination. This is the correct choice for all test environments: it eliminates file system side effects and prevents sequence state leaking between test runs.

Custom persistence backends are supported by implementing `IMessageStore` and `IMessageStoreFactory` — the extension point for integrating an external durable store when file-based persistence doesn't meet operational requirements.

### 11.2 Logging

`ILogFactory` produces `ILog` instances that record raw FIX wire-format traffic. Each entry is timestamped and contains the complete tag-value string of the sent or received message; this is distinct from application-level structured logging. Three built-in implementations:

- `FileLogFactory` — writes one log file per session per day, with incoming and outgoing messages in separate files; the primary diagnostic tool for FIX-level troubleshooting.
- `ScreenLogFactory` — writes to standard output; appropriate for development environments.
- `NullLogFactory` — discards all FIX traffic output; correct for integration tests where FIX-level logging isn't needed.

QuickFIX/n's `ILog` does not integrate with `Microsoft.Extensions.Logging`. Application-level structured logging uses the standard .NET logging abstractions independently.

---

## 12. COMMON PITFALLS

- **`FieldNotFoundException` on optional fields.** Direct access on an absent optional field throws. Guard optional fields with `IsSetX()` or `IsSetField()` before access.
- **Using deprecated `ExecType` values.** `PARTIAL_FILL` (1) and `FILL` (2) are deprecated in FIX 4.4. All fill execution reports must use `TRADE` (F) with the appropriate `OrdStatus`.
- **Mismatched `ExecType` and `OrdStatus`.** An order with `LeavesQty > 0` and `ExecType=TRADE` must carry `OrdStatus=PARTIALLY_FILLED`; `LeavesQty=0` must carry `OrdStatus=FILLED`. Inconsistent pairs will be rejected by counterparty validation.
- **Unhandled exceptions escaping `FromApp`.** QuickFIX/n catches only four protocol exceptions from `FromApp` (`UnsupportedMessageType`, `FieldNotFoundException`, `IncorrectDataFormat`, `IncorrectTagValue`) and converts them to the appropriate FIX reject. Any other exception propagates, terminates the `ClientHandlerThread`, and disconnects the client. Wrap downstream calls in try/catch and either translate the error to one of the four exceptions or send an explicit reject via `SendToTarget` and return normally.
- **Blocking the session I/O thread.** Any blocking call inside a callback prevents QuickFIX/n from reading the next incoming message or responding to a TestRequest, causing the counterparty to time out and disconnect. Offload all blocking or latency-sensitive work to a channel consumer task.
- **Sending before `OnLogon`.** Calling `Session.SendToTarget` before `OnLogon` fires throws `SessionNotFound`. Gate all sends on verified logon state.
- **1-based repeating group index.** The index passed to `GetGroup` is 1-based; passing `0` throws an exception. The loop range for a group with N instances is 1 to N inclusive.
- **Missing data dictionary file.** If the `DataDictionary` path doesn't exist at runtime, QuickFIX/n throws at startup. `FIX44.xml` must be present in the build output directory.
- **Sequence number desync after store loss.** Deleting or corrupting the file store between sessions causes the counterparty's expected sequence numbers to diverge. Production recovery requires coordinating a sequence reset with the counterparty. Test environments must use `MemoryStoreFactory` to avoid this problem entirely.
- **Manual `MsgSeqNum` assignment.** Application code must not set `MsgSeqNum`, `PossDupFlag`, or `PossResend` manually except during an explicit gap fill procedure. Incorrect manipulation corrupts session state and forces a full reset.
- **Clock skew near session boundaries.** When `StartTime`/`EndTime` are configured, clock drift between hosts can cause connection refusals near boundaries. All gateway hosts must synchronise clocks via NTP.

---

## REFERENCES

- FIX 4.4 Standard Specification — authoritative field/message reference
- QuickFIX/n official documentation and source repository: github.com/connamara/quickfixn
