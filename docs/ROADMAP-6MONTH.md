# SMF Platform — 6-Month Product Roadmap
**Period:** June 2026 – November 2026  
**Version:** 1.0  
**Owner:** Product & Technology  
**Review cycle:** Bi-weekly steering; monthly roadmap revision

---

## Guiding Principles

1. **Security before scale** — no new acquisition campaigns or partnerships until endpoint authorization is fully deployed.
2. **Member journey first** — every engineering sprint must move at least one conversion metric (G2, G3, G4) before adding net-new capabilities.
3. **Measure everything** — each feature ships with the KPIs it is designed to move (from `KPI-DICTIONARY.md`).
4. **Small, shippable increments** — maximum two weeks per releasable unit; no features in progress for > 30 days.

---

## Roadmap at a Glance

```
Month 1   Month 2   Month 3   Month 4   Month 5   Month 6
Jun 2026  Jul 2026  Aug 2026  Sep 2026  Oct 2026  Nov 2026
────────  ────────  ────────  ────────  ────────  ────────
[==== PHASE 1: Security + Journey ====]
 1A RBAC ─────────────┐
 1B Journey fixes ────┤
 1C Dashboard KPIs ───┘
                      [======= PHASE 2: Member Platform =======]
                       2A Member portal (login) ──────────┐
                       2B Renewal engine ─────────────────┤
                       2C Comm. segmentation ─────────────┤
                       2D Club mini-portal ───────────────┘
                                          [= PHASE 3 Discovery =]
                                           3A Smart recs (design)
                                           3B Sponsor dashboards
                                           3C Safeguarding intelligence
```

---

## Phase 1 — Security Hardening & Member Journey (Jun – Aug 2026)

### Sprint 1 — Weeks 1–2 (2–13 Jun)
**Goal:** API protection foundation in place; no admin endpoint reachable without a token.

| Item | Owner | KPI impact | Status |
|------|-------|-----------|--------|
| Add JWT middleware to `Program.cs` | Backend | Risk reduction | Planned |
| Define RBAC policies (`smf.reviewer`, `smf.finance`, `smf.content`, `smf.admin`) | Backend | Risk reduction | Planned |
| Apply `RequireAuthorization` to all admin endpoints (FR-1A-03) | Backend | Risk reduction | Planned |
| Disable dev-token in Production (FR-1A-01) | Backend | Risk reduction | Planned |
| Admin SPA: add `AuthContext` + attach JWT in `httpClient.ts` (FR-1A-06) | Frontend (admin) | Risk reduction | Planned |

**Exit criteria:** Zero admin endpoints reachable without a valid token. Confirmed by automated integration test suite.

---

### Sprint 2 — Weeks 3–4 (16–27 Jun)
**Goal:** Domain lifecycle hardened; domain guards for Approve/Digital-ID in place.

| Item | Owner | KPI impact | Status |
|------|-------|-----------|--------|
| Guard `Approve` handler against Active member regression (FR-1A-04) | Backend | Risk reduction | Planned |
| Guard `IssueDigitalId` — Active status required (FR-1A-05) | Backend | Risk reduction | Planned |
| Add `Rejected` and `Suspended` to `RegistrationStatus` enum | Backend | O1 (admin throughput) | Planned |
| `Member.Reject()`, `Member.Suspend()`, `Member.Reinstate()` domain methods | Backend | C1 (backlog governance) | Planned |
| New endpoints: `/reject`, `/suspend`, `/reinstate` + handlers | Backend | C1 | Planned |
| Admin UI: Reject / Suspend / Reinstate buttons on member detail (FR-1C-02) | Frontend (admin) | C1 | Planned |

**Exit criteria:** Admin can reject a Pending member with a reason; can suspend/reinstate an Active member. Rejected and Suspended members cannot be issued digital IDs.

---

### Sprint 3 — Weeks 5–6 (30 Jun – 11 Jul)
**Goal:** Member journey fixes shipped; checkout visible and conversion path clear.

| Item | Owner | KPI impact | Status |
|------|-------|-----------|--------|
| "Pay Membership Fee" CTA in public nav (FR-1B-01) | Frontend (web) | G4 (time-to-payment) | Planned |
| Post-registration deep link guidance (FR-1B-02) | Frontend (web) | G4 | Planned |
| `/my-membership` status page (FR-1B-03) | Frontend (web) | G2 (conversion), G4 | Planned |
| Checkout: replace member picker with ID lookup (FR-1B-04) | Frontend (web) | G2 | Planned |
| E-learning GUID persistence from status page (FR-1B-05) | Frontend (web) | E1 (e-learning completion) | Planned |

