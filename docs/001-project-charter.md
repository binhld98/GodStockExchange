# PROJECT CHARTER

# OpenEquityExchange – Electronic Trading Platform

**Version:** 1.3  
**Status:** Draft – Under Review  
**Date:** June 2026  
**Author:** BinhLD

---

## EXECUTIVE SUMMARY

**OpenEquityExchange (OEE)** is a production-grade electronic trading platform built on the .NET ecosystem. The platform implements the core infrastructure of a modern stock exchange: order ingestion, risk validation, order matching, and market data distribution.

The project follows a strict **"measure before optimising"** philosophy. Early phases focus on building a complete, functionally correct exchange. Subsequent phases identify and resolve performance bottlenecks using profiling evidence rather than assumption.

---

## 1. PROJECT DEFINITION

### 1.1 Project Identity

| Attribute      | Value                                |
| -------------- | ------------------------------------ |
| Project Name   | OpenEquityExchange (OEE)             |
| Project Type   | Open-source reference implementation |
| Primary Stack  | .NET, C#                             |
| License        | MIT                                  |
| Timeline       | Q2 2026 – Q4 2026 (approx. 9 months) |
| Team Structure | Solo developer (all roles)           |
| Repository     | [OpenEquityExchange](#)              |

### 1.2 Strategic Objectives

| ID    | Objective                                                                          | Success Metric                                                                         |
| ----- | ---------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| BO‑01 | Deliver a complete, working exchange reference implementation                      | End-to-end order flow from client ingestion through execution to market data broadcast |
| BO‑02 | Demonstrate production-grade performance techniques in a realistic exchange domain | All techniques benchmarked; trade-offs documented with measured evidence               |
| BO‑03 | Maintain a production-quality, maintainable codebase                               | ≥ 80% automated test coverage; zero paid dependencies                                  |
| BO‑04 | Provide a self-contained, publicly accessible educational reference                | Reproducible benchmarks and full documentation; new developer onboarding ≤ 1 hour      |

---

## 2. PROJECT SCOPE

### 2.1 In-Scope

| Concern                  | Detail                                                                                                    |
| ------------------------ | --------------------------------------------------------------------------------------------------------- |
| Market Access            | FIX & WebSocket protocols; client authentication; session management; message normalisation               |
| Event Sequencing         | Event ordering; timestamp assignment; write-ahead log (WAL)                                               |
| Risk Management          | **Stateless Risk** (e.g., price/quantity > 0) and **Stateful Risk** (e.g., credit & buying power limit)   |
| Order Routing            | Per-instrument sharding; order routing; dynamic shard balancing                                           |
| Order Matching           | Central limit order book (CLOB); price-time priority order matching; full order lifecycle; event emission |
| Market Data Distribution | Real-time Level 1 and Level 2 order book updates; trade tick reports                                      |
| Persistence & Recovery   | Append-only event log; state snapshot management; deterministic crash recovery                            |
| Observability            | Structured metrics, distributed tracing, and logs across critical processing paths                        |
| Quality Assurance        | Unit, integration, and load testing; performance benchmarking with reproducible results                   |

### 2.2 Out-of-Scope

- Query APIs for administrative configuration and reporting interfaces
- Real monetary transactions and user account management
- Derivatives instruments (options, futures, multi-leg orders)
- Regulatory compliance frameworks (e.g., MiFID II, Reg NMS, SEC)
- Clearing, settlement, and post-trade processing
- Retail UI (web, mobile, desktop)
- KYC / AML workflows
- External market data (all data is synthetic)

---

## 3. FUNCTIONAL REQUIREMENTS

The platform delivers the following exchange-grade capabilities across its processing pipeline:

**Market Access & Normalisation** — Accepts orders from external clients via FIX & WebSocket protocols. Performs session authentication, inbound message validation, and format normalisation before forwarding events for internal processing.

**Deterministic Event Sequencing** — Assigns a globally unique, monotonically increasing sequence number to every inbound event. Sequence numbers serve as the authoritative ordering mechanism for all downstream components and as the primary anchor for state recovery.

**Two-Tier Risk Validation** — Enforces a two-stage pre-trade risk gate. Stateless checks are applied at the gateway to reject clearly invalid orders. Stateful checks (credit exposure, position limits, and buying power) are enforced after sequencing to ensure a consistent account state across all participants.

**Order Matching** — Maintains a central limit order book per instrument shard. Matches resting and incoming orders by price-time priority. Emits structured execution events for fills, partial fills, and cancellations.

**Market Data Distribution** — Publishes real-time order book snapshots, incremental book updates, and trade reports to downstream consumers with minimal and measurable latency.

**Persistence & Crash Recovery** — Records all events to a write-ahead log prior to processing. Supports deterministic state reconstruction through full log replay and periodic snapshot-based recovery, with recovery time bounded at $P_{99}$ < 30 seconds.

**Observability** — Emits structured telemetry (metrics, distributed traces, and structured logs) across all hot paths, enabling real-time performance monitoring and post-incident diagnostics.

---

## 4. KEY DELIVERABLES

### 4.1 Core System Components

| Deliverable                    | Type               |
| ------------------------------ | ------------------ |
| Market Access Gateway          | Software Component |
| Sequencer with Write-Ahead Log | Software Component |
| Two-Tier Risk Engine           | Software Component |
| Shard Manager & Load Balancer  | Software Component |
| Matching Engine & Order Book   | Software Component |
| Market Data Publisher          | Software Component |
| Persistence & Recovery Layer   | Software Component |

### 4.2 Quality & Performance

| Deliverable                   | Type        |
| ----------------------------- | ----------- |
| Performance Benchmark Results | Report      |
| Load Testing Report           | Report      |
| Unit & Integration Test Suite | Source Code |
| Performance Baseline Document | Markdown    |

### 4.3 Documentation

| Deliverable                          | Type     |
| ------------------------------------ | -------- |
| Exchange Domain Knowledge Guide      | Markdown |
| Architecture Decision Records (ADRs) | Markdown |
| Low-Latency Technique Guide          | Markdown |
| System Design Document               | Markdown |
| Build & Deployment Guide             | Markdown |

### 4.4 Publication

| Deliverable              | Type        |
| ------------------------ | ----------- |
| Public GitHub Repository | Source Code |
| Live Demo Gateway        | Demo System |
| Portfolio Artifact       | Social Post |

---

## 5. SUCCESS CRITERIA

### 5.1 Correctness

- Orders match by price-time priority with no exceptions
- No lost orders or duplicate fills under normal or peak load
- Sequence numbers are contiguous, monotonic, and integrity-validated
- Crash recovery restores a consistent and deterministic state
- Test coverage ≥ 80% across all core components

### 5.2 Performance

- Order-to-Match Latency: $P_{99}$ < 1ms
- Sustained Order Throughput: ≥ 10,000 orders/second
- Market Data Jitter: $P_{99}$ < 100 µs
- Recovery Time Objective: $P_{99}$ < 30 seconds

### 5.3 Code & Architecture Quality

- Single responsibility per component
- Consistent naming conventions throughout the codebase
- Zero paid dependencies
- ADRs cover all significant architectural decisions

### 5.4 Documentation

- Each component documents its responsibilities and architectural rationale
- Each optimisation documents its justification, trade-offs, and measured impact
- A new developer can clone, build, and run the full system within one hour

---

## 6. SYSTEM ARCHITECTURE

### 6.1 Component Overview

OEE follows a sequential pipeline architecture in which every order traverses a fixed set of processing stages. Each stage has a clearly bounded responsibility and communicates with adjacent stages through well-defined contracts.

```text
┌──────────────────────────────────────┐
│          Market Access Gateway       │ → Ingestion · Authentication · Stateless Validation · Normalisation
└──────────────────┬───────────────────┘
                   │
┌──────────────────▼───────────────────┐
│              Sequencer               │ → Global Ordering · Authoritative Timestamping · Write-Ahead Log
└──────────────────┬───────────────────┘
                   │
┌──────────────────▼───────────────────┐
│            Shard Manager             │ → Instrument Routing · Load Distribution
└──────────────────┬───────────────────┘
                   │
┌──────────────────▼───────────────────┐
│             Risk Engine              │ → Stateful Validation
└──────────────────┬───────────────────┘
                   │
┌──────────────────▼───────────────────┐
│           Matching Engine            │ → Central Limit Order Book · Price-Time Priority · Event Emission
└──────────────────┬───────────────────┘
                   │
                   ├──► Persistence
                   ├──► Market Data Publisher
                   └──► Observability
```

### 6.2 Design Principles

| Principle                 | In Practice                                                                 |
| ------------------------- | --------------------------------------------------------------------------- |
| Deterministic Processing  | Given the same sequence of inputs, the engine produces identical outputs    |
| Deterministic Time        | All downstream components rely exclusively on sequencer-assigned timestamps |
| Immutability              | Orders and trade records are never modified after creation                  |
| Event Sourcing            | All state changes are an append-only sequence of events                     |
| Separation of Concerns    | Dependencies point inward toward the domain logic                           |
| Observable by Default     | All critical paths emit structured telemetry without impacting throughput   |
| Testability               | Components use dependency injection and avoid static state                  |
| Measure before Optimising | Changes require benchmark evidence                                          |

---

## 7. PROJECT PHASES & MILESTONES

Four sequential phases across Q2–Q4 2026. Each phase produces a stable, demonstrable output.

### Phase 1 — Functional Foundation

**Objectives:** Deliver a complete, working exchange using proven libraries to establish a correct functional baseline.  
**Exit Milestone:** Orders are ingested, risk-validated, matched, and market data is broadcast over the network. All test cases pass.

### Phase 2 — Measurement & Benchmarking

**Objectives:** Use benchmarking and profiling tools to locate specific performance bottlenecks.  
**Exit Milestone:** Detailed performance report highlighting the bottlenecks that need to be addressed.

### Phase 3 — High-Performance Refactoring

**Objectives:** Replace identified bottlenecks with custom high-performance alternatives using low-latency .NET techniques.  
**Exit Milestone:** System meets the original targets of $P_{99}$ < 1ms latency and 10,000 orders/second sustained throughput under load.

### Phase 4 — Final Validation & Publication

**Objectives:** Release the codebase and documentation publicly to GitHub and launch a live demo gateway.  
**Exit Milestone:** A new developer can clone, build, and run the full system, and reproduce all benchmarks within one hour.

---

## 8. RISKS, ASSUMPTIONS & CONSTRAINTS

### 8.1 Risks

| Risk                                           | Likelihood | Impact | Mitigation                                                                       |
| ---------------------------------------------- | ---------: | -----: | -------------------------------------------------------------------------------- |
| Scope creep from additional feature ideas      |       High | Medium | Enforce strict scope boundaries and log deferred items to a prioritised backlog  |
| Over-engineering before the baseline is proven |     Medium |   High | Deliver complete end-to-end capabilities before any optimisation activity        |
| Performance targets not met within timeline    |     Medium |   High | Start performance testing early and make improvements based on benchmark results |
| Solo developer delivery delays                 |     Medium | Medium | Set time limits for each phase and focus first on critical path correctness      |

### 8.2 Assumptions

- The developer can commit 30–40 hours per week consistently throughout all phases
- All required tools, infrastructure, and libraries are open-source and free to use
- Synthetic market data is acceptable for all development, testing, and benchmarking purposes
- GitHub remains available for version control, CI/CD automation, and public hosting

### 8.3 Constraints

| Constraint                  | Detail                                                                    |
| --------------------------- | ------------------------------------------------------------------------- |
| No paid dependencies        | All components are built exclusively on open-source libraries             |
| No polyglot runtime         | The system runs entirely within the .NET runtime                          |
| Sequential delivery         | Each phase must be completed and validated before the next begins         |
| Synthetic data only         | No integration with external or live market data providers                |
| Evidence-based optimisation | No performance changes are introduced without reproducible benchmark data |

---

## 9. ROLES & TIME ALLOCATION

| Role        | Responsibility                      | Allocation |
| ----------- | ----------------------------------- | ---------: |
| Developer   | Implementation and validation       |       ~50% |
| QA Engineer | Testing and failure scenarios       |       ~20% |
| Architect   | ADRs and design decisions           |       ~15% |
| Tech Writer | Documentation and technical content |       ~15% |

---

## 10. REFERENCES

Detailed exchange-domain terminology and architectural concepts are documented in the [Exchange Domain Knowledge Guide](#).

---
