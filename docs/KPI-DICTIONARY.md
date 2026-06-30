# SMF Platform — KPI Dictionary & Metrics Baseline
**Owner:** Business Operations  
**Review Cadence:** Monthly (dashboard); Quarterly (targets)  
**Source of truth:** `GET /api/admin/stats` + future dedicated analytics endpoints

---

## How to Read This Document

Each KPI entry contains:
- **Definition** — precise calculation
- **Data source** — which table/field/API drives it today
- **Baseline** — set to "TBD — measure after first 30 days of live operation"
- **Target** — where the federation wants to be in 6 months
- **Signal** — what a bad trend looks like and what to do

---

## Theme 1 — Growth & Acquisition

### G1 · Total Registered Members
| Field | Value |
|-------|-------|
| Definition | COUNT of all `Members` rows regardless of status |
| Formula | `SELECT COUNT(*) FROM Members` |
| Data source | `AdminStats.TotalMembers` → `GET /api/admin/stats` |
| Baseline | TBD |
| Target (6 mo.) | Define with leadership during roadmap sign-off |
| Cadence | Daily |

**Signal:** Flat curve after an event/campaign = acquisition channel is exhausted; investigate referral or visibility.

---

### G2 · Registration-to-Active Conversion Rate
| Field | Value |
|-------|-------|
| Definition | % of members who reach `Active` status within 30 days of registering |
| Formula | `Active members registered in period / Total members registered in same period × 100` |
| Data source | `Members.RegistrationStatus`, `Members.CreatedAtUtc`, `Payments.CompletedAtUtc` |
| Baseline | TBD |
| Target | ≥ 60% within 30 days |
| Cadence | Weekly |

**Signal:** Rate below 40% indicates friction in the approval or payment steps. Drill into the sub-funnel:
- Pending → Approved lag (admin bottleneck)
- Approved → Active lag (checkout discoverability or payment failure)

---

### G3 · Time-to-Approval (TTA)
| Field | Value |
|-------|-------|
| Definition | Median calendar days from `Members.CreatedAtUtc` to admin approval event |
| Formula | `MEDIAN(ApprovalTimestamp - CreatedAtUtc)` per cohort |
| Data source | Requires audit event table (planned Phase 1) |
| Baseline | TBD |
| Target | ≤ 3 business days |
| Cadence | Weekly |

**Signal:** TTA > 5 days indicates staffing gap or missing admin notification. Trigger: auto-escalation email at day 4.

---

### G4 · Time-to-Payment (TTP)
| Field | Value |
|-------|-------|
| Definition | Median calendar days from approval to first successful payment |
| Formula | `MEDIAN(Payment.CompletedAtUtc - ApprovalTimestamp)` |
| Data source | `Payments.CompletedAtUtc`, approval event (planned) |
| Baseline | TBD |
| Target | ≤ 7 days |
| Cadence | Weekly |

**Signal:** TTP > 14 days = member is Approved but cannot find or complete checkout. Priority fix: surface checkout CTA in approval email and public nav.

---

## Theme 2 — Revenue

### R1 · Monthly Recurring Revenue — Membership (MRR-M)
| Field | Value |
|-------|-------|
| Definition | Sum of successful membership-fee payments in the calendar month, in SAR |
| Formula | `SUM(Payments.AmountMinor WHERE Purpose=MembershipFee AND Status=Succeeded AND CompletedAt in month) / 100` |
| Data source | `AdminStats.MembershipRevenueThisMonthMinor` |
| Baseline | TBD |
| Target | Define after first 90 days |
| Cadence | Daily |

---

### R2 · Monthly Event Revenue (MER)
| Field | Value |
|-------|-------|
| Definition | Sum of successful event-fee payments in the calendar month |
| Formula | `SUM(Payments.AmountMinor WHERE Purpose=EventFee AND Status=Succeeded AND CompletedAt in month) / 100` |
| Data source | `AdminStats.EventRevenueThisMonthMinor` |
| Baseline | TBD |
| Target | Define per event season |
| Cadence | Daily |

---

### R3 · Store Revenue (Monthly)
| Field | Value |
|-------|-------|
| Definition | Sum of store orders in `Paid` or `Fulfilled` status during the month |
| Formula | `SUM(StoreOrders.TotalMinor WHERE Status IN (Paid, Fulfilled) AND CreatedAt in month) / 100` |
| Data source | Store orders table (no current admin stats aggregation — planned Phase 1 dashboard enhancement) |
| Baseline | TBD |
| Cadence | Daily |

---

