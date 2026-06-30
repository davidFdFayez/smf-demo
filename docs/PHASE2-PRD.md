# SMF Platform — Phase 2 Product Requirements Document
**Theme:** Member Account Portal · Renewal Engine · Communication Segmentation  
**Target window:** 90–180 days from kick-off (after Phase 1 completes)  
**Owner:** Product · Engineering · Marketing  
**Status:** Draft — dependent on Phase 1 RBAC delivery

---

## Executive Summary

Phase 1 secured the platform and fixed the core member journey fragmentation. Phase 2 converts the platform from a **registration system** into a **member relationship platform**: members have a logged-in identity, the federation can proactively manage renewals, and marketing can send targeted, measurable communications to specific segments instead of broadcast blasts.

---

## Feature 1 — Member Account Portal (Login + Profile)

### Problem Statement
Members today have no persistent authenticated session. To access any member-specific feature (checkout, e-learning, digital ID download) they must re-enter their GUID or SMF ID each time. This creates friction, increases support tickets, and prevents the federation from building a personalized member experience.

### Goals
- Give every registered member a secure login (email + password or national-ID SSO)
- Aggregate all member-related data into one authenticated "My Account" dashboard
- Eliminate GUID-copy-paste patterns across checkout, e-learning, and digital ID flows

### User Stories

| ID | As a… | I want to… | So that… |
|----|-------|-----------|----------|
| UP-01 | Registered member | Log in with my email and password | I can access my account without re-entering my SMF ID every time |
| UP-02 | Registered member | See my current membership status and payment history | I know if any action is needed from me |
| UP-03 | Active member | Download my digital ID from my account | I do not have to contact the federation |
| UP-04 | Active member | See all my certificates and download them | I can share them with employers or clubs |
| UP-05 | Active member | See my enrolled courses and continue where I left off | I do not need to paste my member GUID into the course page |
| UP-06 | Active member | Update my phone number and emergency contact | My profile stays current |
| UP-07 | Minor athlete | Have my guardian manage consent through the same portal | The email link flow remains, but parent can also log in to review/approve |

### Functional Requirements

#### Member Login & Registration Integration
- New route: `POST /api/auth/login` — accepts email + password; returns access token + refresh token.
- New route: `POST /api/auth/refresh` — rotates tokens.
- New route: `POST /api/auth/logout` — invalidates refresh token server-side.
- Registration (`POST /api/members`) creates the member record and a corresponding `MemberCredential` row (hashed password). An email with a verify-your-email link is sent.
- First login is blocked until email is verified.

**New files needed:**
- `src/SMF.Domain/Entities/MemberCredential.cs`
- `src/SMF.Application/Features/Auth/Commands/LoginCommand.cs`
- `src/SMF.Application/Features/Auth/Commands/RefreshTokenCommand.cs`

#### My Account Dashboard (Web)
- New route `/account` in the public web app — requires authenticated session.
- Sections: Status card, Payment history, Certificates, Enrolled courses, Profile edit.
- Unauthenticated visitors are redirected to `/login?return=/account`.

**New files needed:**
- `web/src/pages/AccountPage.tsx`
- `web/src/pages/LoginPage.tsx`
- `web/src/contexts/AuthContext.tsx`
- `web/src/hooks/useAuth.ts`

#### Token Propagation
- `web/src/services/httpClient.ts` must attach the stored access token to all authenticated requests.
- On `401` response, attempt token refresh; on second failure, redirect to `/login`.

### Non-Functional Requirements
- Passwords hashed with bcrypt (cost factor ≥ 12).
- Access token TTL: 15 min. Refresh token TTL: 30 days.
- Login rate-limited to 5 attempts per 10 minutes per IP.
- Forgotten password flow: email with a time-limited reset link (TTL: 1 hour).

### Out of Scope for UP
- Social login (Google, Apple) — Phase 3 consideration
- Two-factor authentication — Phase 3
- National ID / NAFATH SSO — defer to Phase 3 unless leadership confirms timeline

### Acceptance Criteria
- [ ] A member who registered can log in with email and password after email verification.
- [ ] `/account` displays correct status, payment history, certificates, and enrolled courses.
- [ ] Navigating to a course from `/account` does not show the GUID prompt.
- [ ] Downloading the digital ID from `/account` works for Active members.
- [ ] Refreshing the page on `/account` with a valid refresh token does not log the member out.
- [ ] 6 failed login attempts in 10 minutes result in a rate-limit response.

---

## Feature 2 — Renewal & Retention Engine

### Problem Statement
Membership fees are annual. Currently there is no automated mechanism to notify members before expiry, offer a grace period, or trigger a re-activation path. Without this, Active members silently lapse and are lost.

