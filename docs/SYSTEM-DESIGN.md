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
| **Layered services (Api / BLL / DAL / Entities)** | `Services/<Name>/*` | Controllers stay thin, all rules live in the BLL, and the DAL implements interfaces the BLL declares (dependency inversion), so business logic never depends on EF Core or SQL. |
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
- ~~Likes are in memory~~ — fixed in Step 4: likes now live in the Video database.
- **The signing key is shared.** Every service has the same HMAC key. Production uses **asymmetric keys (RS256 + JWKS)**: only Auth can sign, and the others can only verify.

---

## Step 4 — Video upload, async processing and adaptive streaming

```
Browser ──multipart──► Gateway ──► Video service
                                     1. save original  → D:\YTubeMedia\uploads\{id}\source.mp4
                                     2. insert row      → status Queued
                                     3. publish         → "video.uploaded"  ──► RabbitMQ
                                     ◄── 200 { status: "Queued" } (immediately)
                                                                   │
                         Processing worker ◄── queue "processing.video-uploaded" (prefetch 1)
                           ffprobe → ladder → ffmpeg ×N → master.m3u8 → thumbnail
                           publishes "video.processing.progress / completed / failed"
                                                                   │
                         Video service ◄── queue "video.processing-events" → status Processing → Ready/Failed
Watch page ──► GET /media/videos/{id}/hls/master.m3u8 ──► hls.js picks & switches renditions
```

### 4.1 Why processing is asynchronous
Transcoding a 10-minute video into six qualities takes minutes of CPU. If the upload request waited for
it, the browser would time out, a server restart would lose the work, and ten uploads at once would
starve the API of threads. So the request does only the **fast, essential** part (store the file,
record it, enqueue a job) and returns. The slow part happens in a **separate process** that can be
scaled, restarted and retried independently. The user sees status `Queued → Processing x% → Ready`.

### 4.2 Message broker concepts (RabbitMQ)
| Concept | In YTube | Why |
|---|---|---|
| **Topic exchange** | `ytube.events` | Publishers send to an exchange with a *routing key*; they don't know who listens. |
| **Routing key / binding** | `video.uploaded`, `video.processing.*` | Queues subscribe with patterns; `*` matches one word, so one queue gets progress + completed + failed. |
| **Durable queue + persistent message** | all queues / messages | A broker restart doesn't lose jobs. |
| **Publisher confirms** | `RabbitMqPublisher` | `PublishAsync` returns only after the broker has stored the message. |
| **Manual ack** | `RabbitMqConsumerService` | The message leaves the queue only after the handler succeeds. If the worker crashes mid-encode, the job is redelivered. |
| **Prefetch** | worker = 1, Video = 10 | Back-pressure: a worker holds one CPU-heavy job at a time. |
| **Competing consumers** | run 2+ workers | Each job goes to exactly one of them — horizontal scaling with zero code changes. |
| **Dead-letter queue (DLQ)** | `<queue>.dlq` | A message whose handler throws goes here instead of looping forever ("poison message"). |
| **Requeue on shutdown** | `OperationCanceledException` → nack(requeue) | Stopping a worker mid-job isn't the job's fault, so it goes back for another worker. |

**Delivery guarantee:** this is *at-least-once* delivery — a message can arrive twice (e.g. the worker
finished but crashed before acking). So every handler is **idempotent**:
- the worker deletes old HLS output before writing (`PrepareHlsDirectory`);
- the Video service ignores a late "progress" for a video that's already Ready/Failed.

**Business failure vs crash:** a corrupt file is an *expected* outcome — the worker publishes
`VideoProcessingFailed` with a friendly reason and acks. Only unexpected exceptions reach the DLQ.

### 4.3 The dual-write problem (and the outbox pattern)
Upload does two writes to two systems: the **database** row and the **RabbitMQ** message. If the DB write
succeeds and the publish fails, the video is stuck in "Queued" forever. Today we catch that and mark it
Failed. The robust fix is the **transactional outbox**: save the message in an `Outbox` table *in the same
DB transaction* as the row, and a background relay publishes it later — the two writes become one. That's
a planned lesson.

