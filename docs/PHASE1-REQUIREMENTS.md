# SMF Platform — Phase 1 Detailed Requirements
**Theme:** Security Hardening + Member Journey Unification  
**Target window:** 0–90 days from kick-off  
**Owner:** Engineering + Product  
**Status:** Draft — awaiting leadership sign-off (see `PRIORITIES-BRIEF.md`)

---

## Scope Summary

Phase 1 delivers three streams in parallel:

| Stream | Effort estimate | Risk if skipped |
|--------|----------------|-----------------|
| **1A** — RBAC & endpoint protection | High (backend-heavy) | Critical |
| **1B** — Membership journey fixes | Medium (full-stack) | High |
| **1C** — Admin dashboard KPI expansion | Low (backend + UI) | Medium |

---

## Stream 1A — Role-Based Access Control & Endpoint Security

### Background
All current API endpoints (admin approvals, certificate issuance, campaign dispatch, store fulfillment, etc.) are publicly accessible to any HTTP client. `MatchScoringHub` is the only currently protected surface. The dev-token endpoint (`POST /api/auth/dev-token`) must be disabled in production.

### Functional Requirements

#### FR-1A-01 · Authentication Middleware
- All API requests to admin-grade routes must carry a valid JWT in the `Authorization: Bearer <token>` header.
- Unauthenticated requests receive `401 Unauthorized` with a `WWW-Authenticate` challenge.
- The existing `AuthEndpoints.cs` dev-token path must be disabled when `ASPNETCORE_ENVIRONMENT != "Development"`.

**Affected files:** `src/SMF.Api/Program.cs`, `src/SMF.Api/Endpoints/AuthEndpoints.cs`

#### FR-1A-02 · Admin Role Definition
Three roles are required for Phase 1:

| Role | Permitted actions |
|------|-----------------|
| `smf.reviewer` | Approve/reject members and clubs; view member detail; record medical clearance |
| `smf.finance` | View and export payments; fulfill/cancel store orders; view invoices |
| `smf.content` | Create/edit events, news, e-learning courses; manage governance documents |
| `smf.admin` | All of the above plus issue certificates, send campaigns, manage tenants, view analytics |

**Note:** All four roles can read the admin dashboard stats.

#### FR-1A-03 · Endpoint Authorization Map
Apply `RequireAuthorization(policy)` to the following endpoint groups:

| Endpoint group | File | Required role(s) |
|---------------|------|-----------------|
| `POST /api/members/{id}/approve` | `MembersEndpoints.cs` | `smf.reviewer` or `smf.admin` |
| `POST /api/members/{id}/digital-id` | `MembersEndpoints.cs` | `smf.admin` |
| `POST /api/members/{id}/medical-clearance` | `MembersMedicalEndpoints.cs` | `smf.reviewer` or `smf.admin` |
| `POST /api/clubs/{id}/approve` | `ClubsEndpoints.cs` | `smf.reviewer` or `smf.admin` |
| `GET /api/admin/*` | `AdminEndpoints.cs` | Any authenticated admin role |
| `GET /api/export/*` | `ExportEndpoints.cs` | `smf.admin` or `smf.finance` |
| `/api/admin/store/*` (mutating) | `StoreEndpoints.cs` | `smf.finance` |
| `POST /api/notifications/*` | `NotificationsEndpoints.cs` | `smf.admin` |
| All broadcast/campaign mutations | `CommunicationEndpoints.cs` | `smf.admin` |
| `POST /api/certificates/*` | `CertificatesEndpoints.cs` | `smf.admin` |
| `GET /api/safeguarding/*` (list/get) | `SafeguardingEndpoints.cs` | `smf.reviewer` or `smf.admin` |
| Compliance admin mutations | `ComplianceEndpoints.cs` | `smf.content` or `smf.admin` |

Public endpoints that must remain unauthenticated: `POST /api/members` (register), `GET /api/events` (list), `POST /api/payments` (initiate), `GET /api/certificates/verify/{code}`, `POST /api/safeguarding` (anonymous report submit).

#### FR-1A-04 · Member Status Guard on `Approve`
The domain `Member.Approve()` method must be guarded at the application layer to reject calls when `RegistrationStatus == Active`. The handler should return a `400` with a clear error message to prevent regression.

