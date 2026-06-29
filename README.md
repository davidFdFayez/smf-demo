# Saudi MuayThai Federation — Membership Registration

Full-stack reference implementation for the SMF member registration flow.

- **Backend**: .NET 8, Clean Architecture, CQRS with MediatR, FluentValidation, EF Core (SQL Server / InMemory fallback).
- **Frontend**: React 18 + TypeScript, Vite, Tailwind CSS, react-hook-form + Zod.

## Repository layout

```text
.
├── SMF.sln
├── src/
│   ├── SMF.Domain/              # Entities, enums — no external dependencies
│   ├── SMF.Application/         # CQRS commands/handlers, validators, abstractions
│   ├── SMF.Infrastructure/      # EF Core DbContext, repositories, migrations
│   └── SMF.Api/                 # ASP.NET Core host + minimal-API endpoints
└── web/                         # Vite + React UI (TypeScript)
```

## Architecture at a glance

```
HTTP request
   │
   ▼
[SMF.Api]            minimal-API endpoint maps JSON → RegisterMemberCommand
   │
   ▼
[SMF.Application]    MediatR pipeline: ValidationBehavior → RegisterMemberCommandHandler
   │                 (FluentValidation enforces "Athlete < 18 ⇒ GuardianConsent")
   ▼
[SMF.Infrastructure] MemberRepository allocates next SMF_ID sequence (rowversion + retry)
   │                 Member persisted via EF Core in the same SaveChanges transaction
   ▼
[SMF.Domain]         Member.Register(...) — entity invariants + Status = Pending
```

Key building blocks:

| Concern                         | Where it lives                                                                      |
| ------------------------------- | ----------------------------------------------------------------------------------- |
| Entity + invariants             | `src/SMF.Domain/Entities/Member.cs`                                                 |
| Command + result DTO            | `src/SMF.Application/Features/Members/Commands/RegisterMember/RegisterMemberCommand.cs` |
| Validation (incl. minor rule)   | `.../RegisterMemberCommandValidator.cs` + `Common/Behaviors/ValidationBehavior.cs`   |
| Handler (thin orchestrator)     | `.../RegisterMemberCommandHandler.cs`                                               |
| Repository contract             | `src/SMF.Application/Common/Interfaces/IMemberRepository.cs`                        |
| EF Core implementation          | `src/SMF.Infrastructure/Persistence/Repositories/MemberRepository.cs`               |
| DbContext + configurations      | `src/SMF.Infrastructure/Persistence/ApplicationDbContext.cs`                        |
| Migrations                      | `src/SMF.Infrastructure/Persistence/Migrations/`                                    |
| HTTP endpoint                   | `src/SMF.Api/Endpoints/MembersEndpoints.cs`                                         |
| ProblemDetails exception mapper | `src/SMF.Api/Middleware/ExceptionHandlingMiddleware.cs`                             |
| UI — Zod schema                 | `web/src/features/members/schema.ts`                                                |
| UI — dynamic form               | `web/src/features/members/RegisterMemberForm.tsx`                                   |
| UI — API service                | `web/src/services/membersApi.ts`                                                    |

## Running the API

Prereqs: .NET 8 SDK (or newer). A real SQL Server instance is **optional** — if no connection string is configured, the API falls back to an in-memory database so you can iterate end-to-end without any infrastructure.

```bash
# From repo root
dotnet build SMF.sln
dotnet run --project src/SMF.Api
```

Defaults:

- API: `http://localhost:5080`
- Swagger: `http://localhost:5080/swagger`
- CORS allows `http://localhost:5173` (the Vite dev server)

### Using SQL Server (optional)

1. Install the EF CLI once:

   ```bash
   dotnet tool install --global dotnet-ef --version 8.0.10
   ```

2. Set a connection string in `src/SMF.Api/appsettings.Development.json`:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=SMF;Trusted_Connection=True;TrustServerCertificate=True;"
     }
   }
   ```

3. Start the API. In `Development`, pending migrations are applied automatically on boot. To apply manually instead:

   ```bash
   dotnet ef database update \
     --project src/SMF.Infrastructure \
     --startup-project src/SMF.Api
   ```

4. To add a new migration after a schema change:

   ```bash
   dotnet ef migrations add <Name> \
     --project src/SMF.Infrastructure \
     --startup-project src/SMF.Api \
     --output-dir Persistence/Migrations
   ```

   The design-time factory (`ApplicationDbContextFactory`) uses a LocalDB connection by default. Override with:

   ```bash
   $env:SMF_MIGRATIONS_CONNECTION = "Server=...;Database=SMF;User Id=...;Password=...;TrustServerCertificate=True;"
   ```

## Running the Web UI

Prereqs: Node.js 18+.

```bash
cd web
npm install
npm run dev
```

Then open `http://localhost:5173`. The Vite dev server proxies `/api/*` to `http://localhost:5080`, so you don't need to configure `VITE_API_BASE_URL` in development.