### 4.4 Storage design
- **Files on disk, metadata in SQL.** Databases are bad at storing gigabytes; they store *paths*
  (`videos/{id}/hls/master.m3u8`) relative to a media root. In production the root becomes **object
  storage** (S3 / Azure Blob) — only `LocalMediaStorage` / `LocalMediaFiles` would change.
- **Private vs public.** `uploads/` (originals) is never served; only `videos/` is exposed at `/media`.
- **Atomic writes.** Files are written as `*.partial` and renamed when complete, so a half-written
  upload never appears under its real name.
- **Keep the original.** It lets us re-encode later (new codec, new resolution) without asking the user.
- **Caching headers.** Segments never change → `Cache-Control: immutable, max-age=1 year`; playlists and
  thumbnails get 60 s. A **CDN** in front of `/media` would serve almost all video traffic from the edge.

### 4.5 Transcoding and the bitrate ladder
- **Ladder:** 1080p 5 Mbps · 720p 2.8 Mbps · 480p 1.4 Mbps · 360p 800 kbps · 240p 400 kbps · 144p 200 kbps.
- **No upscaling:** a 480p upload only gets 480/360/240/144 — upscaling adds bytes, not detail.
- **Aspect ratio & rotation:** the "p" number is the *short side*, so portrait phone videos become
  1080×1920, 720×1280… ffprobe's rotation flag is honoured.
- **Capped bitrate** (`-b:v`, `-maxrate`, `-bufsize`): each rendition really fits the bandwidth the
  master playlist advertises, so the player's choices are trustworthy.
- **Aligned keyframes** (`-force_key_frames expr:gte(t,n_forced*4)`, `-sc_threshold 0`): every rendition
  starts a new segment at exactly 0, 4, 8… seconds. **This is what makes mid-play switching seamless**:
  segment N of 720p and segment N of 1080p cover the same moment, so the player can splice them.

### 4.6 HLS and adaptive bitrate (ABR)
- **Media playlist** (`720p/index.m3u8`): the list of 4-second `.ts` segments for one quality.
- **Master playlist** (`master.m3u8`): the list of qualities with `BANDWIDTH` and `RESOLUTION`.
- **Auto (ABR):** hls.js measures how fast segments download and how full the buffer is, then picks
  the highest rendition that won't stall — moving up or down every few seconds.
  `capLevelToPlayerSize` avoids fetching 1080p for a small player.
- **Manual switch:** `hls.currentLevel = n` flushes the buffer and loads the new quality *from the
  current position* (instant). `hls.nextLevel` would switch at the next segment (smoother, slower).
- **Segment length trade-off:** shorter segments = faster switching and startup, but more files and
  requests; 2–6 s is the usual range (we use 4 s).
- **Safari** plays HLS natively, so hls.js isn't needed there.

### 4.7 Database design for videos
- **Tags in their own table** (`VideoTags(VideoId, Tag)` + index on `Tag`) — "all videos tagged
  *angular*" is an index seek, not a scan of comma-separated strings. Tags are normalised
  (lower-case, no `#`, de-duplicated) so `Angular`, `#angular` and `angular ` are one tag.
- **Likes:** primary key `(VideoId, UserId)` makes a double like impossible even under a race.
- **Indexes follow the queries:** `(Status, PublishedAtUtc)` for the public feed,
  `(Status, Category, PublishedAtUtc)` for category tabs, `OwnerId` for "Your videos".
- **N+1 avoided:** like counts for a page of 12 videos come from *one* grouped query, not 12.
- **Paging in SQL** (`OFFSET/FETCH`), and `ExecuteUpdate` for status changes (one UPDATE, no SELECT).
- **UTC everywhere:** `datetime2` has no time zone, so values are marked UTC when read
  (`ConfigureConventions`) — otherwise browsers treat them as local time.

