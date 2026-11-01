# YTube — System Design Study Guide

Each step of the project introduces real system design concepts. This file explains **what** we built,
**why** it's designed that way, and the **trade-offs**, so you can explain it in an interview.
Every section ends with self-check questions.

---

## Step 1 — Foundation (gateway, services, auth)

| Concept | Where in the code | One-line why |
|---|---|---|
| **API Gateway** | `Gateway/YTube.Gateway` (YARP) | One entry point: clients don't need to know where each service lives; cross-cutting rules (auth, rate limits, CORS) live in one place. |
| **Service decomposition** | `Services/*` | Split by business capability (auth, users, videos, feed) so each can change, deploy and scale independently. |
| **Database per service** | `YTube_AuthDb`, `YTube_UserDb` | No service reads another's tables, so schemas can change without breaking others. Services share data through APIs or events, never through a shared DB. |
| **Stateless authentication (JWT)** | `JwtTokenService`, `AddYTubeJwtAuthentication` | Any service can verify a token with the signing key without calling Auth, so Auth isn't a bottleneck on every request. |
| **Rate limiting** | Gateway `AddRateLimiter` | A token bucket for overall traffic, and a strict fixed window on `/api/auth` against brute-forcing passwords and OTPs. |
| **Caching with TTL** | Pending sign-ups in Redis (5 min) | Short-lived data that should disappear on its own doesn't belong in SQL. |
| **Health checks** | `/health` on every service | Load balancers and orchestrators need to know whether an instance is alive. |
| **Correlation IDs** | `CorrelationIdMiddleware` | One ID follows a request across all services' logs, which is the first step towards distributed tracing. |
| **Centralised error handling** | `GlobalExceptionHandler`, `Result<T>` | Expected failures are values (`Result`); real bugs are logged once and become a clean 500 with a reference ID. |

**Self-check:** Why does every service validate the JWT again if the gateway already did? *(Defense in depth: a service might be reached without going through the gateway, e.g. from inside the network.)*

---

## Step 2 — Public browsing with login-only actions

### Authorization at two layers
- **Gateway:** each route has a policy: `anonymous` (public) or `default` (valid JWT required). Routes can match on **HTTP method**, so `GET /api/videos/x` is public but `PUT /api/videos/x/like` is not. Rejecting at the edge saves the services from handling junk traffic.
- **Service:** `[Authorize]` on the same actions. Never rely on a single layer.

### Optional authentication
Public endpoints still read a token **if one is sent**. Same URL, and the answer is personalised (`likedByMe: true`) for signed-in users. An invalid token on a public endpoint doesn't fail; the request is simply treated as a guest's.

### Synchronous service-to-service calls
Feed calls Video over HTTP (`VideoServiceClient`).
- **Header propagation** (`HeaderPropagationHandler`): the user's token and correlation ID are forwarded, so Video knows who is asking and the logs stay linked.
- **Timeout** (5 s): a slow dependency must never hang our own requests.
- **Trade-off:** Feed is now *coupled* to Video's availability. If Video is down, the feed fails. Later we fix this with resilience patterns (retries, circuit breaker) and an **event-driven read model**, where Feed keeps its own copy of the video data.

### Pagination
Offset paging (`page`, `pageSize`) is simple, but if new videos arrive while you scroll you can see duplicates or skip items. **Cursor paging** ("give me 12 after video X") fixes that; it's planned for the scale step.

### Idempotency
`PUT /like` and `DELETE /like` set state instead of toggling it. Sending the same request twice gives the same result, so a retry after a network error is always safe. A "toggle like" endpoint would flip it back.

### Optimistic UI
The Like button flips **immediately** and rolls back if the server says no, so the app feels instant even with network latency.

**Self-check:** Why is `PUT /videos/{id}/like` better than `POST /videos/{id}/toggle-like`? *(It's idempotent, so it's safe to retry.)*

---

## Step 3 — Sessions done right + saved theme preferences

### 3.1 Why two tokens?

| | Access token | Refresh token |
|---|---|---|
| Lifetime | **15 minutes** | **7 days** |
| Sent to | Every API call (`Authorization: Bearer …`) | Only `/api/auth/refresh` and `/api/auth/logout` |
| Checked by | Any service, *without* a DB lookup (signature check) | Auth service only, *with* a DB lookup |
| Can be revoked? | No, it just expires | Yes, it's deleted or marked revoked in the DB |

A JWT can't be revoked (services don't call Auth to check it), so we keep it **short-lived**. The refresh token is the long-lived "remember me". Because it's checked against the database, **logout and theft response actually work**. This is the standard trade-off between *stateless speed* and *stateful control*.

### 3.2 Where each token is stored (the security core of this step)

| Storage | XSS can steal it? | Sent automatically (CSRF risk)? | Survives reload? |
|---|---|---|---|
| `localStorage` (old version) | **Yes:** any injected script can read it | No | Yes |
| JavaScript memory (access token now) | Only while the page is open, and only the short-lived token | No | No |
| **httpOnly cookie** (refresh token now) | **No:** JavaScript cannot read it | Yes, so it needs SameSite | Yes |

Cookie settings (`AuthController.CookieOptions`):
- `HttpOnly`: no JavaScript access at all. You verified this: `document.cookie` doesn't show it.
- `SameSite=Strict`: the browser won't attach it to requests started by *other sites*, which blocks CSRF. (`localhost:4200` → `localhost:5000` counts as the same site, because ports don't matter for "site".)
- `Path=/api/auth`: it isn't sent with every API call, only to the two endpoints that need it.
- `Secure`: HTTPS only (relaxed in Development because we run plain http).

