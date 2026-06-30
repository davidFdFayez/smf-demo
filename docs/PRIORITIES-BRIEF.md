# SMF Platform — Priority Validation Brief
**Prepared for:** Federation Leadership  
**Date:** May 2026  
**Purpose:** Confirm the order of attack before sprint planning begins

---

## Why This Order Matters

The platform is already feature-rich but has three compounding risks that, if left unaddressed, will undermine every new capability added on top:

1. **Security** — admin-grade API endpoints (approve members, issue certificates, send campaigns, fulfill orders) carry no access control today. Any party that can reach the server can invoke them.
2. **Member journey fragmentation** — members register, get approved, pay, and access e-learning through four disconnected flows with no single "my account" anchor. Support burden grows linearly with membership.
3. **Revenue leakage** — the membership checkout page exists but is not linked in the main navigation. Approved members who should pay cannot easily find the path.

Fixing these in the wrong order wastes effort: building a member portal on top of an unsecured API is dangerous; growing membership aggressively before the journey is coherent increases churn and support cost.

---

## Proposed Priority Stack

| Priority | Theme | Business Justification | Timeline |
|----------|-------|----------------------|----------|
| **1** | Security & Access Control | Protect existing admin operations from unauthorized access; regulatory and reputational obligation | 0–4 weeks |
| **2** | Member Journey Unification | Reduce support tickets; improve registration-to-active conversion; enable self-service | 4–10 weeks |
| **3** | Lifecycle Governance | Enable disciplinary and renewal workflows; protect federation reputation and event integrity | 8–14 weeks |
| **4** | Revenue & Retention | Increase ARPM (average revenue per member); reduce churn; introduce renewal automation | 3–5 months |
| **5** | Analytics & Intelligence | Enable data-driven decisions; sponsor dashboards; AI-driven safeguarding alerts | 5–9 months |

---

## Decision Points for Leadership

The following three decisions must be confirmed before Phase 1 work begins. Each has downstream implications on architecture and timeline.

### Decision 1 — Auth Provider
**Question:** Will the platform use an external identity provider (e.g., Saudi National SSO / Absher, Azure AD B2C, Auth0) or a custom JWT solution?

| Option | Pros | Cons |
|--------|------|------|
| External IdP (Absher / NAFATH) | National ID verification built-in; high trust for sports compliance | Integration timeline 4–8 weeks; government dependency |
| Azure AD B2C / Auth0 | Fast to implement; proven RBAC | Monthly cost; another vendor dependency |
| Custom JWT (extend current `dev-token` pattern) | Full control; no cost | Security responsibility; longer audit prep |

**Recommendation:** External IdP aligns with Vision 2030 digital-government direction. If timeline is tight, ship Custom JWT first then migrate.

### Decision 2 — Admin Access Model
**Question:** Should admin roles be fine-grained (Approver, Finance, Content, Compliance, Live-Ops) or coarse (Admin / Super-Admin)?

**Recommendation:** Start with three roles — **Reviewer** (approve/reject), **Finance** (payments, invoices, store), **Content** (events, news, e-learning). Collapse to two if team is small.

### Decision 3 — Suspension/Revocation Scope
**Question:** If a member is suspended, should they:
- (a) Lose access to events and digital ID only, or
- (b) Lose all active membership benefits including certificates?

**Recommendation:** Option (a) with a `Suspended` status between `Active` and an explicit `Revoked` terminal state. This lets disciplinary cases be resolved without permanently destroying the member record.

---

## Risks of Deprioritizing Security (Priority 1)

| Risk | Likelihood | Impact |
|------|-----------|--------|
| Unauthorized member approval by external party | Medium | High — integrity of entire member database |
| Unauthorized campaign/notification spam | High | High — reputational, possible regulator interest |
| Certificate issuance for ineligible members | Medium | High — event integrity, potential fraud |
| Unauthorized store order fulfillment | Low | Medium — financial |

**Conclusion:** Security hardening should proceed immediately, in parallel with UX discovery work for the member portal. Neither should wait on the other.

---

## Approval

| Role | Name | Decision | Date |
|------|------|----------|------|
| Secretary General | | ☐ Approved / ☐ Modified | |
| IT Director | | ☐ Approved / ☐ Modified | |
| Finance Director | | ☐ Approved / ☐ Modified | |
| Head of Compliance | | ☐ Approved / ☐ Modified | |

**Notes / modifications:**

> _Record any changes to the priority order or decisions here before sign-off._
