# Final Engineering Summary

## Plan & rationale

Built a minimal-but-real URL shortener: create → redirect → analytics, on ASP.NET Core 8 +
EF Core/SQLite, prioritizing a working, testable vertical slice over breadth of features.
Order of build: core create/resolve (the load-bearing path) → validation/security hardening
→ analytics → reliability (caching, rate limiting, idempotency) → tests across all layers →
documentation. This order was chosen so that every later addition had a correct, tested
foundation underneath it rather than bolting reliability concerns on at the end.

## Artifacts produced

- Runnable API (`src/UrlShortener.Api`) — entities, EF Core context, service layer, minimal
  API endpoints, exception middleware, rate limiting, Swagger.
- Test suite (`tests/UrlShortener.Tests`) — unit tests (validator, code generator), service
  tests against real SQLite, HTTP-level integration tests via `WebApplicationFactory`.
- `ARCHITECTURE.md`, `SCENARIOS.md` (3 worked scenarios), `AI_TRACEABILITY.md`, this summary.

## Risks, trade-offs, and validation performed

| Risk / trade-off | Mitigation taken | Residual risk |
|---|---|---|
| Build unverified (no .NET SDK in this sandbox) | Code modeled closely on standard ASP.NET Core 8 / EF Core patterns; every file reviewed line-by-line | First `dotnet build` may surface small issues (e.g. a package version mismatch); flagged upfront in README rather than hidden |
| Short codes are sequential/guessable | Documented; acceptable for a prototype | Not enumeration-resistant — would need random codes + collision retry for a public product |
| Click analytics written inline (not queued) | Isolated in try/catch so it never blocks the redirect | At very high scale, DB write contention on click events would need offloading to a queue |
| SSRF guard is a literal hostname blocklist | Blocks `localhost`/loopback/link-local | Does not resolve DNS or check private CIDR ranges behind a hostname — a determined attacker could route around it |
| No phishing/malware URL reputation check | Out of scope — would require an external safe-browsing API not available here | Shortener could mask a malicious destination; flagged as a real gap, not solved |
| No authentication/authorization | Out of scope per assignment focus | Anyone can create/read any short URL's analytics; would need API keys or user accounts pre-production |
| Single-instance cache (`IMemoryCache`) | Fine for one instance | Would not stay consistent across multiple horizontally-scaled instances — Redis is the documented next step |

## Assumptions made (and why)

- Assumed "analytics" means click count + time series + referrer, since the assignment names
  "analytics" without specifying fields, and these are the standard shortener-analytics fields.
- Assumed SQLite is an acceptable persistence choice for a prototype reviewed by a human, given
  no specific database was mandated, in exchange for zero external setup friction.
- Assumed the reviewer runs this on a machine with the .NET 8 SDK, since that wasn't available
  in the authoring sandbox to self-verify.

## Limitations

- Not load-tested (no load-testing tool available in this sandbox); reliability reasoning is
  analytical (see Scenario B in `SCENARIOS.md`), not measured.
- No CI pipeline configured (out of scope for a 2-3 day take-home, but would be the next addition).
- No multi-instance/distributed deployment story implemented, only documented.

## What I'd do with more time

1. Run `dotnet build`/`dotnet test`, fix any compiler issues, and capture real test-run output.
2. Add a load test (e.g. `k6` or `bombardier`) against `/{code}` to validate the caching claim
   in Scenario B with real numbers instead of analytical reasoning.
3. Swap the SSRF literal-blocklist for actual DNS-resolution + private-CIDR checking, with a
   timeout so it can't become a new latency/availability risk on the create path.
4. Add basic API-key auth so analytics aren't publicly readable for any code.
