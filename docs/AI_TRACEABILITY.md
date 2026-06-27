# AI-Assisted Execution: Traceability Log

Format per entry: **Task** → **Prompted for** → **AI output** → **Decision** (accepted /
edited / rejected) → **Rationale**.

| # | Task | Prompted for | Decision | Rationale |
|---|------|--------------|----------|-----------|
| 1 | Short code generation | "Deterministic, collision-free short code strategy with O(1) generation" | **Accepted** (Base62 of auto-increment id) | Simplest approach that guarantees uniqueness via the DB's own identity column; sequential/guessable trade-off explicitly documented rather than hidden |
| 2 | Short code generation (alt.) | Same prompt also surfaced "random code + retry-on-collision" | **Rejected** | Adds an unbounded retry loop and extra DB round-trips for no benefit at this scale; kept as the fallback `RandomCode()` method for future custom-alias collision handling instead of the primary path |
| 3 | URL validation | "Validate input URLs are safe to redirect to" | **Edited** | Initial AI draft allowed any scheme matching a regex for "looks like a URL"; tightened to an explicit `http`/`https` allow-list instead of a deny-list, since allow-lists fail closed and deny-lists don't |
| 4 | Click recording placement | "Record analytics on each redirect" | **Edited** | AI's first draft wrote the click event inside the same transaction/scope as resolving the short URL, meaning a slow analytics write could delay the redirect response; moved to a separately try/caught call so redirect correctness is never gated on analytics success |
| 5 | Caching strategy | "Reduce DB load on the redirect hot path" | **Accepted, scoped down** | AI offered cache, Redis, and full pre-warm; picked `IMemoryCache` for this single-instance prototype and documented Redis as the production path rather than building it now (scope discipline) |
| 6 | Idempotency | "Avoid duplicate short URLs on client retry" | **Accepted** | Optional caller-supplied idempotency key, checked before insert; simple and matches common API idempotency patterns (e.g. Stripe-style keys) without inventing a content-hash scheme that the assignment doesn't call for |
| 7 | Rate limiting | "Protect create/redirect endpoints from abuse" | **Accepted, simplified** | AI suggested .NET 8's built-in `RateLimiter` middleware with fixed-window policies; accepted as sufficient for a prototype rather than reaching for a distributed limiter (Redis-backed) that would need infrastructure not present here |
| 8 | Tests for the service layer | "Unit-test the URL shortener service against a database" | **Edited** | First draft used EF Core's `InMemory` provider; rejected and replaced with a real SQLite `:memory:` connection because `InMemory` does not enforce unique-index constraints, which would have silently hidden alias-collision bugs |
| 9 | Unsafe scheme list | "List schemes commonly abused in open-redirector / XSS-via-redirect attacks" | **Edited** | Accepted `javascript:`, `data:`, `file:`; rejected the AI's suggestion to also exclude `mailto:` since it isn't a redirect-based attack vector and excluding it adds no real protection |
| 10 | SSRF protection | "Prevent the shortener being used to reach internal infrastructure" | **Rejected (initial), replaced with reduced scope** | AI's draft did full DNS resolution + private-CIDR checking synchronously in the create path; rejected as too heavy/untestable for this sandbox (no reliable outbound DNS access to verify), replaced with a literal hostname blocklist and the gap explicitly documented as partial protection |
| 11 | IP storage for analytics | "Store enough info to detect abuse without storing raw PII" | **Accepted, modified** | AI suggested storing the raw IP "for now, hash later"; changed to hash at write-time (SHA-256) immediately, since "hash later" tends to never happen and raw IP storage is the actual privacy risk being avoided |
| 12 | Documentation structure | "Generate API docs from the endpoint definitions" | **Accepted** | Used built-in Swagger/OpenAPI generation rather than hand-written API docs, since the minimal-API attributes already describe request/response shapes accurately and a hand-maintained doc would drift |

## Quality gates applied before accepting any AI output

- **Read every line** before accepting — no AI output was merged unread.
- **Security review pass**: every endpoint touching user input (URL, alias) was checked
  specifically for injection/redirect-abuse risk, not just "does it compile."
- **Test-first cross-check**: for service logic, tests were written against the *intended*
  behavior (from the task's acceptance criteria) and then checked against the implementation —
  not generated from the implementation itself, to avoid tests that just mirror bugs.
- **Scope discipline**: AI proposals that solved a more general problem than the assignment
  asked for (full SSRF DNS resolution, distributed rate limiting, content-hash dedup) were
  deliberately scoped down or deferred, with the deferral written down rather than silently
  dropped.
- **No high-impact change shipped without this log entry** — every entry above corresponds to
  a real review decision, not a post-hoc summary.