Other scripts:

```bash
npm run typecheck   # tsc --noEmit
npm run build       # production bundle in dist/
npm run preview     # preview the production build
```

## The registration flow

The backend exposes a single command-style endpoint:

```
POST /api/members
Content-Type: application/json

{
  "fullName": "Khalid Al-Otaibi",
  "dateOfBirth": "2012-05-14",
  "role": "Athlete",
  "guardianConsent": true
}
```

### Business rule

Athletes whose `DateOfBirth` makes them under 18 **must** have `GuardianConsent === true`. This rule is enforced in three places, in depth-of-defense order:

1. **UI (`RegisterMemberForm.tsx`)** — renders the Guardian Consent checkbox only when `role === "Athlete"` && `isMinor(dateOfBirth)`. When the condition flips off, the value is reset to `false`.
2. **Zod (`schema.ts`)** — `superRefine` adds an issue at `guardianConsent` if the rule is violated; the form never submits when red.
3. **FluentValidation (`RegisterMemberCommandValidator.cs`)** — identical rule re-checked server-side; violation raises `ValidationException` which the ASP.NET middleware turns into a `400 application/problem+json`.

### SMF ID generation

Format: `#SMF{YYYY}-{sequence:00000}` (e.g. `#SMF2026-00001`).

- A `MemberSequences` table tracks `LastSequence` per year, guarded by a `rowversion` column.
- `MemberRepository.GetNextSequenceForYearAsync` bumps the counter with optimistic concurrency + a short retry loop, and commits the increment in the **same `SaveChangesAsync` transaction** as the `Member` insert, so allocation and insert stay atomic under concurrent writes.

### Response

Success → `201 Created`:

```json
{ "id": "c69…", "smF_ID": "#SMF2026-00001", "registrationStatus": "Pending" }
```