**Affected file:** `src/SMF.Application/Features/Members/Commands/ApproveMember/ApproveMemberCommandHandler.cs`

#### FR-1A-05 · Digital ID Issuance Guard
`POST /api/members/{id}/digital-id` must only issue tokens for members with `RegistrationStatus == Active`. Pending or Approved members should receive `422` with `"Member must be Active to receive a digital ID."`.

**Affected file:** `src/SMF.Application/Features/Members/Commands/IssueDigitalId/IssueDigitalIdCommandHandler.cs`

#### FR-1A-06 · Admin HTTP Client Auth
The admin SPA `httpClient.ts` must attach the JWT in the `Authorization` header for all requests. A React context (`AuthContext`) must store the token after login and clear it on logout.

**Affected file:** `admin/src/services/httpClient.ts`

### Non-Functional Requirements
- Token expiry: access token 15 min, refresh token 7 days.
- All 401/403 responses must be logged to the application log with caller IP and requested path.
- The dev-token endpoint must be covered by an integration test asserting it returns `404` in production environment.

### Acceptance Criteria
- [ ] Calling `POST /api/members/{id}/approve` without a token returns `401`.
- [ ] Calling the same endpoint with a `smf.finance` token returns `403`.
- [ ] Calling with a `smf.reviewer` token on a Pending member returns `200`.
- [ ] Calling with a `smf.reviewer` token on an Active member returns `400` with descriptive message.
- [ ] `POST /api/members/{id}/digital-id` on an Approved (not yet Active) member returns `422`.
- [ ] Dev-token endpoint returns `404` when `ASPNETCORE_ENVIRONMENT=Production`.

---

## Stream 1B — Membership Journey Updates

### Background
An Approved member who visits the site has no obvious path to pay their fee (checkout is reachable only by direct URL). There is no "my membership" self-service page. E-learning enrollment requires the member to paste their own GUID — a UX anti-pattern.

### Functional Requirements

#### FR-1B-01 · "Pay Membership" CTA in Public Navigation
Add a persistent "Pay Membership Fee" link in the public navigation (`PublicLayout.tsx`) that routes to `/checkout`. The link should be visually prominent (outlined button, not a plain nav link).

**Affected file:** `web/src/layouts/PublicLayout.tsx`

#### FR-1B-02 · Post-Registration Deep Link to Checkout
After a successful registration, the success screen must display a note: *"Once your application is approved, you will receive a link to complete your membership payment."* and show the member's SMF ID prominently for reference.

**Affected file:** `web/src/pages/RegisterPage.tsx` (or the success state in `RegisterMemberForm.tsx`)

#### FR-1B-03 · "My Membership" Status Page
Create a new public page at `/my-membership` that allows a member to look up their status by:
1. Entering their SMF ID (e.g. `#SMF2026-00001`), or
2. Following a deep link `?id=<guid>` (sent in the approval email).

The page must display:
- Current status (`Pending` / `Approved` / `Active`) with a clear visual state
- If `Approved`: a prominent "Complete Payment" button linking to `/checkout?memberId=<id>`
- If `Active`: digital ID status, certificate count, enrolled courses
- If `Pending`: expected review time and contact link

**New file:** `web/src/pages/MyMembershipPage.tsx`  
**Route entry:** Add `/my-membership` to `web/src/App.tsx`  
**API:** Reuse `GET /api/members/{id}` (already exists)

#### FR-1B-04 · Checkout Member Picker → Authenticated Lookup
Replace the current "pick from approved members list" picker in `CheckoutPage.tsx` with a simple GUID or SMF-ID text input. The user enters their ID; the UI fetches `GET /api/members/{id}` and displays name + status confirmation before proceeding.

This prevents a user from accidentally (or deliberately) paying on behalf of a different member.

**Affected file:** `web/src/features/payments/CheckoutPage.tsx`

#### FR-1B-05 · E-Learning Member ID Persistence Improvement
When a member looks up their status on `/my-membership` (FR-1B-03), store their member GUID in `localStorage` under the key `smf.memberId`. The e-learning course pages already read this key — this change means members who use the status page first will not need to paste their GUID again.

