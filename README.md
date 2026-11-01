# YTube — a YouTube-like microservices project

> 📘 **Study guide:** [docs/SYSTEM-DESIGN.md](docs/SYSTEM-DESIGN.md) explains every system design concept, step by step.

A learning project for **system design**: each concept is added when the app actually needs it.
Stack: **.NET 10**, **Angular 21** (standalone + signals + Tailwind v4), **SQL Server**, **Redis** (Docker).
Services run directly on the machine for now; full Docker support comes in a later phase.

## Architecture

```
Angular (4200) ──► API Gateway (5000, YARP) ──► Auth  (5001) ── SQL Server: YTube_AuthDb, Redis
                    • JWT check at the edge       ├► User  (5002)
                    • rate limiting               ├► Video (5003)
                    • CORS, correlation ids       └► Feed  (5004)
                                                  Processing worker (background jobs, no HTTP)
```

| Path via gateway              | Service | Login required |
|-------------------------------|---------|----------------|
| `/api/auth/**`                | Auth    | no (rate limited: 10/min per IP) |
| `/api/users/**`               | User    | yes |
| `GET /api/videos/**`          | Video   | no — anyone can browse/watch |
| `/api/videos/liked`, any non-GET on `/api/videos/**` (like, unlike, later upload) | Video | yes |
| `GET /api/feed/**`            | Feed    | no — home feed is public |
| `/api/feed/subscriptions`     | Feed    | yes |

## Public vs. logged-in (how access is handled)

Browsing is public like YouTube; acting as a user needs a login. Four layers enforce it:

1. **Gateway (edge)** — each YARP route has an `AuthorizationPolicy`: `anonymous` for public reads,
   `default` (valid JWT) for everything else. Routes can match on HTTP method, so `GET /api/videos/x`
   is public while `PUT /api/videos/x/like` is not. Bad requests are rejected before reaching a service.
2. **Service (defense in depth)** — controllers repeat the rule with `[Authorize]`, so a service is safe
   even if called directly. Public endpoints use **optional authentication**: if a valid token is sent
   they personalise the answer (`likedByMe`), otherwise they answer as for a guest.
3. **Service-to-service** — Feed calls Video over HTTP and forwards the user's token and correlation id
   (`HeaderPropagationHandler`), so Video knows who is asking.
4. **Frontend** — no guards on browsing pages. Login-only *actions* (like, subscribe, upload) go through
   `SignInPromptService`, which shows a "Sign in to…" dialog for guests. Login-only *pages* (Liked,
   History, Subscriptions) show a sign-in empty state. After signing in, `?returnUrl=` brings the user
   back to where they were (only in-app paths are allowed, to prevent open redirects).

## Folder layout

```
backend/
  YTube.slnx
  BuildingBlocks/YTube.Common     shared: ApiResponse, Result, PagedResult, JWT setup, Redis cache,
                                  correlation-id + header propagation, global exception handler
  Gateway/YTube.Gateway           YARP reverse proxy (routes in appsettings.json) — no business logic
  Services/<Name>/                Auth, User, Video, Feed — each split into 4 class-library layers:
    <Name>.Api                    controllers + Program.cs only (no logic); wires BLL + DAL via DI
    <Name>.BLL                    all business logic (Services/) + the interfaces it needs (Interfaces/)
    <Name>.DAL                    data access only, in folders:
      Context/                    DbContext (Video: in-memory context until Phase 2)
      Models/                     table models, T-prefixed (TUser, TRefreshToken, TVideo…)
      Mappings/                   table model ⇄ entity conversion
      Repositories/               implement the BLL's repository interfaces
      Migrations/                 EF Core migrations (Feed has Clients/ instead: its data comes over HTTP)
    <Name>.Entities               business entities + request/response DTOs the BLL works with; references nothing
  Services/Processing/Processing.Worker   background worker (no layers yet)
frontend/ytube-web                Angular 21 app
scripts/run-backend.ps1           build + start every service in its own window
docker-compose.yml                Redis
```