Validation failure → `400 Bad Request` with `ValidationProblemDetails`:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "GuardianConsent": ["Guardian consent is required for athletes under 18."]
  }
}
```

The UI's API service (`membersApi.ts`) camelCases these keys and calls `setError` on the matching form fields, so server-side errors render inline under the correct input.

## Secrets management

Dev secrets live in [.NET user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), **not** in `appsettings.Development.json`. `WebApplication.CreateBuilder` loads them automatically when `EnvironmentName == "Development"`.

First-time setup on a new machine:

```bash
cd src/SMF.Api
dotnet user-secrets init                                   # only if not already initialized
dotnet user-secrets set "Jwt:SigningKey"                 "<at-least-32-chars>"
dotnet user-secrets set "Payments:WebhookSigningSecret"  "<random-64-hex>"
dotnet user-secrets set "DigitalId:SigningSecret"        "<at-least-32-chars>"
```

List / remove:

```bash
dotnet user-secrets list
dotnet user-secrets remove "Jwt:SigningKey"
```

In staging / production these same keys are supplied via environment variables or the cloud provider's secret manager (Azure Key Vault, AWS Secrets Manager, etc.) — the keys must match **case-for-case** with the colon-delimited config paths above.

## Transactional outbox

`PaymentSuccessfulEvent` is not published synchronously inside the webhook request. Instead the handler stages it on the `OutboxMessages` table in the same SQL transaction that commits the `Payments` row change. An `OutboxHostedService` polls the table and dispatches each pending row through MediatR. Guarantees:

- **Atomicity** — if the process crashes after `SaveChanges`, the outbox row stays pending and the next poll picks it up. No event is lost.
- **Idempotency** — `Payment.MarkSucceeded`/`MarkFailed` are idempotent, and the handler only enqueues the event on the *first* state transition, so duplicate webhook deliveries do not produce duplicate outbox rows.
- **Retry** — failures increment `Attempts` (capped at `OutboxMessage.MaxAttempts = 10`) and persist `LastError`, stopping the poison row from spinning forever.

Tuning knobs (`appsettings.json` → `Outbox`):

| Key                 | Default       | Purpose                                            |
| ------------------- | ------------- | -------------------------------------------------- |
| `PollInterval`      | `00:00:01`    | How often the hosted service drains the outbox.    |
| `BatchSize`         | `50`          | Max rows processed per iteration.                  |
| `RunHostedService`  | `true`        | Set to `false` so tests / CLIs can drive it manually. |

**Scale-out caveat**: the current poller assumes a single instance. For horizontal scaling, add a lease column or switch the read query to `SELECT … WITH (UPDLOCK, READPAST)` on SQL Server so multiple pollers don't double-dispatch.

## Offline Digital Accreditation (QR)

Athletes present a QR at the venue gate. Design goals, in order:

1. **Works offline.** Hive-backed cache on the device; QR renders on the very first frame, no spinner, no network.
2. **Tamper-resistant.** The QR does *not* encode the bare `SMF_ID` — it encodes a server-signed HMAC-SHA256 token (`base64url(payload) . base64url(sig)`) issued by `POST /api/members/{id}/digital-id`. Editing the on-disk Hive JSON doesn't help an attacker: the signed token is re-verified at the gate.
3. **Revocation-aware.** The gate calls `POST /api/admissions/verify`, which verifies the signature and re-reads the member's live `RegistrationStatus`. Post-issue suspensions deny admission even inside the token's validity window.
4. **Forwarding-resistant (bounded).** Tokens carry `iat` / `exp`. Default `Lifetime = 24h` — configurable via `DigitalId:Lifetime`. Screenshotting a friend's QR is a 24-hour problem, not a forever problem.
5. **Bilingual.** The Flutter UI ships EN / AR with RTL layout flip.
6. **Auto-recovers.** The mobile app silently re-issues the token on the offline→online edge whenever the cached one is inside the "expires soon" band (last 20% of lifetime), and also on app resume.

### Admission outcomes the gate may receive

| Outcome                       | HTTP | Meaning                                                              |
| ----------------------------- | ---- | -------------------------------------------------------------------- |
| `Admitted`                    | 200  | Signature valid, in window, current status is Approved / Active.     |
| `TokenMalformed`              | 200  | Cannot parse `payload.sig`.                                          |
| `SignatureMismatch`           | 200  | Signature recomputation differs — forgery or wrong environment.      |
| `NotYetValid` / `Expired`     | 200  | Outside the `iat`/`exp` window (clock skew = `DigitalId:ClockSkew`). |
| `MemberUnknown`               | 200  | Token references a member id that no longer exists.                  |
| `MemberNotInGoodStanding`     | 200  | Live status is `Pending` (or anything that isn't Approved/Active).   |

All denials return **HTTP 200** on purpose. Operators watching dashboards then clearly distinguish "scanner online but token rejected" (200 + denial) from "scanner can't reach the API" (timeout / 5xx).

### Setup

1. `dotnet user-secrets set "DigitalId:SigningSecret" "<at-least-32-chars>"` in `src/SMF.Api`.
2. In staging / production, supply the same key via environment variable `DigitalId__SigningSecret` or the secret manager.
3. Optional tuning:
   ```jsonc
   {
     "DigitalId": {
       "Lifetime": "1.00:00:00",   // 24h — longer for multi-day events
       "ClockSkew": "00:02:00"     // tolerance on iat/exp
     }
   }
   ```
4. Rotate the secret by deploying the new value with a brief overlap period — all outstanding tokens become invalid on rotation (clients transparently re-issue on next refresh).

### Known limitations / next steps

- The issuance endpoint is currently anonymous. In production `POST /api/members/{id}/digital-id` must be gated behind real athlete auth (OIDC, IdP) so an attacker with a leaked member-id GUID can't mint their own token. This is an auth-layer concern, not a Digital ID concern — the signing / verification pipeline stays identical.
- No device binding: two devices signed into the same member share the same token. Acceptable for MVP; revoke on logout if higher assurance is needed.
- Screenshot prevention (`FLAG_SECURE` on Android, screen-recorder indicator on iOS) is intentionally *not* wired up — add `flutter_windowmanager` (Android) and a method channel for iOS when productionising.

## Suggested next steps

- Approve / reject workflow (`Member.Approve()` already exists on the entity) via an `ApproveMemberCommand` + `POST /api/members/{id}/approve`.
- Query side: `GetMemberByIdQuery`, list with filtering (`status`, `role`), pagination.
- Integration tests with `WebApplicationFactory` + the InMemory provider for the handler + endpoint.
- Authentication (e.g. JWT) + role-based authorization for the approve workflow.
