# PROJECT CHARTER

# OpenEquityExchange – Electronic Trading Platform

**Version:** 1.1
**Status:** Draft ‒ Open for Review
**Date:** June 2026
**Author:** BinhLD

---

## EXECUTIVE SUMMARY

**OpenEquityExchange** is a production-grade electronic trading platform built on the .NET ecosystem. It implements the core infrastructure of a modern stock exchange: order ingestion, risk validation, order matching, and market data distribution.

The project follows a strict **"measure before optimizing"** philosophy. The initial phases focus on building a complete, functionally correct exchange using proven standard libaries. Subsequent phases will systematically replace identified bottlenecks with custom implementations to achieve ultra-low latency targets.

---

## 1. PROJECT DEFINITION

### 1.1 Project Identity

| Attribute     | Value                                     |
| ------------- | ----------------------------------------- |
| Project Name  | OpenEquityExchange (OEE)                  |
| Project Type  | Open-source .NET reference implementation |
| Primary Stack | .NET 8, C# 12                             |
| Timeline      | Q2 2026 – Q4 2026 (9 months)              |
| Team          | Solo developer                            |
| License       | MIT                                       |
| Repository    | [OpenEquityExchange](#)                   |

### 1.2 Objectives

| ID    | Objective                                                  | Success Metric                                               |
| ----- | ---------------------------------------------------------- | ------------------------------------------------------------ |
| BO-01 | Reference implementation for stock exchange infrastructure | End-to-end flow from order entry to execution to market feed |
| BO-02 | Demonstrate low-latency .NET techniques on a real domain   | Techniques are benchmarked and trade-offs documented         |
| BO-03 | Production-quality, maintainable codebase                  | `≥80% test coverage`; no paid dependencies                   |
| BO-04 | Self-contained public reference                            | Reproducible benchmarks and comprehensive documentation      |

---

## 2. NON-FUNCTIONAL REQUIREMENTS

| Requirement                | Target                                       |
| -------------------------- | -------------------------------------------- |
| **Order-To-Match Latency** | $P_{99}$ `<1ms`                              |
| **Sustained Throughput**   | `≥10,000 orders/second`                      |
| **System Uptime**          | `99.99%`                                     |
| **Replay Behaviour**       | Deterministic                                |
| **Recovery Objective**     | $P_{99}$ `<30s`                              |
| **Memory Stability**       | No unbounded memory growth                   |
| **Observability**          | Metrics, traces, and logs for critical paths |

---

## 3. SCOPE

### 3.1 In-Scope

| Component                  | Details                                                                                                                             |
| -------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| **Market Access**          | FIX & WebSocket protocols, session management, message normalization, shard routing                                                 |
| **Sequencer**              | Event ordering, deterministic timestamping, Write-ahead log (WAL)                                                                   |
| **Risk Engine**            | **Stateless Risk** (price/quantity > 0, symbol exits, etc) and **Stateful Risk** (credit & buying power limit, position limit, etc) |
| **Matching Engine**        | Central limit order book (CLOB), price-time priority matching, per-instrument sharding                                              |
| **Market Data**            | Level 1 snapshots, Level 2 book updates, trade ticks, real-time transport                                                           |
| **Persistence & Recovery** | Write-ahead log, event replay, snapshot recovery                                                                                    |
| **Scalability**            | Per-instrument shards, load balancing                                                                                               |
| **Observability**          | OpenTelemetry, Prometheus, Grafana                                                                                                  |
| **Testing**                | Unit, integration, BenchmarkDotNet suites, load testing                                                                             |

### 3.2 Out-of-Scope

- REST API for cold-path queries, admin config
- Real monetary transactions or user account ledgers
- Derivatives (options, futures, multi-leg orders)
- Regulatory compliance (MiFID II, Reg NMS, SEC)
- Clearing, settlement, post-trade processing
- Retail UI (web, mobile, desktop)
- KYC / AML workflows
- External market data (all data is synthetic)

---

## 4. KEY DELIVERABLES

### 4.1 Core System Components

| Deliverable                      | Type        |
| -------------------------------- | ----------- |
| Dual FIX + WebSocket Gateway     | Source Code |
| Internal Type Normaliser         | Source Code |
| Shard Manager & Shard Balancer   | Source Code |
| Matching Engine & Order Book     | Source Code |
| Market Data Publisher            | Source Code |
| Persistence Layer (WAL + Replay) | Source Code |

### 4.2 Quality & Performance

| Deliverable                   | Type        |
| ----------------------------- | ----------- |
| BenchmarkDotNet Results       | Report      |
| Load Testing Report           | Report      |
| Unit / Integration Test Suite | Source Code |
| Performance Baseline          | Markdown    |

### 4.3 Documentation & Deployment

| Deliverable                          | Type     |
| ------------------------------------ | -------- |
| Exchange Domain Knowledge            | Markdown |
| Architecture Decision Records (ADRs) | Markdown |
| Low-Latency Technique Guide          | Markdown |
| System Design Document               | Markdown |
| Build & Deployment Guide             | Markdown |

### 4.4 Publication

| Deliverable               | Type        |
| ------------------------- | ----------- |
| Public GitHub Repository  | Source Code |
| Porfolio Artifact         | Social Post |
| Online Accessible Gateway | Demo System |

---

## 5. SUCCESS CRITERIA

### 5.1 Correctness

- Orders match by price-time priority with no exceptions
- No lost orders or duplicate fills under load
- Sequence numbers are contiguous, monotonic, and validated
- Crash recovery restores a consistent state
- Test coverage ≥80% across core components

### 5.2 Performance

- Order-to-match latency $P_{99}$ `<1ms`
- Sustained throughput `≥10,000 orders/second`
- Market data jitter $P_{99}$ `<100µs`

### 5.3 Code Quality

- Single responsibility per component
- Consistency naming convention
- No paid dependencies

### 5.4 Documentation

- Each module documents responsibilities and architectural rationale
- Each optimisation documents rationale, trade-offs, and measured impact
- ADRs cover significant architectural decisions
- A new developer can run the system within one hour

---

## 6. TECHNICAL ARCHITECTURE

### 6.1 Component Overview

```text
┌──────────────────────────────────────┐
│      FIX +  WebSocket Gateway        │ → Order ingestion, session management, stateless
└──────────────────┬───────────────────┘   risk checks, internal type normalization
                   │
┌──────────────────▼───────────────────┐
│              Sequencer           ->  │ → Event ordering, deterministic timestamping, write-ahead log
└──────────────────┬───────────────────┘
                   │
┌──────────────────▼───────────────────┐
│         Stateful Risk Engine         │ → Stateful risk checks: buying power, position limits, etc.
└──────────────────┬───────────────────┘
                   │
┌──────────────────▼───────────────────┐
│           Matching Engine            │ → Central limit order book, price-time priority matching
└──────────────────┬───────────────────┘
                   │
                   ├──→ Persistence
                   ├──→ Market Data Publisher
                   └──→ Observability

```

### 6.2 Design Principles

| Principle                     | In Practice                                                                      |
| ----------------------------- | -------------------------------------------------------------------------------- |
| **Deterministic Processing**  | Given the same sequence of inputs, the engine produces identical outputs         |
| **Deterministic Time**        | All downstream components rely exclusively on sequencer-assigned timestamps      |
| **Immutability**              | Orders and trades are never modified after creation                              |
| **Event Sourcing**            | State changes are append-only events                                             |
| **Clean Architecture**        | Domain logic is isolated from infrastructure; dependencies point inward          |
| **Observable by Default**     | Critical paths emit logs, traces, and metrics without blocking execution threads |
| **Testability**               | Components use dependency injection and avoid static state                       |
| **Measure before Optimising** | Changes require benchmark evidence                                               |

### 6.3 Sequence Model

Sequence is a core exchange concept. Every event entering the system receives a unique, monotonically increasing sequence number before processing.

| Property          | Requirement                                                                  |
| ----------------- | ---------------------------------------------------------------------------- |
| Monotonic         | Sequence numbers never decrease                                              |
| Contiguous        | Unexpected gaps indicate missing or delayed events and require investigation |
| Single assignment | Sequence numbers are assigned by a dedicated coordination component          |
| Determinism gate  | Downstream components process events in sequence order                       |
| Recovery anchor   | WAL and snapshots are indexed by sequence number                             |

Replaying the WAL from sequence `N` reconstructs identical state.

### 6.4 Low-Latency .NET Techniques

| Technique                       | Where Applied                 | Purpose                            |
| ------------------------------- | ----------------------------- | ---------------------------------- |
| **System.IO.Pipelines**         | Protocol handling             | Efficient socket processing        |
| **Bounded lock-free messaging** | Inter-component communication | Low-latency bounded hand-off       |
| **Span & ArrayPool**            | Parsing & Serialisation       | Zero-copy buffer slicing           |
| **Struct Types**                | Domain models & values        | Reduce heap allocations            |
| **Thread Affinitization**       | Matching & Sequencing engines | Eliminate context-switching jitter |

---

## 7. PROJECT PHASES & MILESTONES

Four sequential phases across Q2–Q4 2026. Each phase ends with a demonstrable output.

### Phase 1 — Functional Foundation

**Goal:** Build a complete, working exchange using proven libraries to establish a functional baseline.  
**Exit Milestone:** System is fully functional and stable; orders are matched and broadcasted over network protocols.

### Phase 2 — Measurement & Benchmarking

**Goal:** Use BenchMarkDotNet and profiling tools to identify specific performance bottlenecks.  
**Exit Milestone:** Detailed performance report highlighting the bottlenecks to be replaced.

### Phase 3 — High-Performance Refatoring

**Goal:** Replace identified bottlenecks with custom component via using low latency .NET techniques.  
**Exit Milestone:** System meets original target of `<1ms` latency and `10,000 orders/seconds` throughput via custom optimization

### Phase 4 — Final validation & Publication

**Goal:** Complete documentation and public release of the reference implementation.  
**Exit Milestone:** Repository is public and onboarding takes under one hour.

---

## 8. DEPENDENCIES

All dependencies are open-source and free to use.

| Library                     | Purpose                         |
| --------------------------- | ------------------------------- |
| **xUnit**                   | Unit and integration testing    |
| **Moq**                     | Mocking and test doubles        |
| **BenchmarkDotNet**         | Micro-benchmarking              |
| **OpenTelemetry .NET**      | Distributed tracing and metrics |
| **prometheus-net**          | Prometheus metrics export       |
| **Grafana**                 | Dashboards                      |
| **QuickFIX/n**              | FIX protocol engine             |
| **k6**                      | Load and throughput testing     |
| **Docker / Docker Compose** | Local deployment                |
| **GitHub Actions**          | CI pipeline                     |

---

## 9. RISKS, ASSUMPTIONS & CONSTRAINTS

### 9.1 Risks

| Risk                               | Likelihood | Impact | Mitigation                                            |
| ---------------------------------- | ---------: | -----: | ----------------------------------------------------- |
| Scope creep from new feature ideas |       High | Medium | Maintain strict scope boundaries                      |
| Over-engineering                   |     Medium |   High | Deliver end-to-end capability first                   |
| Performance targets not met        |     Medium |   High | Benchmark early and iterate                           |
| Solo developer delivery delays     |     Medium | Medium | Time-box phases, isolate core book testing in Phase 1 |

### 9.2 Assumptions

- Developer can commit 30–40 hours per week consistently
- All tools and infrastructure are open-source
- Synthetic market data is acceptable
- .NET 8 LTS remains supported
- GitHub remains available for hosting and CI/CD

### 9.3 Constraints

| Constraint                    | Detail                                       |
| ----------------------------- | -------------------------------------------- |
| **No paid dependencies**      | Open-source libraries only                   |
| **No polyglot runtime**       | Runtime implementation remains entirely .NET |
| **Sequential delivery**       | One phase at a time                          |
| **No external data**          | Synthetic market data only                   |
| **Measure before optimising** | Optimisations require benchmark evidence     |

---

## 10. ROLES & TIME ALLOCATION

| Role            | Responsibilities                    | Allocation |
| --------------- | ----------------------------------- | ---------: |
| **Developer**   | Implementation and validation       |       ~50% |
| **QA Engineer** | Testing and failure scenarios       |       ~20% |
| **Architect**   | ADRs and design decisions           |       ~15% |
| **Tech Writer** | Documentation and technical content |       ~15% |

---
