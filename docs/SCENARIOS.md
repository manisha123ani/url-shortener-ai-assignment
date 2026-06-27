# Three Scenarios: Greenfield, Brownfield, Ambiguous

Each scenario shows: requirement → decomposition → AI-assisted execution → validation.

---

## Scenario A — Greenfield: "Add click analytics to the shortener"

**Requirement (as given):** "Users should be able to see how many times their short link
was clicked, broken down by day, plus where the clicks came from."

### 1. Requirement understanding
Normalized into: a read endpoint `GET /api/urls/{code}/analytics` returning total clicks,
a daily time series, and a referrer breakdown. Ambiguity resolved: "where clicks came from"
interpreted as HTTP `Referer` header (not GeoIP — no geo data available without an external
service, and the assignment scope doesn't mention one). Decision recorded in
`ARCHITECTURE.md` rather than silently assumed.

### 2. Task decomposition
1. Add `ClickEvent` entity + EF Core mapping (depends on: existing `ShortUrl` entity)
2. Record a `ClickEvent` row on every successful redirect (depends on: #1; must not block redirect)
3. Increment a denormalized `ClickCount` on `ShortUrl` for O(1) total-count reads (depends on: #1)
4. Build `GetAnalyticsAsync` aggregation (group by day, group by referrer) (depends on: #1–#3)
5. Expose `GET /api/urls/{code}/analytics` endpoint (depends on: #4)
6. Tests: unit (aggregation logic), integration (click → analytics reflects count) (depends on: #1–#5)

### 3. AI-assisted execution
- Prompted for: "EF Core entity + migration-free SQLite mapping for a click-event table with
  an index suited for `(shortUrlId, clickedAtUtc)` range queries."
- AI draft initially put the click-recording call **inside** the same `SaveChanges` transaction
  as the redirect-resolution read. **Rejected**: a slow/failed analytics write should never
  delay or break the 302 response. Edited to wrap click recording in its own try/catch in the
  endpoint, decoupled from the resolve path. See `AI_TRACEABILITY.md` entry #4.
- AI draft for referrer grouping used the raw `Referer` header value as the group key, which
  would create a near-unbounded cardinality of one-off "groups" for different referrer URLs
  with effectively the same source — but resolving that fully (e.g. registrable-domain
  extraction) was determined to be over-engineering for the assignment's scope; flagged as a
  limitation rather than partially fixed.

### 4. Validation
- Unit test: clicking 3 times → `TotalClicks == 3`.
- Unit test: referrer grouping puts no-referrer hits under `"(direct)"`.
- Integration test: full create → 3 redirects → analytics call reflects count 3.
- Risk identified: analytics is per-instance accurate but not real-time consistent across
  multiple API instances without a shared cache invalidation strategy — documented, not solved,
  since this prototype runs as a single instance.

---

## Scenario B — Brownfield: "The redirect endpoint is slow under load; fix it"

**Requirement (as given, intentionally under-specified like a real bug report):** "Redirects
feel slow when there's traffic."

### 1. Requirement understanding
Translated "slow under load" into a concrete, testable claim: every redirect currently does a
DB read on the hot path with no caching, so latency scales with DB contention as concurrent
requests rise. Ambiguity resolved by stating the assumption explicitly rather than guessing
silently: assumed the bottleneck is the synchronous DB lookup + click write, not the framework
itself, since minimal APIs add negligible overhead. This assumption is stated, not hidden.

### 2. Codebase reasoning (impacted areas)
- `UrlShortenerService.ResolveAsync` — the read path called on every single redirect.
- `UrlShortenerService.RecordClickAsync` — a write that was, in an earlier draft, executed
  synchronously and inline with the redirect response, meaning every redirect waited on two
  DB round trips minimum (read short URL + insert click + update count).
- No caching layer existed in the earlier draft between the endpoint and EF Core.

### 3. Task decomposition
1. Add `IMemoryCache` lookup in `ResolveAsync`, keyed by short code, TTL 5 minutes
   (depends on: nothing — additive)
2. Ensure cache entries are short-lived enough that expiry/deactivation changes propagate
   within an acceptable window (depends on: #1)
3. Confirm click recording does not block the redirect response — wrap in try/catch, log on
   failure, always still redirect (depends on: existing endpoint code)
4. Add `AsNoTracking()` to all read-only EF Core queries to skip change-tracking overhead
   (depends on: nothing — additive, mechanical)
5. Tests: confirm a cached lookup returns the same result as a cold one; confirm an expired
   link still correctly returns 410 even when previously cached (depends on: #1–#2)

### 4. AI-assisted execution
- Asked AI to "propose 3 ways to reduce DB load on a hot redirect path and the trade-off of
  each." It proposed in-memory cache (chosen, simplest for single-instance prototype), Redis
  (correct for multi-instance — documented as the production path, not implemented to avoid
  scope creep), and pre-warming all codes into memory at startup (**rejected** — doesn't scale
  past a small dataset and reintroduces a memory growth risk).
- AI's first cache-invalidation draft cached the entity **after** evaluating expiry, meaning an
  expired link, once cached, could never "un-expire" — fine — but it also meant a link
  deactivated mid-TTL would keep redirecting for up to 5 minutes. Flagged this as an accepted
  trade-off (documented in `ARCHITECTURE.md`) rather than silently shipping it.

### 5. Validation
- Unit test: resolving an expired link returns `Expired`, not `Found`, even though the read
  path is identical code regardless of cache hit/miss.
- Manual reasoning check (no load-testing tool in this sandbox): cache reduces DB reads for
  the dominant case (popular links resolved repeatedly), while cold/unique links still hit the
  DB once — correctly bounding worst case to "no worse than before."
- Risk documented: 5-minute staleness window for deactivation is an explicit, accepted
  trade-off, not an oversight.

---

## Scenario C — Ambiguous: "Make sure people can't abuse the shortener"

**Requirement (as given, genuinely ambiguous):** "Make sure people can't abuse the shortener."

### 1. Requirement understanding — disambiguation
"Abuse" could mean: (a) spamming the create endpoint, (b) using the shortener to mask
malicious/phishing links, (c) using it as an open redirector to internal infrastructure
(SSRF), or (d) scraping/enumerating all short codes. Rather than picking one silently, all
four were named explicitly and scoped:
- (a) and (c) → addressable within this prototype's time box.
- (b) → out of scope (would need a URL-reputation/safe-browsing API integration — an external
  dependency not available in this sandbox; documented as a limitation, not attempted).
- (d) → partially mitigated as a side-effect of Base62(id) encoding being sequential rather
  than fully prevented; full prevention (random codes + collision retry) was considered and
  explicitly **not** adopted for this prototype to keep code generation O(1) — documented trade-off.

### 2. Task decomposition
1. Rate-limit `POST /api/urls` (fixed window, 30/min) to blunt spam creation (depends on: nothing)
2. Rate-limit `GET /{code}` redirects separately, more generously, since legitimate traffic
   volume there is naturally higher (depends on: nothing)
3. Validate and allow-list URL schemes (`http`/`https` only) to block `javascript:`/`data:`
   payloads being "shortened" into something that looks like a normal link (depends on: nothing)
4. Block obviously-internal hosts (`localhost`, loopback, link-local) to reduce SSRF-via-
   redirect risk (depends on: #3)
5. Tests for each rejection path (depends on: #1–#4)

### 3. AI-assisted execution
- Asked AI for "a list of unsafe URL schemes commonly abused in open-redirector attacks."
  Cross-checked the list against general security knowledge before encoding it — accepted
  `javascript:`, `data:`, `file:`, rejected one AI-suggested addition (`mailto:`) since
  `mailto:` is not a meaningful abuse vector for a redirect-based attack and excluding it
  isn't a security improvement, just unnecessary restriction. See `AI_TRACEABILITY.md` #9.
- AI's first SSRF-guard draft attempted full DNS resolution + private-CIDR checking inline in
  the request path. **Rejected as over-scoped for this prototype** — synchronous DNS lookups
  on every create call add latency and a new failure mode, and a sandbox without full network
  access can't reliably test it. Replaced with the simpler literal-hostname blocklist that's
  documented as partial, not complete, protection.

### 4. Validation
- Unit tests assert each rejected scheme/host individually (`javascript:`, `data:`, `ftp:`,
  `localhost`, `127.0.0.1`).
- Explicit, named residual risks (not silently accepted): no DNS-resolution-based SSRF
  protection, no phishing/malware URL reputation check, codes are enumerable in sequence.
  These are listed in `FINAL_SUMMARY.md` as follow-up work, since a reviewer should see what
  was deliberately deferred versus what was missed.