**Exit criteria:** An Approved member visiting the site can find the payment path within 2 clicks. E2E test covers the full Approved → pay → Active journey.

---

### Sprint 4 — Weeks 7–8 (14–25 Jul)
**Goal:** Admin dashboard shows all KPIs from the KPI dictionary.

| Item | Owner | KPI impact | Status |
|------|-------|-----------|--------|
| Extend `AdminStats` with store revenue, PSR, backlog age, conversion % (FR-1C-01) | Backend | R3, R4, C1, G2 | Planned |
| Dashboard: new KPI cards for extended stats | Frontend (admin) | All above | Planned |
| Dashboard: funnel visualization (FR-1C-03) | Frontend (admin) | G2 | Planned |
| Establish baseline measurements for all KPIs in `KPI-DICTIONARY.md` | Analytics | All | Planned |

**Exit criteria:** Dashboard reflects all KPIs in `KPI-DICTIONARY.md`. Baseline values documented in the KPI dictionary.

---

### Phase 1 Milestones Summary

| Milestone | Target date | Success metric |
|-----------|------------|---------------|
| M1 — All admin endpoints protected | 13 Jun 2026 | 0 unprotected admin routes in pen-test report |
| M2 — Lifecycle governance complete | 27 Jun 2026 | Reject/Suspend/Reinstate available in admin UI |
| M3 — Member journey fixed | 11 Jul 2026 | TTP (G4) improves by ≥ 30% vs baseline |
| M4 — Full dashboard KPIs live | 25 Jul 2026 | All 16 KPIs rendering with data |

---

## Phase 2 — Member Platform (Aug – Oct 2026)

### Sprint 5–6 — Weeks 9–12 (28 Jul – 22 Aug)
**Goal:** Member login and account dashboard live.

| Item | Owner | KPI impact | Status |
|------|-------|-----------|--------|
| `MemberCredential` entity + migration | Backend | Enabler for E1, G2 | Planned |
| Login / Refresh / Logout endpoints | Backend | Enabler | Planned |
| Email verification flow | Backend | Trust/compliance | Planned |
| Web: `AuthContext`, `/login`, `/account` pages | Frontend (web) | G2, E1, support tickets | Planned |
| Web: attach JWT to all authenticated requests | Frontend (web) | Enabler | Planned |
| Forgotten password flow | Backend + Frontend | Trust | Planned |

**Exit criteria:** Members can register, verify email, log in, view account dashboard, access e-learning without GUID prompt, and download their digital ID — all without contacting support.

---

### Sprint 7–8 — Weeks 13–16 (25 Aug – 19 Sep)
**Goal:** Renewal engine running; no member lapses without being notified.

| Item | Owner | KPI impact | Status |
|------|-------|-----------|--------|
| `MembershipExpiresAtUtc` + `GracePeriod` / `Expired` status | Backend | Retention | Planned |
| `RenewalBackgroundService` with notification triggers | Backend | Retention | Planned |
| `POST /api/members/{id}/renew` endpoint | Backend | R1 (MRR-M) | Planned |
| Account page: "Renew" CTA for near-expiry / grace period members | Frontend (web) | Retention | Planned |
| Admin: expiring members list page | Frontend (admin) | O1 | Planned |

**Exit criteria:** Background service sends expiry reminders on schedule. Grace period transitions are automated. Admin can view who is expiring in the next 30 days.

---

### Sprint 9–10 — Weeks 17–20 (22 Sep – 17 Oct)
**Goal:** Segmented campaigns available; marketing team self-sufficient.

| Item | Owner | KPI impact | Status |
|------|-------|-----------|--------|
| Audience segment builder (admin) | Frontend (admin) | Campaign ROI | Planned |
| `EvaluateSegmentQuery` backend | Backend | Campaign ROI | Planned |
| Campaign lifecycle: PendingApproval → Approved guard | Backend | Risk reduction | Planned |
| Campaign delivery + tracking table | Backend | CS metrics | Planned |
| Campaign report page in admin | Frontend (admin) | CS metrics | Planned |
| Member notification preferences (`/account`) | Frontend (web) | Trust | Planned |

**Exit criteria:** Marketing can create a segment, preview audience size, schedule a campaign with dual approval for large sends, and view delivery + click metrics within 5 minutes of send.

---

### Sprint 11–12 — Weeks 21–24 (20 Oct – 14 Nov)
**Goal:** Club self-service portal live; federation staff can redirect club admin enquiries to the portal.

