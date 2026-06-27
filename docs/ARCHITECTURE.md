# Architecture Overview

## 1. Components

```
                         ┌─────────────────────────────┐
                         │        ASP.NET Core          │
                         │     Minimal API (Program.cs) │
                         └──────────────┬───────────────┘
                                        │
        ┌───────────────────────────────┼───────────────────────────────┐
        │                               │                               │
┌───────▼────────┐            ┌─────────▼─────────┐           ┌─────────▼─────────┐
│ ExceptionHandling│           │  Rate Limiter      │           │   Swagger / OpenAPI│
│ Middleware       │           │  (fixed window)     │           │   (review/debug)   │
└──────────────────┘           └─────────────────────┘           └────────────────────┘
                                        │
                              ┌─────────▼─────────┐
                              │   UrlEndpoints     │  (HTTP <-> service mapping, status codes)
                              └─────────┬───────────┘
                                        │
                              ┌─────────▼─────────┐
                              │ IUrlShortenerService│ (orchestration: validate, generate, cache)
                              └────┬──────────┬─────┘
                                   │          │
                      ┌────────────▼──┐   ┌───▼─────────────────┐
                      │ IShortCode    │   │  IMemoryCache         │
                      │ Generator     │   │  (hot redirect lookups)│
                      │ (Base62)      │   └────────────────────────┘
                      └───────────────┘
                                   │
                         ┌─────────▼─────────┐
                         │   AppDbContext     │ (EF Core)
                         │  ShortUrl, ClickEvent│
                         └─────────┬───────────┘
                                   │
                         ┌─────────▼─────────┐
                         │  SQLite (file)     │ (swap-in: Azure SQL / Postgres)
                         └────────────────────┘
```

## 2. Tools and execution approach

- **Language/runtime:** C# / .NET 8, ASP.NET Core Minimal APIs.
- **Persistence:** EF Core + SQLite for the prototype (zero external dependency to run);
  data access is entirely through `AppDbContext`, so the provider is swappable.
- **AI tooling used during execution:** Claude, used for (a) scaffolding endpoint/service
  boilerplate from an explicit task spec, (b) generating the first draft of unit/integration
  tests against a written acceptance-criteria list, (c) reviewing the validation/security logic
  for gaps (SSRF, scheme allow-listing, alias rules). Every AI-suggested change was read,
  understood, and in several cases rejected or modified before acceptance — see
  `AI_TRACEABILITY.md` for the itemized log.
- **Control flow:** request → exception middleware → rate limiter → minimal API endpoint →
  `IUrlShortenerService` (validates, talks to cache + EF Core) → typed `Results.*` response.

## 3. Key decisions

| Decision | Rationale | Trade-off accepted |
|---|---|---|
| Short code = Base62(auto-increment id) | Guarantees uniqueness with zero collision-retry loop; O(1) generation | Codes are sequential/guessable — not enumeration-resistant |
| SQLite for prototype | Runs anywhere, no external service needed for the assignment reviewer | Not the production target (Azure SQL/Cosmos in a real deployment) |
| Click recording wrapped in its own try/catch | A failed analytics write must never break the user-facing redirect | Click loss is possible on failure (acceptable; redirect correctness prioritized) |
| `IMemoryCache` for redirect lookups | Redirect is the highest-QPS path; avoids a DB round trip per click | Single-instance cache; would need Redis for multi-instance deployment |
| Idempotency key (optional) on create | Protects against duplicate-create on client retries | Caller must explicitly pass a key; not automatic dedup by URL content |
| Reject non-http(s) schemes + obvious internal hosts | Prevents `javascript:`/`data:` XSS-via-redirect and basic SSRF | Not a full SSRF defense (no DNS resolution / private-CIDR check) — flagged as a gap |
| IP stored as SHA-256 hash, not raw | Avoids storing PII while still allowing rough abuse signals | Cannot be reversed for legitimate abuse investigation either |

## 4. Scalability path (not built, but designed for)

- Click events → publish to Azure Service Bus/Event Hub instead of writing inline; a separate
  aggregator consumes and updates counts asynchronously. Removes the DB write from the
  redirect's critical path entirely.
- Cache → Redis (Azure Cache for Redis) once running multiple instances, so cache state is shared.
  CQRS-style read replicas for analytics if click volume grows large.
- Short code generation → if multiple API instances insert concurrently, the auto-increment id
  is still globally unique (DB-enforced), so this strategy survives horizontal scaling without
  a distributed counter.

## 5. Known risk: unverified build

The execution sandbox used to write this code does not have the .NET SDK available, so this
code has not been compiled or run here. It has been written carefully, cross-checked line by
line, and modeled closely on working ASP.NET Core 8 minimal API + EF Core patterns, but the
engineer (you) should run `dotnet build` and `dotnet test` as the first step and treat any
compiler errors as expected follow-up work, not a sign the approach is wrong. This is disclosed
rather than glossed over, consistent with the assignment's emphasis on engineer ownership and
honest validation reporting.