### Goals
- Retain at least 70% of Active members into a second year
- Reduce admin effort for manual renewal follow-up
- Provide a one-click renewal path accessible from the member portal

### User Stories

| ID | As a… | I want to… | So that… |
|----|-------|-----------|----------|
| RR-01 | Active member | Receive a reminder 30 days before my membership expires | I can renew before I lose benefits |
| RR-02 | Active member | Renew my membership in one click from my account | I do not have to re-navigate the registration flow |
| RR-03 | Lapsed member | Have a 14-day grace period after expiry | I can renew without losing my SMF ID and history |
| RR-04 | Admin | See a list of members expiring in the next 30 days | I can proactively reach out to VIP members |
| RR-05 | Admin | Trigger manual renewal campaigns for a cohort | I can address seasonal drop-off |

### Functional Requirements

#### Membership Expiry Tracking
- Add `MembershipExpiresAtUtc` column to the `Members` table, set to `+365 days` from the date `ActivateAfterPayment()` is called.
- Add `RenewalGracePeriodExpiresAtUtc` = `MembershipExpiresAtUtc + 14 days`.
- Add domain method `Member.MarkExpired()` that sets `RegistrationStatus = Expired` (new status — add to enum).
- Add domain method `Member.Renew()` that resets the expiry date and returns status to `Active`.

**Affected files:**
- `src/SMF.Domain/Entities/Member.cs`
- `src/SMF.Domain/Enums/RegistrationStatus.cs` (add `Expired = 5`, `GracePeriod = 6`)

#### Renewal Background Service
- A hosted service (`RenewalBackgroundService`) runs daily at 08:00 AST.
- At `ExpiresAt - 30 days`: send reminder notification (push + email).
- At `ExpiresAt - 7 days`: send final reminder.
- At `ExpiresAt`: transition to `GracePeriod`; send "your membership has expired" notification.
- At `RenewalGracePeriodExpiresAtUtc`: transition to `Expired`.

**New file:** `src/SMF.Infrastructure/BackgroundServices/RenewalBackgroundService.cs`

#### Renewal Payment Flow
- New endpoint `POST /api/members/{id}/renew` — creates a renewal payment via existing `POST /api/payments` pattern with `Purpose = RenewalFee`.
- On webhook callback success: call `Member.Renew()`, reset expiry dates.
- The member portal `/account` shows a "Renew Membership" button when status is `GracePeriod` or when expiry is within 30 days.

#### Admin Expiry View
- New admin page `/members/expiring` listing members whose `MembershipExpiresAtUtc` is within 30 days, sortable by expiry date.
- Filter toggles: All / GracePeriod / Expired.

**New file:** `admin/src/pages/MembersExpiringPage.tsx`

### Acceptance Criteria
- [ ] An Active member with expiry date set receives an email 30 days before expiry.
- [ ] An Active member with expiry date set receives a push notification 7 days before expiry.
- [ ] On expiry date, member transitions to `GracePeriod`; digital ID gate returns `200` (still valid during grace period).
- [ ] 14 days after expiry, member transitions to `Expired`; digital ID gate returns `403`.
- [ ] Member in `GracePeriod` can complete renewal payment and return to `Active` with updated expiry.
- [ ] Admin expiry list correctly shows members due within 30 days.

---

## Feature 3 — Communication Segmentation & Campaign Orchestration

### Problem Statement
The current notification system (`POST /api/notifications/send` and `/campaign`) has no access control and no audience segmentation — messages are either sent to a single member or broadcast to all. Marketing cannot target by role, status, club, event registration, or course enrollment.

### Goals
- Enable the marketing team to create targeted campaigns without engineering involvement
- Make every campaign outcome measurable (delivered, opened, clicked, converted)
- Prevent spam and unauthorized use of the notification system

### User Stories

| ID | As a… | I want to… | So that… |
|----|-------|-----------|----------|
| CS-01 | Marketing manager | Create a campaign targeting all Approved members who have not yet paid | I can prompt them to complete payment |
| CS-02 | Marketing manager | Send a push notification to all members registered for Event X | I can share last-minute event updates |
| CS-03 | Marketing manager | See open rates, click rates, and conversion rates for each campaign | I know which messages work |
| CS-04 | Admin | Require approval before any campaign is sent to > 500 members | I can prevent accidental mass sends |
| CS-05 | Member | Manage notification preferences (email / push / SMS per topic) | I only receive messages I want |

### Functional Requirements

#### Audience Segment Builder
New admin page `/communications/segments` with a segment rule builder:

| Dimension | Available filters |
|-----------|-----------------|
| Membership status | Pending, Approved, Active, Expired, Suspended |
| Role | Athlete, Coach, Referee, ClubAdmin |
| Club | Affiliated club (multi-select) |
| Event | Registered for event X |
| Course | Enrolled in course X |
| Expiry | Expires within N days |
| Geography | City / region (if collected at registration) |

