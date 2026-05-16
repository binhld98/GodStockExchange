# PROJECT CHARTER

# OpenEquityExchange – Electronic Trading Platform

**Document Version:** 1.0  
**Status:** Draft ‒ Open for Review  
**Author:** BinhLD  
**Last Updated:** May 2026

---

## EXECUTIVE SUMMARY

**OpenEquityExchange** is a production-grade electronic trading platform built on the .NET ecosystem. It implements the core infrastructure of a modern stock exchange: order ingestion, risk validation, order matching, and market data distribution.

The project emphasizes architectural rationale, performance characteristics, and implementation trade-offs alongside source code.

---

## 1. PROJECT DEFINITION

### 1.1 Project Identity

| Attribute         | Value                                                                |
| ----------------- | -------------------------------------------------------------------- |
| **Project Name**  | OpenEquityExchange (OEE)                                             |
| **Project Type**  | Open-source .NET reference implementation                            |
| **Primary Stack** | .NET 8, C# 12                                                        |
| **Timeline**      | Q2 2026 – Q4 2026 (9 months)                                         |
| **Team**          | Solo developer                                                       |
| **License**       | MIT                                                                  |
| **Repository**    | [OpenEquityExchange](https://github.com/binhld98/OpenEquityExchange) |

### 1.2 Objectives

| ID        | Objective                                                  | Success Metric                                               |
| --------- | ---------------------------------------------------------- | ------------------------------------------------------------ |
| **BO-01** | Reference implementation for stock exchange infrastructure | End-to-end flow from order entry to execution to market feed |
| **BO-02** | Demonstrate low-latency .NET techniques on a real domain   | Techniques are benchmarked and trade-offs documented         |
| **BO-03** | Production-quality, maintainable codebase                  | ≥80% test coverage; no paid dependencies                     |
| **BO-04** | Self-contained public reference                            | Reproducible benchmarks and comprehensive documentation      |

---

## 2. NON-FUNCTIONAL REQUIREMENTS

| Requirement                    | Target                                       |
| ------------------------------ | -------------------------------------------- |
| Average order-to-match latency | <1ms                                         |
| Sustained throughput           | ≥10,000 orders/sec                           |
| Replay behaviour               | Deterministic                                |
| Recovery objective             | <30 seconds                                  |
| Memory stability               | No unbounded memory growth                   |
| Observability                  | Metrics, traces, and logs for critical paths |

---

## 3. SCOPE

### 3.1 In-Scope

| Component                  | Details                                                                   |
| -------------------------- | ------------------------------------------------------------------------- |
| **Market Access**          | FIX gateway, session management, shard routing                            |
| **Sequencer**              | Event ordering and sequence coordination                                  |
| **Risk Engine**            | Pre-trade checks: position limits, circuit breakers, order validation     |
| **Matching Engine**        | Central limit order book, price-time priority, per-instrument sharding    |
| **Market Data**            | Level 1 snapshots, Level 2 book updates, trade ticks, real-time transport |
| **Persistence & Recovery** | Write-ahead log, event replay, snapshot recovery                          |
| **Scalability**            | Per-instrument shards, load balancing                                     |
| **Observability**          | OpenTelemetry, Prometheus, Grafana                                        |
| **Testing**                | Unit, integration, BenchmarkDotNet suites, load testing                   |

### 3.2 Out-of-Scope

- Real monetary transactions or user account ledgers
- Derivatives (options, futures, multi-leg orders)
- Regulatory compliance (MiFID II, Reg NMS, SEC)
- Clearing, settlement, post-trade processing
- Retail UI (web, mobile, desktop)
- KYC / AML workflows
- External market data (all data is synthetic)

---

## 4. KEY DELIVERABLES

### 4.1 Core Systems

| Deliverable                      | Type        |
| -------------------------------- | ----------- |
| Matching Engine & Order Book     | Source Code |
| Gateway & Risk Services          | Source Code |
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
| Architecture Decision Records (ADRs) | Markdown |
| Low-Latency Technique Guide          | Markdown |
| Exchange Domain Primer               | Markdown |
| System Design Document               | Markdown |
| API / Protocol Reference             | Markdown |
| Deployment Guide + Docker Compose    | Markdown |
| README & Quick Start                 | Markdown |

### 4.4 Publication

| Deliverable           | Type           |
| --------------------- | -------------- |
| GitHub Repository     | Public Code    |
| Technical Blog Post   | Blog           |
| Architecture Diagrams | PNG / SVG      |
| Demo Scripts          | Shell / Python |

---

## 5. SUCCESS CRITERIA

### Correctness

- Orders match by price-time priority with no exceptions
- No lost orders or duplicate fills under load
- Sequence numbers are contiguous, monotonic, and validated
- Crash recovery restores a consistent state
- Test coverage ≥80% across core components

### Performance

- Order-to-match latency <1ms (average, single node)
- Market data jitter <100µs
- Sustained throughput ≥10,000 orders/second
- GC pressure is measured and documented

### Code Quality

- Single responsibility per component
- No paid dependencies
- Structured logs and distributed traces on critical paths

### Documentation

- Each module documents responsibilities and architectural rationale
- Each optimisation documents rationale, trade-offs, and measured impact
- ADRs cover significant architectural decisions
- A new developer can run the system within one hour

---

## 6. TECHNICAL ARCHITECTURE

### 6.1 Component Overview

```text
┌──────────────────┐
│   FIX Gateway    │ → Ingestion, session management, shard routing
└────────┬─────────┘
         │
┌────────▼─────────┐
│   Sequencer      │ → Event ordering and sequence coordination
└────────┬─────────┘
         │
┌────────▼─────────┐
│   Risk Engine    │ → Pre-trade validation
└────────┬─────────┘
         │
┌────────▼──────────────┐
│   Matching Engine     │ → Central limit order book
│   (Shard Manager)     │ → One shard per instrument
└────────┬──────────────┘
         │
         ├──→ Persistence
         ├──→ Market Data Publisher
         └──→ Observability
```

### 6.2 Design Principles

| Principle                     | In Practice                                                              |
| ----------------------------- | ------------------------------------------------------------------------ |
| **Deterministic processing**  | Given the same sequence of inputs, the engine produces identical outputs |
| **Clean architecture**        | Domain logic is isolated from infrastructure; dependencies point inward  |
| **Immutability**              | Orders and trades are never modified after creation                      |
| **Event sourcing**            | State changes are append-only events                                     |
| **Observable by default**     | Critical paths emit logs, traces, and metrics                            |
| **Testability**               | Components use dependency injection and avoid static state               |
| **Measure before optimising** | Changes require benchmark evidence                                       |

### 6.3 Sequence Model

Sequence is a core exchange concept. Every event entering the system receives a unique, monotonically increasing sequence number before processing.

| Property              | Requirement                                                                  |
| --------------------- | ---------------------------------------------------------------------------- |
| **Monotonic**         | Sequence numbers never decrease                                              |
| **Contiguous**        | Unexpected gaps indicate missing or delayed events and require investigation |
| **Single assignment** | Sequence numbers are assigned by a dedicated coordination component          |
| **Determinism gate**  | Downstream components process events in sequence order                       |
| **Recovery anchor**   | WAL and snapshots are indexed by sequence number                             |

Replaying the WAL from sequence `N` reconstructs identical state.

### 6.4 Low-Latency .NET Techniques

| Technique                             | Where Applied                          | Purpose                         |
| ------------------------------------- | -------------------------------------- | ------------------------------- |
| **System.IO.Pipelines**               | FIX gateway TCP I/O                    | Efficient socket processing     |
| **Bounded lock-free messaging**       | Inter-component communication          | Low-latency bounded hand-off    |
| **Span<T> / Memory<T>**               | FIX parsing, market data serialisation | Zero-copy buffer slicing        |
| **ArrayPool<T>**                      | Parsing and serialisation buffers      | Reduce allocations on hot paths |
| **Struct-based domain types**         | Price levels, order queue entries      | Reduce heap allocations         |
| **Object pooling**                    | Orders, execution reports              | Reduce GC pressure              |
| **BenchmarkDotNet + MemoryDiagnoser** | Performance-critical code              | Allocation and latency analysis |

---

## 7. PROJECT PHASES & MILESTONES

Four sequential phases across Q2–Q4 2026. Each phase ends with a demonstrable output.

### Phase 1 — Foundation

**Goal:** Single-instrument exchange on correct .NET foundations

- [ ] Core value types: `Order`, `Trade`, `PriceLevel`, `Instrument`
- [ ] Single-instrument matching engine
- [ ] FIX gateway stub
- [ ] Order pipeline implementation
- [ ] Level 1 market data feed
- [ ] Write-ahead log and crash recovery
- [ ] Unit test baseline

**Exit milestone:** System accepts orders and produces correct fills for one instrument.

---

### Phase 2 — Scaling & Observability

**Goal:** Multi-instrument support with full visibility

- [ ] Shard manager
- [ ] Risk engine
- [ ] OpenTelemetry, Prometheus, Grafana
- [ ] Synthetic order generator
- [ ] Integration test suite

**Exit milestone:** Multiple instruments handled concurrently with observable system behaviour.

---

### Phase 3 — Hardening & Performance

**Goal:** Verified performance and evidence-based optimisation

- [ ] Benchmark suite
- [ ] Object pooling where justified
- [ ] Stress testing at ≥10,000 orders/second
- [ ] Failure scenarios and replay validation
- [ ] Level 2 market data
- [ ] Additional order types
- [ ] ADR per significant optimisation

**Exit milestone:** Performance targets validated and documented.

---

### Phase 4 — Documentation & Publication

**Goal:** Complete, self-contained reference implementation

- [ ] Low-latency technique guide
- [ ] Exchange domain primer
- [ ] ADRs for major design decisions
- [ ] System design documentation
- [ ] Deployment guide
- [ ] Repository documentation and templates
- [ ] Technical blog post

**Exit milestone:** Repository is public and onboarding takes under one hour.

---

## 8. DEPENDENCIES

All dependencies are open-source and free.

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

| Risk                               | Likelihood | Impact | Mitigation                          |
| ---------------------------------- | ---------: | -----: | ----------------------------------- |
| Scope creep from new feature ideas |       High | Medium | Maintain strict scope boundaries    |
| Over-engineering                   |     Medium |   High | Deliver end-to-end capability first |
| Performance targets not met        |     Medium |   High | Benchmark early and iterate         |
| Solo developer delivery delays     |     Medium | Medium | Time-box phases                     |

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
| --------------- | ----------------------------------- | ---------- |
| **Developer**   | Implementation and validation       | ~50%       |
| **QA Engineer** | Testing and failure scenarios       | ~20%       |
| **Architect**   | ADRs and design decisions           | ~15%       |
| **Tech Writer** | Documentation and technical content | ~15%       |

---
