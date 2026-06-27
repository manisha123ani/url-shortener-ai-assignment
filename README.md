# URL Shortener — AI-Assisted Engineering Assignment

A working URL shortener prototype (ASP.NET Core 8 minimal API + EF Core/SQLite) built to
demonstrate engineer-led, AI-accelerated execution: requirement understanding, task
decomposition, disciplined AI-assisted implementation, and validation.

> **Note on this submission:** the development sandbox used to produce this prototype does
> not have the .NET SDK installed, so the code below has been written and reasoned through
> carefully but **not compiled/run in this environment**. Section "Known Risk: Unverified
> Build" in `docs/ARCHITECTURE.md` calls this out explicitly, and the **Setup** steps below
> are exactly what you'd run locally to build, test, and verify it. Treat first-run compiler
> errors (if any) as expected follow-up, not as something quietly hidden.

## What's included

```
UrlShortener/
├── src/UrlShortener.Api/        # Minimal API: endpoints, services, EF Core, middleware
├── tests/UrlShortener.Tests/    # xUnit unit + WebApplicationFactory integration tests
└── docs/
    ├── ARCHITECTURE.md          # Components, control flow, key decisions, trade-offs
    ├── SCENARIOS.md             # 3 worked scenarios: greenfield / brownfield / ambiguous
    ├── AI_TRACEABILITY.md       # AI-generated / edited / rejected log with rationale
    └── FINAL_SUMMARY.md         # Plan, risks, assumptions, limitations
```

## Features

- `POST /api/urls` — create a short URL (optional custom alias, expiry, idempotency key)
- `GET /{code}` — redirect to the original URL, records a click event (best-effort, non-blocking)
- `GET /api/urls/{code}/analytics` — total clicks, clicks-by-day, top referrers
- `GET /health` — health check
- Swagger UI at `/swagger`
- Rate limiting (fixed-window) on create and redirect paths
- In-memory caching of hot redirect lookups
- Input validation incl. scheme allow-listing and basic SSRF guard
- Idempotent create (via optional `idempotencyKey`)

## Setup

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
cd UrlShortener
dotnet restore
dotnet build

# Run the API (creates urlshortener.db via SQLite on first run)
dotnet run --project src/UrlShortener.Api

# Swagger UI: https://localhost:<port>/swagger
```

### Run tests

```bash
dotnet test
```

### Try it out

```bash
# Create a short URL
curl -X POST http://localhost:5000/api/urls \
  -H "Content-Type: application/json" \
  -d '{"originalUrl":"https://learn.microsoft.com/aspnet/core"}'

# -> { "shortCode": "1", "shortUrl": "http://localhost:5000/1", ... }

# Follow the redirect
curl -i http://localhost:5000/1

# Check analytics
curl http://localhost:5000/api/urls/1/analytics
```

## Testing approach

- **Unit tests**: `Base62ShortCodeGenerator` (encoding correctness, determinism), `UrlValidator`
  (accepted/rejected URL shapes, alias rules) — pure logic, no I/O.
- **Service tests**: `UrlShortenerService` against a real SQLite (`:memory:`) connection, not
  EF's `InMemory` provider — chosen specifically because `InMemory` silently ignores unique-index
  violations and would mask alias-collision bugs.
- **Integration tests**: `WebApplicationFactory<Program>` boots the full pipeline (DI, rate
  limiting, middleware) and exercises real HTTP create → redirect → analytics flows, plus
  error paths (400, 404, 409).

## Limitations & trade-offs

See `docs/FINAL_SUMMARY.md` for the full list. Headline items:
- Short codes derived from the auto-increment DB id are sequential/guessable — acceptable for
  a prototype, not for a public-facing product expecting enumeration resistance.
- Click recording is synchronous-but-isolated (own try/catch, never blocks the redirect) rather
  than queued; fine at prototype scale, would need a queue (Service Bus/Event Hub) at production scale.
- SQLite is used for zero-friction local setup; the data access layer is provider-agnostic via
  EF Core, so swapping to Azure SQL/Postgres is a connection-string + package change.
- No auth/authz layer — out of scope per the assignment's focus on AI-assisted execution, but
  flagged as a pre-production gap in `FINAL_SUMMARY.md`.