### How the layers reference each other

```
<Name>.Api ──► <Name>.BLL ◄── <Name>.DAL
     │              │              │
     └──────────────┴──► <Name>.Entities ◄┘
```

Classic N-tier with one tweak (dependency inversion): the **BLL declares the interfaces**
(`IUserRepository`, `IVideoRepository`, …) and the **DAL implements them**, so the DAL references
the BLL — never the other way round. The BLL has no EF Core / SQL / HTTP code at all, so it can be
unit-tested with fakes, and swapping storage (e.g. Video's in-memory repository → SQL) only touches
the DAL. Each layer exposes one DI extension (`AddAuthBll`, `AddAuthDal`, …) called from `Program.cs`.

## Running locally

Prerequisites: .NET 10 SDK, Node 24, Angular CLI 21, SQL Server Express (`localhost\SQLEXPRESS04`), Docker Desktop.

```powershell
docker compose up -d                 # 1. Redis
.\scripts\run-backend.ps1            # 2. builds, then starts gateway + all services
cd frontend\ytube-web; npm start     # 3. http://localhost:4200
```

- The Auth database is created/migrated automatically on startup (Development only).
- **No SMTP configured → OTP emails are printed in the Auth service console** (look for `[DEV EMAIL]`).
  To send real email, fill the `Smtp` section in `Auth.Api/appsettings.Development.json`
  (for Gmail use an App Password; don't commit it).
- API explorer for each service: `http://localhost:500X/scalar`. Health: `http://localhost:500X/health`.

New migration after changing entities (example: Auth; same pattern for User):

```powershell
cd backend
dotnet ef migrations add <Name> --project Services/Auth/Auth.DAL --startup-project Services/Auth/Auth.Api --output-dir Migrations
```

## Auth flow

1. `POST /api/auth/signup` → pending sign-up stored in **Redis** (5-min TTL), 6-digit OTP emailed.
2. `POST /api/auth/verify-otp` → OTP checked with `BCrypt.Verify` (max 5 tries), user saved to SQL, tokens returned.
3. `POST /api/auth/login` → 15-min **access token** in the response body (kept in memory only) +
   7-day **refresh token** in an `HttpOnly; SameSite=Strict; Path=/api/auth` cookie (only its hash is stored in SQL).
4. `POST /api/auth/refresh` (cookie) → **rotation**: old refresh token revoked, new pair issued. Reusing a revoked
   token revokes all of that user's sessions (theft detection), except within a 30 s grace window for concurrent tabs.
5. Angular: silent refresh on startup, proactive refresh 60 s before expiry, 401 → refresh + retry,
   and sign-in/out synced across tabs with `BroadcastChannel`.
6. Theme preference: `GET/PUT /api/users/me/preferences` (User service → `YTube_UserDb`, cached in Redis).

## System design concepts — roadmap

| Phase | Concepts | Status |
|------|----------|--------|
| 1. Foundation | API gateway, service decomposition, database per service, stateless JWT auth, refresh-token rotation, rate limiting, caching with TTL, health checks, correlation ids, centralized error handling | ✅ done |
| 2. Core features | Video upload & metadata, blob storage, User profiles/channels, Feed, pagination, sync service-to-service calls | ⏳ next |
| 3. Async & events | Message queue (RabbitMQ), pub/sub, outbox pattern, idempotent consumers, background processing | |
| 4. Scale | Cache-aside & invalidation, CQRS read models, read replicas, sharding/partitioning, CDN concepts | |
| 5. Resilience | Retries, timeouts, circuit breaker (Polly), bulkheads, graceful degradation | |
| 6. Observability | Structured logging, OpenTelemetry tracing, metrics, dashboards | |
| 7. Containers | Dockerfiles, docker-compose for everything, (maybe) Kubernetes | |
| 8. ML | Recommendations, trending, tagging, moderation | |