**Affected file:** `web/src/pages/MyMembershipPage.tsx`

### Acceptance Criteria
- [ ] "Pay Membership Fee" button is visible in the public navigation on all screen sizes.
- [ ] Visiting `/my-membership?id=<valid-guid>` with an Approved member shows the "Complete Payment" CTA.
- [ ] Visiting `/my-membership?id=<valid-guid>` with an Active member shows digital ID and certificates.
- [ ] After using `/my-membership`, navigating to a course detail page does not show the GUID input prompt.
- [ ] Checkout page shows a confirmation of the member's name before enabling the "Pay" button.

---

## Stream 1C — Admin Dashboard KPI Expansion

### Background
The current dashboard shows six KPIs. The KPI dictionary (`KPI-DICTIONARY.md`) identifies six additional metrics needed for operational management.

### Functional Requirements

#### FR-1C-01 · Extend `AdminStats` with Missing KPIs
Add the following fields to `AdminStats` record and `GetAdminStatsQueryHandler`:

```csharp
// New fields to add to AdminStats record
long StoreRevenueThisMonthMinor,
int PendingMembersOlderThan3DaysCount,
double PaymentSuccessRatePct,
double RegistrationToActiveConversionPct  // 30-day cohort
```

**Affected files:**
- `src/SMF.Application/Features/Admin/AdminFeatures.cs`
- `src/SMF.Api/Endpoints/AdminEndpoints.cs`

#### FR-1C-02 · Reject/Suspend Lifecycle Actions in Admin UI
Add action buttons to the member detail page in admin:
- **Reject** (Pending only): moves member to a `Rejected` status (see domain change below); requires a mandatory reason field.
- **Suspend** (Active only): moves member to `Suspended`; requires a mandatory reason.
- **Reinstate** (Suspended only): returns member to `Active`.

**Domain change required:** Add `Rejected = 3`, `Suspended = 4` to `RegistrationStatus` enum with corresponding domain methods `Reject(string reason)`, `Suspend(string reason)`, `Reinstate()`.

**Affected files:**
- `src/SMF.Domain/Enums/RegistrationStatus.cs`
- `src/SMF.Domain/Entities/Member.cs`
- New handlers: `RejectMemberCommandHandler.cs`, `SuspendMemberCommandHandler.cs`, `ReinstateMemberCommandHandler.cs`
- New endpoints in `MembersEndpoints.cs`: `POST /api/members/{id}/reject`, `/suspend`, `/reinstate`
- `admin/src/pages/MemberDetailPage.tsx`

#### FR-1C-03 · Dashboard Funnel Visualization
Add a vertical funnel chart to the admin dashboard below the existing KPI cards, showing:
- Registered → Approved → Active conversion numbers and drop-off %
- Color-coded steps (green for healthy, amber for attention-needed)

**Affected file:** `admin/src/pages/DashboardPage.tsx`

### Acceptance Criteria
- [ ] `GET /api/admin/stats` returns `storeRevenueThisMonthMinor`, `pendingMembersOlderThan3DaysCount`, `paymentSuccessRatePct`, and `registrationToActiveConversionPct`.
- [ ] Dashboard renders new KPI cards without layout regression.
- [ ] Funnel visualization reflects live data.
- [ ] Rejecting a Pending member with a reason changes their status to `Rejected` and the reason is stored.
- [ ] Suspending an Active member requires a reason and changes status to `Suspended`.
- [ ] Reinstating a Suspended member returns them to `Active`.
- [ ] `Rejected` and `Suspended` members cannot be issued digital IDs.

---

## Dependencies and Pre-conditions

| Dependency | Owner | Needed by |
|-----------|-------|-----------|
| Decision on auth provider (see PRIORITIES-BRIEF.md Decision 1) | Leadership | FR-1A-01 |
| Admin role list confirmed (Decision 2) | Leadership | FR-1A-02 |
| Suspension scope decision (Decision 3) | Leadership | FR-1C-02 |
| DB migration capability set up | Engineering | All 1A and 1C work |

## Out of Scope for Phase 1
- Member login / self-service account (Phase 2)
- Automated renewal reminders (Phase 2)
- Competition eligibility engine (Phase 2)
- Sponsor dashboards (Phase 3)