Cross-origin plumbing: Angular (port 4200) and the gateway (port 5000) are different **origins**. That needs `withCredentials: true` on the auth calls and `AllowCredentials()` with an explicit origin list in the gateway's CORS policy. A wildcard origin is not allowed together with credentials.

### 3.3 Rotation and theft detection
Every refresh **rotates**: the old refresh token is revoked and a new one is issued (`AuthService.RefreshAsync`).
If a revoked token is ever used again, someone probably copied it. The server then **revokes every session of that user** (reuse detection), which forces both the real user and the attacker to sign in again.

**The race problem and the grace window.** Two tabs share one cookie. If both refresh at the same instant, the second one arrives with the token the first one just rotated, which would look like theft and log the user out everywhere. So a token that was **rotated less than 30 s ago** is accepted once more (`RotationGracePeriod`).
*Trade-off:* an attacker who replays a stolen token within those 30 s isn't detected. Production systems (Auth0, Okta) use the same "reuse interval" idea.

### 3.4 Keeping the user signed in

```
App opens ──► POST /api/auth/refresh (cookie sent automatically)
                 ├─ 200 → access token in memory → signed in (no password!)   = "silent refresh"
                 └─ 401 → guest (still allowed to browse)

While signed in:
  timer fires 60 s before expiry ──► refresh in the background          = "proactive refresh"
  a request still gets 401 (laptop slept) ──► refresh once, retry it     = interceptor safety net
  refresh rejected ──► session over → guest
```

- **Single-flight refresh:** if the timer, the interceptor and startup all ask at once, they share **one** HTTP call (`refreshInFlight$` + `shareReplay`). Otherwise they'd race each other's rotations.
- **App initializer:** the app waits for the silent refresh before the first screen, so it never flashes "Sign in" for a user who is actually signed in.

### 3.5 Cross-tab sync
Tabs share cookies but **not memory**. `BroadcastChannel('ytube-auth')` is a message bus between tabs of the same origin:
- Sign out in one tab → the others drop their in-memory token immediately.
- Sign in in one tab → the others run a silent refresh and become signed in.

This is the **pub/sub** pattern at browser scale. You'll meet the same idea at server scale with RabbitMQ.

### 3.6 Theme preference: where does the data live?

**Source of truth** depends on who you are:
- **Guest:** the browser (`localStorage['ytube.theme']`). There's no account to store it in.
- **Signed in:** the **User service database** (`YTube_UserDb.UserPreferences`). It follows you to every device. The browser copy stays as a **local cache** for instant startup.

**Why the User service, not Auth?** Auth's job is *identity* (who are you). Preferences are *profile data*. Keeping them apart is the **single responsibility** principle at service level. The User service never touches Auth's database: the only link is the user ID (`sub` claim) inside the JWT.

**Merge rule on sign-in** (`ThemeService.syncFromServer`):
1. The account has a saved theme → it wins, so it's consistent across devices.
2. The account has none (`isDefault: true`) and the guest picked one → upload it, so the choice isn't lost.

### 3.7 Cache-aside with Redis

```
GET preferences:   Redis hit? ──yes──► return
                       └─no──► read SQL ──► store in Redis (TTL 30 min) ──► return

PUT preferences:   write SQL ──► DELETE the Redis key   (next GET reloads fresh data)
```

- **Why cache it?** Every app start reads preferences, but they change rarely. That's a high read/write ratio, the textbook case for a cache.
- **Why a TTL?** If an invalidation is ever missed, stale data still expires on its own. A TTL is a safety net.
- **Why delete instead of update on write?** With two concurrent writes, "set cache" calls can land in the wrong order and leave the *older* value cached indefinitely. Deleting can't go stale: the next read always fetches the truth.
- **Graceful degradation:** if Redis is down, `PreferencesService` logs a warning and uses SQL. The cache is an optimisation, never the source of truth, so its failure must not break the feature.

You saw it work: after the first read a `user:prefs:{id}` key appeared with a TTL of about 1800 s; after a PUT the key was gone.

### 3.8 Theming in the frontend
- **Design tokens:** components only use *semantic* colors (`bg-surface`, `text-muted`, `bg-accent`). A theme is just a set of CSS variables on `<html data-theme="…">`, so adding a theme means adding variables, not editing components.
- **No flash on load:** a tiny inline script in `index.html` sets `data-theme` from `localStorage` *before the first paint*, before Angular even downloads. Without it, dark-theme users would see a white flash on every load.
- **"System" theme** follows the OS (`prefers-color-scheme`) and updates live when the OS switches.

**Self-check questions**
1. Why not just make the access token last 7 days? *(It can't be revoked, so a stolen one would work for 7 days.)*
2. What attack does `HttpOnly` stop, and which one does `SameSite` stop? *(XSS token theft; CSRF.)*
3. What happens if two tabs refresh at the same time without the grace window? *(Reuse detection fires and every session is revoked.)*
4. In cache-aside, why invalidate instead of updating the cache on write? *(It avoids stale values from racing writes.)*
5. Redis crashes. What happens to preferences? *(They're served from SQL with only higher latency: graceful degradation.)*
6. Why do preferences live in the User service and not Auth? *(Identity vs profile data: single responsibility, and they can scale or change independently.)*

### Known limits (to improve later)
- **A brief flash of the wrong theme on a new device.** On first load there, the local copy shows "System" for a moment until the server value arrives.
- **Likes are in memory.** They're lost on restart; the Video service gets a real database in Phase 2.
- **The signing key is shared.** Every service has the same HMAC key. Production uses **asymmetric keys (RS256 + JWKS)**: only Auth can sign, and the others can only verify.