### R4 · Payment Success Rate (PSR)
| Field | Value |
|-------|-------|
| Definition | % of payment attempts that result in `Succeeded` status |
| Formula | `Succeeded payments / Total initiated payments × 100` |
| Data source | `Payments` table, `Status` column |
| Baseline | TBD |
| Target | ≥ 90% |
| Cadence | Daily |

**Signal:** PSR < 80% = provider issue, card failure, or UX friction (wrong payment method shown). Check `PaymentCallbackController` error logs.

---

### R5 · Average Revenue per Active Member (ARPM)
| Field | Value |
|-------|-------|
| Definition | Total platform revenue (membership + events + store) / Active member count in the period |
| Formula | `(MRR-M + MER + Store Revenue) / Active Members` |
| Data source | Combined from R1 + R2 + R3 + G1 sub-count |
| Baseline | TBD |
| Target | Track trend; increase 20% YoY |
| Cadence | Monthly |

---

## Theme 3 — Engagement & Experience

### E1 · E-Learning Completion Rate
| Field | Value |
|-------|-------|
| Definition | % of enrollments that result in a completed course (all lessons marked complete) |
| Formula | `Completed enrollments / Total enrollments × 100` |
| Data source | E-learning enrollments table + lesson completion events |
| Baseline | TBD |
| Target | ≥ 50% |
| Cadence | Monthly |

---

### E2 · Event Fill Rate
| Field | Value |
|-------|-------|
| Definition | % of available event capacity filled (registered participants / capacity) |
| Formula | `Event registrations / Event.MaxParticipants × 100` |
| Data source | `EventRegistrations`, `Events.MaxParticipants` |
| Baseline | TBD |
| Target | ≥ 70% per event |
| Cadence | Per event |

---

### E3 · Certificate Issuance Volume
| Field | Value |
|-------|-------|
| Definition | Count of certificates issued per month, broken down by type |
| Data source | `Certificates` table |
| Baseline | TBD |
| Cadence | Monthly |

---

## Theme 4 — Compliance & Safety

### C1 · Pending Approvals Backlog Age
| Field | Value |
|-------|-------|
| Definition | Count of members in `Pending` status older than 3 business days |
| Formula | `COUNT(Members WHERE Status=Pending AND DATEDIFF(day, CreatedAtUtc, NOW()) > 3)` |
| Data source | `Members` table |
| Target | 0 (all processed within 3 business days) |
| Cadence | Daily; alert if > 0 at end of business day |

---

### C2 · Safeguarding Report Resolution Time
| Field | Value |
|-------|-------|
| Definition | Median days from safeguarding report submission to triage completion |
| Data source | Safeguarding reports table, triage timestamp |
| Target | ≤ 5 business days for initial triage |
| Cadence | Weekly |

---

### C3 · Guardian Consent Completion Rate
| Field | Value |
|-------|-------|
| Definition | % of guardian consent requests that are approved within 7 days |
| Formula | `Approved consent requests / Total sent within 7 days × 100` |
| Data source | Parental consent records |
| Target | ≥ 80% |
| Cadence | Weekly |

---

## Theme 5 — Operations

### O1 · Admin Task Throughput
| Field | Value |
|-------|-------|
| Definition | Count of admin-driven actions per week (approvals, rejections, certificate issuances, fulfillments) |
| Data source | Audit event log (planned) |
| Baseline | TBD |
| Cadence | Weekly |

---

### O2 · Outbox Processing Lag
| Field | Value |
|-------|-------|
| Definition | Median time between outbox message creation and successful dispatch |
| Data source | `OutboxMessages.CreatedAt` vs `ProcessedAt` |
| Target | < 60 seconds under normal load |
| Cadence | Continuous monitoring; alert on p95 > 5 min |

---

## Dashboard Implementation Notes

The current `GET /api/admin/stats` response covers G1, R1, R2, and parts of G2 (MembersByStatus). The following additions are needed to fully populate this KPI dictionary in the admin dashboard:

| Missing KPI | Required Backend Change |
|-------------|------------------------|
| G2 Conversion Rate | New query joining Members + Payments by cohort month |
| G3 / G4 Time metrics | Audit event table + new stats endpoint fields |
| R3 Store Revenue | Add `StoreRevenueThisMonthMinor` to `AdminStats` record |
| R4 PSR | Add `PaymentsSuccessRatePct` to stats or new `/api/admin/payments/summary` |
| C1 Backlog Age | Add `PendingOlderThan3DaysCount` to stats |
| O2 Outbox Lag | New `/api/admin/outbox/health` endpoint |

These additions are scoped into Phase 1 requirements (see `PHASE1-REQUIREMENTS.md`).