A "preview segment" action shows estimated audience size before saving.

**New files:**
- `admin/src/pages/CampaignSegmentsPage.tsx`
- `src/SMF.Application/Features/Communications/Queries/EvaluateSegmentQuery.cs`

#### Campaign Lifecycle
A campaign goes through: `Draft` → `PendingApproval` (if audience > 500) → `Approved` → `Scheduled` / `Sending` → `Sent` → `Completed`.

The broadcast suite in `CommunicationEndpoints.cs` already has a preview + send/cancel pattern. The lifecycle extension adds:
- `POST /api/admin/campaigns/{id}/submit` — moves to `PendingApproval` if size ≥ threshold.
- `POST /api/admin/campaigns/{id}/approve` — `smf.admin` only.
- `POST /api/admin/campaigns/{id}/schedule` — sets a send time.

#### Campaign Analytics
- Per-message delivery status tracked in `CampaignDelivery` table (memberId, campaignId, deliveredAt, openedAt, clickedAt).
- Mobile/web apps must fire `POST /api/campaigns/{id}/track?event=open|click` on notification open/tap.
- New admin page `/communications/campaigns/{id}/report` shows delivery, open, click, and conversion funnel.

#### Member Notification Preferences
- New endpoint `GET/PUT /api/members/{id}/notification-preferences` — returns/updates per-topic opt-in flags.
- Topics: `event_updates`, `renewal_reminders`, `news`, `course_updates`, `federation_alerts`.
- Push token registration already exists (`/api/devices/register`) — link preference to device records.

**New file:** `web/src/pages/NotificationPreferencesPage.tsx` (accessible from `/account`)

### Acceptance Criteria
- [ ] A segment "Approved members not yet Active" can be created and previewed with a count.
- [ ] A campaign to an audience of 600 cannot be sent without a second approver action.
- [ ] Campaign delivery, open, and click counts update in the report page within 5 minutes of the event.
- [ ] A member who opts out of `news` topic does not receive news campaign messages.
- [ ] The existing unauthenticated `/api/notifications/send` endpoint returns `401` after Phase 1 auth is in place.

---

## Feature 4 — Partner / Club Self-Service Mini-Portal

### Problem Statement
Club administrators currently have no authenticated self-service access. They must contact the federation to update rosters, check compliance document deadlines, or view their members' event participation. This creates unnecessary federation staff workload.

### Goals
- Allow club admins to manage their club's roster, documents, and event participation without federation staff involvement
- Provide clubs with a read-only performance snapshot (rankings, certifications, event results)

### User Stories

| ID | As a… | I want to… | So that… |
|----|-------|-----------|----------|
| CP-01 | Club admin | Log in and see my club's current member roster | I can ensure it is up to date |
| CP-02 | Club admin | Upload our annual compliance documents (insurance, safety plan) | I can meet federation requirements without emailing staff |
| CP-03 | Club admin | See which of our members are registered for upcoming events | I can coordinate logistics |
| CP-04 | Club admin | See a performance snapshot (medals, rankings) for our athletes | I can report to club leadership |
| CP-05 | Club admin | Submit a request to add a new athlete to our club | I do not need to contact the federation directly |

### Functional Requirements

#### Club Admin Authentication
- Club admins identified by `Members.Role == ClubAdmin` and `Members.AffiliatedClubId != null`.
- Login reuses the member auth system (Feature 1). Role detection happens post-login.
- A logged-in `ClubAdmin` member sees an additional "Club Management" section in their account.

#### Roster Management
- `GET /api/clubs/{id}/members` — list of members affiliated to the club; filtered by `smf.clubadmin` token's club.
- Club admins can initiate affiliation requests for new members (`POST /api/clubs/{id}/affiliation-requests`); federation reviewer approves.

#### Compliance Document Upload
- `POST /api/clubs/{id}/documents` — multipart upload; extends existing compliance infrastructure.
- Doc types required by the federation (e.g., `insurance_certificate`, `safeguarding_policy`) declared as a config list; expiry dates tracked.
- Overdue documents shown as warnings on the club microsite.

#### Performance Snapshot
- Reuse existing `rankingsApi.ts` data filtered by club affiliation.
- New admin view showing club-level aggregate (total athletes, medals, event participations).

### Acceptance Criteria
- [ ] A logged-in ClubAdmin member can view only their club's roster.
- [ ] A ClubAdmin cannot view or modify another club's data.
- [ ] Uploading a compliance document is reflected immediately in the admin governance view.
- [ ] A ClubAdmin can see which of their athletes are registered for the next scheduled event.