| Item | Owner | KPI impact | Status |
|------|-------|-----------|--------|
| Club admin role detection post-login | Backend | O1 | Planned |
| Roster view (GET /api/clubs/{id}/members filtered by token) | Backend | O1 | Planned |
| Affiliation request flow | Backend | O1 | Planned |
| Compliance document upload (extend existing compliance API) | Backend | C2 | Planned |
| Club admin section in `/account` | Frontend (web) | O1 | Planned |
| Club performance snapshot | Frontend (web) | Engagement | Planned |

**Exit criteria:** A ClubAdmin can log in, view their roster, upload required compliance documents, and check event participation without contacting the federation.

---

### Phase 2 Milestones Summary

| Milestone | Target date | Success metric |
|-----------|------------|---------------|
| M5 — Member login live | 22 Aug 2026 | Support tickets related to GUID re-entry: 0 |
| M6 — Renewal engine live | 19 Sep 2026 | Year-1 retention rate established as baseline |
| M7 — Segmented campaigns live | 17 Oct 2026 | First campaign open rate measured; auth on notification endpoints |
| M8 — Club portal live | 14 Nov 2026 | ≥ 50% of clubs upload compliance docs via portal in first 30 days |

---

## Phase 3 — Innovation Discovery (Nov – Dec 2026)

Phase 3 work begins as design/discovery during Phase 2 sprints, so engineering can start implementation in January 2027.

| Initiative | Business value | Discovery owner | Start |
|-----------|---------------|----------------|-------|
| Smart member recommendations (next event / course / certification) | Increase E1, E2 per-member | Product | Oct 2026 |
| Sponsor-facing analytics dashboards + branded microsites | New revenue stream | Commercial | Nov 2026 |
| Safeguarding intelligence layer (trend heatmaps, alert patterns) | Regulatory & reputation | Compliance | Nov 2026 |
| NAFATH / National SSO integration | Long-term trust, Vision 2030 alignment | Engineering + Gov relations | Nov 2026 |

---

## Owners Matrix

| Role | Phase 1 | Phase 2 | Phase 3 |
|------|---------|---------|---------|
| Product Manager | Requirements sign-off; backlog grooming | PRD delivery; user testing | Discovery facilitation |
| Backend Lead | Streams 1A, 1C; domain hardening | Auth, renewal engine | Architecture for recommendations |
| Frontend Lead (web) | Stream 1B | Member portal; renewal UX | — |
| Frontend Lead (admin) | Stream 1A (admin client); 1C dashboard | Segments; campaign reports; club portal | Sponsor dashboards |
| QA Lead | Integration test coverage for RBAC | E2E login + renewal flows | — |
| Marketing Manager | Baseline KPI tracking | Campaign creation; preference management | Sponsor dashboard requirements |
| Compliance Officer | Policy review for RBAC roles | Notification preferences; club documents | Safeguarding intelligence spec |

---

## Budget & Resource Estimate

| Phase | Engineering weeks | Notes |
|-------|-----------------|-------|
| Phase 1 | 8 sprints × (1 BE + 1 FE web + 1 FE admin) = ~24 person-weeks | Security work is sequential; journey and dashboard can overlap |
| Phase 2 | 8 sprints × (1.5 BE + 1 FE web + 0.5 FE admin) = ~24 person-weeks | Auth and renewal are parallel tracks |
| Phase 3 discovery | 4 person-weeks | Design + tech spike only |
| **Total** | **~52 person-weeks** | Assumes no scope change or unplanned incidents |

---

## Risk Register

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| Auth provider decision delayed | High | High | Start with custom JWT; design for provider swap |
| Domain schema changes (Rejected, Suspended, Expired statuses) cause migration issues | Medium | Medium | Run DB migrations in staging 1 week before production |
| Phase 1 takes longer than 8 weeks due to hidden auth complexity | Medium | High | De-scope funnel visualization (1C-03) to Phase 2 if needed |
| Marketing team not ready to use segment builder | Low | Medium | Run a hands-on training session at M7 |
| NAFATH integration timeline exceeds 6 months | High | Low (for Phase 2) | Not required for Phase 2; parked in Phase 3 |

---

## Sign-Off

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Product Manager | | | |
| CTO / IT Director | | | |
| Secretary General | | | |
| Head of Finance | | | |
| Head of Compliance | | | |

**Next review date:** _______________

**Distribution:** All roadmap documents (`docs/`) are version-controlled in the repository. Changes must be proposed via pull request and require sign-off from Product Manager + at least one leadership representative.