### 4.8 Limits at every hop
An upload passes Kestrel (gateway) → YARP → Kestrel (Video). Each has its own body-size limit (30 MB by
default) and timeout, and *all* must allow 2 GB. Validation happens twice: in the browser (instant
feedback) and in the BLL (the browser can't be trusted).

**Self-check questions**
1. Why does the upload endpoint return before the video is processed? What would break if it didn't?
2. A worker crashes halfway through encoding. What happens to the job, and why is that safe?
3. Why must keyframes be aligned across renditions for quality switching to work?
4. Why store the original upload even after all renditions exist?
5. What is the dual-write problem in the upload flow, and how does the outbox pattern fix it?
6. Why is a corrupt file *not* sent to the dead-letter queue?
7. Why are tags a separate table instead of a comma-separated column?

### Known limits (to improve later)
- **Progress is polled** (every 2.5–4 s). Next step: **SignalR** pushes progress the moment the worker reports it.
- **One shared disk.** Worker and Video service both use `D:\YTubeMedia`; production uses object storage.
- **No resumable uploads.** A dropped connection restarts the upload; large-file platforms use chunked/resumable uploads (e.g. tus).
- **Very long videos** could exceed RabbitMQ's 30-minute delivery-ack timeout; real systems split work into per-rendition jobs.
- **Unlisted ≠ private.** Media of a processing video is reachable if someone guesses its 11-character id.

---

## Step 5 — Channels, follows and view counting

```
Auth ── user.registered ──► User service: create profile (@handle)        ◄─ GET /api/users/me (lazy fallback)
User ── user.profile.updated ──► Video service: rename the creator on all their videos

Channel page = User (profile, followers) + Video (videos, totals)      — composed in the browser
Following    = Feed → User "who do I follow?" → Video "their newest videos"   — fan-out on read
Views        = player (5 s of playback) → POST /views → Redis dedup → Redis buffer → SQL every 10 s
```

### 5.1 Which service owns what
- **User service:** profiles (`UserProfiles`, unique `Handle` index) and the follow graph (`Follows`, PK
  `(FollowerId, FolloweeId)` + index on `FolloweeId`). "Who follows whom" is user data, not video data.
- **Video service:** videos, likes, views and per-channel totals.
- The link between them is only the **user id** (JWT `sub`) — no shared tables, no cross-database joins.

### 5.2 Creating profiles: events + a lazy fallback
Auth publishes **`user.registered`** after sign-up and the User service creates the profile. But events can be
lost (broker down at that moment) and accounts created *before* this feature never had one. So `GET /me`
also creates the profile if it's missing. Both paths call the same **idempotent** `EnsureProfileAsync`; if they
race, the unique primary key lets only one insert win and the other simply reads it back.
Lesson: **don't make correctness depend on one message arriving** — combine events with a safe fallback.

### 5.3 Denormalisation and eventual consistency
Every video stores a copy of its creator's name (`ChannelName`), so the feed renders 12 cards without asking the
User service 12 times. The price: a rename has to be propagated. The User service publishes
**`user.profile.updated`**; the Video service updates the name on all that creator's videos with one
`UPDATE … WHERE OwnerId = @id`. For a moment old and new names coexist — the system is **eventually consistent**.
That's the standard trade-off: fast reads in exchange for a short window of stale data.

### 5.4 The Following feed: fan-out on read vs fan-out on write
- **Fan-out on read (what we do):** at read time, get the ids you follow, then query videos `WHERE OwnerId IN (…)`.
  Uploading costs nothing extra; every feed read does the work.
- **Fan-out on write:** when a creator uploads, push the video id into each follower's pre-built feed list
  (e.g. a Redis list per user). Reads become a single lookup, but a creator with 10 million followers means
  10 million writes per upload.
- Large platforms use a **hybrid**: fan-out on write for normal creators, fan-out on read for celebrities.

### 5.5 Counting views at scale
1. **What counts:** the player reports a view after **5 seconds of real playback** (seeking doesn't add time).
2. **Dedup:** `SET video:viewed:{video}:{viewer} NX EX 1800` — atomic "only if it doesn't exist yet", expiring in
   30 minutes. Signed-in viewers are identified by user id, guests by a hash of IP + browser. Refreshing the page
   doesn't inflate the count.
3. **Buffer:** a counted view is `HINCRBY video:views:pending {video} 1` in Redis — microseconds, no SQL lock.
4. **Flush (write-behind / batching):** every 10 s a background job atomically `RENAME`s the buffer, so new views
   go to a fresh one, and applies `UPDATE Videos SET Views = Views + n` per video.
   - A viral video gets **one** SQL update per 10 s instead of one per viewer → no **hot-row contention**.
   - Relative updates (`Views + n`) can't overwrite each other, unlike "read, add, write".
5. **Trade-offs:** counts in SQL lag up to 10 s (the page shows +1 immediately), and a crash can lose up to 10 s of
   views — fine for a view counter, never acceptable for payments.
6. **Real client IP:** the gateway adds `X-Forwarded-For`; the Video service trusts it only from known proxies
   (`UseForwardedHeaders`, loopback by default) — otherwise anyone could fake their IP.
7. **Resilience in action:** after the laptop slept, Redis calls timed out once; the flusher logged a warning and
   retried on the next tick. Nothing was lost because the buffer is only taken when Redis answers.

### 5.6 Follow and like are idempotent
`PUT /follow` twice = followed once (the primary key also blocks duplicates under a race); `DELETE` removes it.
The UI updates **optimistically** (count changes instantly) and rolls back if the server refuses.

**Self-check questions**
1. Why does `GET /me` create a profile when the `user.registered` event already does?
2. Why store the creator's name on every video, and what does that cost us?
3. When would you switch the Following feed from fan-out on read to fan-out on write?
4. Why buffer views in Redis instead of `UPDATE Videos SET Views = Views + 1` on every view?
5. How does `SET … NX EX` stop a page refresh from counting twice, even with two requests at the same instant?
6. Why must `X-Forwarded-For` only be trusted from known proxies?

### Known limits (to improve later)
- **N+1 on profiles:** follower/following counts are separate `COUNT` queries per profile; fine for small lists, a
  counter column (updated with the follow) is the scalable version.
- **Join date** for accounts that existed before profiles is the date the profile was created, not the sign-up date.
- **View rules are client-side** (5 s of playback); a determined script can still call the endpoint once per
  30 minutes per IP. Real platforms add bot detection on top.
- **Sample channels** (seeded videos) have no account, so they have no channel page and can't be followed.

---

## Step 6 — Comments, replies and @mentions

```
Browser ─GET  /api/comments/video/{id}?cursor=… ─► Gateway ─► Comment service ─► YTube_CommentDb
        ─POST /api/comments/video/{id} (JWT) ──► Gateway (rate limit 20/min) ─► Comment service
                                                        ├─ HTTP ─► Video service  "does it exist, who's the creator?" (cached in Redis)
                                                        ├─ HTTP ─► User service   "who am I?" + "which users are @a, @b?"
                                                        └─ publish comment.user-mentioned ─► RabbitMQ (notifications later)
User service ─ user.profile.updated ─► RabbitMQ ─► Comment service: rename the author on their comments
```

### 6.1 Why a separate Comment service?
Comments are the busiest *write* path on a video site: one video, thousands of people typing. Keeping them out of the
Video service means a comment flood can't slow down video metadata, the two can scale separately (more Comment
instances, a different database later), and the Video database stays small. The price is that a few facts now live in
another service, so the Comment service has to *ask* for them (6.3). Splitting by **workload**, not just by noun, is
the usual reason to cut a new microservice.

### 6.2 Data model: one table, one level of replies
`Comments(Id bigint identity, VideoId, ParentId NULL, AuthorId, AuthorName, AuthorHandle, Text, ReplyCount, CreatedAtUtc, EditedAtUtc)`
- A reply is just a comment with `ParentId`. Replies are **one level deep**: replying to a reply stores the
  top-level comment as the parent. Unlimited nesting (Reddit-style) needs recursive queries and is hard to read on a phone.
- **`ReplyCount` is stored**, not counted: a page of 20 comments would otherwise need 20 extra `COUNT(*)` queries.
  It's updated with a relative `ReplyCount = ReplyCount + 1` inside the same transaction as the insert, so two
  replies at the same moment can't overwrite each other (same idea as the view counter in step 5).
- **Author name/@handle are copied** onto each comment (denormalised) and refreshed by the `user.profile.updated`
  event, the same pattern as `ChannelName` on videos. Each service has its **own queue** for that event
  (`video.user-events`, `comment.user-events`), so both get a copy: that's pub/sub with a topic exchange.
- SQL Server won't cascade-delete a table onto itself, so deleting a comment deletes its replies explicitly in a transaction.
- With connection retries turned on (`EnableRetryOnFailure`), a transaction must run inside
  `CreateExecutionStrategy().ExecuteAsync(...)`, so a retry repeats the whole unit (insert + counter), never half of it.

### 6.3 Talking to other services (and degrading gracefully)
Posting needs to know three things: does the video exist (Video service), who is writing (User service), and which users the @handles
belong to (User service). These are **synchronous HTTP calls** with a 5 s timeout, and the two independent ones run **in parallel**.
- The **video's owner** is needed on every page of comments (Creator badge, delete rights), so it's cached in Redis for
  10 minutes (**cache-aside**). One HTTP call per video per 10 minutes instead of one per page view.
- If the User service is down while resolving mentions, the comment is still saved; the `@handles` just aren't
  links. If the Video service is down while *reading*, comments still show, without the Creator badge.
  That's **graceful degradation**: a dependency failing removes a feature, not the whole page.
- If the Video service is down while *posting*, we return 503. We can't verify the video, and refusing is safer
  than accepting comments on videos that may not exist.

### 6.4 @mentions: resolve once, store the id
Text like `@sam` is matched with a regex (`@` not preceded by a letter, so `me@mail.com` isn't a mention), the handles
are looked up in **one** batched call (`/api/users/by-handles?handle=a&handle=b`), and the result is stored in
`CommentMentions(CommentId, UserId, Handle)`. Storing the **user id** means the link still points at the right person
if the handle is later taken by someone else, and the index on `UserId` makes "comments that mention me" a cheap query
for the notifications page later.
Autocomplete is a **prefix search** (`Handle LIKE 'sa%'`), which can use the unique index on `Handle`; a
"contains" search (`'%sa%'`) would scan the whole table. The browser **debounces** it (waits 200 ms after typing stops)
and ignores answers to outdated queries.

### 6.5 Keyset (cursor) pagination
Offset paging (`OFFSET 400 ROWS FETCH NEXT 20`) makes the database walk past 400 rows, and if new comments arrive while
you scroll, the pages shift and you see duplicates. **Keyset paging** remembers the last id you saw:
`WHERE VideoId = @v AND ParentId IS NULL AND Id < @cursor ORDER BY Id DESC`. The index
`(VideoId, ParentId, Id)` jumps straight to the cursor, so page 1 and page 1000 cost the same, and new comments don't
shift anything. We ask for **21 rows** to know whether a next page exists without a `COUNT`. The total is only counted on
the first page. Trade-off: you can't jump to "page 37", which a comment list never needs.

### 6.6 Abuse protection and safe rendering
- The gateway rate-limits comment **writes** to 20 per minute per IP (reads only have the global limit).
- Comments are rendered from **text segments**, never `innerHTML`, so `<script>` in a comment is shown as text (no XSS).
  Links get `rel="noopener noreferrer nofollow"`; `nofollow` tells search engines not to reward spam links.
- Authorization lives in the BLL: only the author edits; the author **or the video's creator** deletes.
  Deleting is idempotent (deleting twice is still 200).

### 6.7 Events for later
Every new mention publishes `comment.user-mentioned`. Nobody listens yet, so RabbitMQ drops it. Publishing now means
the notifications step (SignalR) only adds a consumer; the Comment service won't change.

**Self-check questions**
1. Why store `ReplyCount` instead of counting replies, and why `+ 1` in SQL instead of reading, adding and saving?
2. What goes wrong with `OFFSET` paging on a busy comment section, and how does a cursor fix it?
3. Why store the mentioned user's **id**, not just the `@handle` text?
4. The User service is down. What still works when posting a comment, and what doesn't?
5. Why does `LIKE 'sa%'` use an index while `LIKE '%sa%'` doesn't?
6. Why do the Video and Comment services each need their own queue for `user.profile.updated`?

### Known limits (to improve later)
- **No comment likes or "Top comments" sort**: newest first only. Sorting by likes needs a score column and a
  different index (and keyset paging on two columns).
- **Mentions aren't notified yet**: the event is published but there's no consumer until the SignalR step.
- **Rate limit is per IP**, so people behind one network share it; per-user limits (Redis counters) would be fairer.
- **Video deletion** doesn't remove its comments yet (there's no delete-video feature); it would publish `video.deleted`
  for the Comment service to clean up.
- **Description @handles** are highlighted but not linked (only comments resolve mentions on save).

---

## Step 7 — Notifications and real-time push (SignalR)

```
Video   ── video.published ──┐
User    ── user.followed ────┤                      ┌─ save one row per recipient ─► YTube_NotificationDb
Comment ── comment.created ──┼─► RabbitMQ ─► Notification service ─┤
Comment ── comment.user-mentioned ┘   (queue          │             └─ push live ─► SignalR hub ─► Redis backplane
                                   notification.events) │                                              │
                     HTTP: "who follows this creator?" ─► User service              Gateway (WebSocket) ◄┘
                                                                                         │
Browser: on sign-in GET /api/notifications (history) + connect /hubs/notifications (live) ◄┘
```

| Event | Who gets notified |
|---|---|
| A followed creator's video finishes processing (`video.published`) | every follower ("X uploaded: …") + the creator ("Your video is live") |
| Someone follows you (`user.followed`) | you, once per follower |
| `@you` in a comment (`comment.user-mentioned`) | you |
| A reply in a thread you started (`comment.created`) | the thread's author |
| A comment on your video (`comment.created`) | the creator |

### 7.1 Event-driven: producers don't know about notifications
The Video, User and Comment services just **publish facts** ("a video was published", "X followed Y"). They don't call
the Notification service and don't know it exists. Adding notifications needed no `if (notify) …` in their business
logic, and if the Notification service is down they keep working: the events wait in its durable queue. This is the
main benefit of **pub/sub**: new consumers can be added without touching producers.

### 7.2 Fan-out on write (again) — and why here
A new video from a creator with 10 000 followers creates 10 000 notification rows. That's **fan-out on write**, the
opposite of the Following feed (step 5, fan-out on read). Why the difference?
- Notifications have **per-user state** (read/unread), so each person needs their own row anyway.
- Opening the bell must be instant: `WHERE UserId = @me ORDER BY Id DESC` on an index, not "find everyone I follow,
  then their videos, then which ones I've seen".
- The write cost is paid **in the background** by a consumer, never by the uploader's request.
Rows are inserted and pushed in chunks of 500 so a big channel doesn't build one giant transaction. Very large
creators are where real systems switch to a hybrid (fan out lazily, or only to recently active followers).

### 7.3 Idempotent consumer: the DedupKey
RabbitMQ guarantees **at-least-once** delivery: after a crash or a lost ack, the same event comes again. Each
notification gets a `DedupKey` describing its cause (`video:{id}`, `comment:{id}`, `follow:{followerId}`) and the table
has a **unique index on (UserId, DedupKey)**. A redelivered event finds its rows already there and inserts nothing; only
rows that were really inserted are pushed. (Tested by republishing a `video.published` by hand: no duplicate.)
The same key gives us anti-spam for free: unfollow + follow again produces no second "X followed you".

### 7.4 Store first, then push
Every notification is **saved before it's pushed**. The push is best-effort: if the user is offline, or the push fails,
nothing is lost — the browser loads `GET /api/notifications` when the app opens and again after every reconnect.
Real-time is an *optimisation* on top of a reliable store, never the only copy.

### 7.5 SignalR: how the live connection works
- **Transport**: SignalR negotiates (`POST /hubs/notifications/negotiate`) and then upgrades to a **WebSocket** — one
  long-lived connection the server can write to at any time. (Fallbacks: Server-Sent Events, long polling.)
  Before this step the watch page *polled* every 2.5 s; polling is simple but wastes requests when nothing changes.
- **Auth**: browsers can't put an `Authorization` header on a WebSocket, so the client sends the JWT as
  `?access_token=…`. The shared JWT setup accepts the query-string token **only for `/hubs/*` paths**. Tokens in URLs can
  end up in logs; our access tokens live 15 minutes, which limits the damage.
- **Who is who**: `Clients.User(id)` reaches every connection of that user (several tabs, phone + laptop). SignalR finds
  the user id through `IUserIdProvider`; ours reads the JWT's `sub` claim.
- **Through the gateway**: YARP proxies WebSocket upgrades like any other request; the gateway validates the token
  before the connection reaches the service (`hubs-route`, policy `default`).
- **Token expiry**: a WebSocket is authenticated once, at connect. The client's `accessTokenFactory` refreshes the token
  if it's about to expire, so every **reconnect** uses a fresh one.
- **Reconnects**: automatic reconnect retries after 0, 2, 10 and 30 s; after that we keep retrying every 15 s. After
  reconnecting the client **reloads the first page** to catch up on anything pushed while it was offline. (Tested:
  stopped the Notification service, posted a mention, restarted it: the event waited in RabbitMQ and the bell updated
  without a page reload.)

### 7.6 Scaling out: the Redis backplane
With two Notification instances behind a load balancer, Alice's WebSocket is on instance 1, but the event that concerns
her may be consumed by instance 2. Instance 2 can't reach her connection directly. The **Redis backplane**
(`AddStackExchangeRedis`) publishes every "send to user X" on Redis pub/sub; each instance delivers it to the
connections it holds. WebSockets are also **sticky**: once open, all traffic for that connection must stay on the
same instance (load balancers need sticky sessions for the negotiate step, or clients must skip negotiation).

### 7.7 Cheap unread counts: a filtered index
The badge asks "how many unread?" on every app load. `IX_Notifications_Unread` is a **filtered index**
(`WHERE IsRead = 0`): it only contains unread rows, so it stays tiny however much history piles up, and
`COUNT(*) WHERE UserId = @me AND IsRead = 0` reads just a few entries. Marking read always includes
`AND UserId = @me`, so nobody can mark someone else's notifications by guessing ids.

### 7.8 The dual-write problem, for real this time
When processing completes, the Video service updates SQL **and** publishes `video.published`. If the process dies
between the two, the video is live but nobody is notified. We reduce the risk (the publish happens inside the
consumer of `video.processing.completed`, so a failure nacks that message and it can be replayed — and DedupKeys make a
replay safe), but the clean fix is the **transactional outbox**: write the event into an `Outbox` table in the same SQL
transaction as the update, and let a background job publish rows from it. That's in the Resilience phase.

**Self-check questions**
1. Why does the Following feed fan out on read but notifications fan out on write?
2. RabbitMQ delivers `video.published` twice. Walk through why followers still get exactly one notification.
3. Why save the notification before pushing it, instead of pushing first?
4. How does the browser authenticate a WebSocket, and why is that only accepted on `/hubs/*`?
5. With three Notification instances, how does a push reach a user connected to a different instance?
6. Why does the client reload the first page after reconnecting?

### Known limits (to improve later)
- **No retention**: notifications are kept forever; production systems delete or archive after ~90 days.
- **No grouping**: 50 comments on your video = 50 notifications (YouTube groups them: "Sam and 49 others…").
- **No preferences**: you can't mute a channel or a notification type yet.
- **Processing progress still polls** on the watch page; it could move to SignalR (a `video.processing.progress` push
  to the uploader) the same way.
- **Long-lived connections ignore token expiry/sign-out on other devices** until they reconnect; a server-side
  "close connections for user X" on logout would fix it.
- **Follower ids come from a synchronous call** to the User service; a creator with millions of followers needs paging
  (or a follower read-model kept by events in the Notification service).
