import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useI18n } from "../i18n";
import {
  getEventById,
  registerForEvent,
  type EventSummary,
} from "../services/eventsApi";
import { initializePayment } from "../services/paymentsApi";
import {
  PaymentProvider,
  PaymentPurpose,
  formatMinor,
} from "../features/payments/types";
import { buildEmbedUrl } from "../services/liveStreamsApi";

/**
 * Public event page with end-to-end registration → payment flow:
 *
 *   1. Fetch event details (`GET /api/events/:id`).
 *   2. User enters their member id + selects a payment method.
 *   3. We call `POST /api/events/:id/registrations` to get a
 *      `registrationId` in "PendingPayment".
 *   4. We call `POST /api/payments` with `eventRegistrationId` set so the
 *      backend can auto-confirm the registration when the gateway posts a
 *      successful webhook.
 *   5. We redirect the browser to the provider's checkout URL.
 *
 * The "Register (free event)" path skips step 4 when the entry fee is 0.
 */
export function EventDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { t, locale } = useI18n();
  const [event, setEvent] = useState<EventSummary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!id) return;
    const ctrl = new AbortController();
    setLoading(true);
    getEventById(id, ctrl.signal)
      .then((e) => {
        setEvent(e);
        setError(null);
      })
      .catch(() => setError(t("common.error_generic")))
      .finally(() => setLoading(false));
    return () => ctrl.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  const df = useMemo(
    () =>
      new Intl.DateTimeFormat(locale === "ar" ? "ar-SA" : "en-US", {
        dateStyle: "full",
        timeStyle: "short",
      }),
    [locale],
  );

  if (loading) {
    return (
      <section className="mx-auto max-w-5xl px-6 py-12 text-sm text-slate-500">
        {t("common.loading")}
      </section>
    );
  }

  if (!event) {
    return (
      <section className="mx-auto max-w-5xl px-6 py-12">
        <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
          {error ?? "Event not found."}
        </div>
        <Link to="/events" className="mt-4 inline-block text-sm text-smf-700 hover:underline">
          ← {t("nav.events")}
        </Link>
      </section>
    );
  }

  const registrationOpen = event.status === "RegistrationOpen";
  const seatsLeft =
    event.capacity == null ? null : Math.max(0, event.capacity - event.registrationCount);

  return (
    <section className="mx-auto max-w-5xl px-6 py-12">
      <Link to="/events" className="text-sm text-smf-700 hover:underline">
        ← {t("nav.events")}
      </Link>

      <div className="mt-4 public-card overflow-hidden">
        <div className="bg-gradient-to-r from-navy-800 to-smf-700 px-6 py-4 text-white">
          <div className="text-xs font-semibold uppercase tracking-wide text-white/75">
            {event.status}
          </div>
          <h1 className="mt-1 text-3xl font-bold">{event.title}</h1>
          <div className="mt-1 text-sm text-white/85">
            {df.format(new Date(event.startsAtUtc))} · {event.location}
          </div>
        </div>

        <div className="grid gap-8 p-6 sm:grid-cols-[1.4fr_1fr]">
          <div>
            {event.description && (
              <p className="whitespace-pre-wrap text-sm leading-6 text-slate-700">
                {event.description}
              </p>
            )}

            <dl className="mt-6 grid grid-cols-2 gap-4 text-sm">
              <Field label={t("events.location")} value={event.location} />
              <Field
                label="Starts"
                value={df.format(new Date(event.startsAtUtc))}
              />
              <Field
                label="Ends"
                value={df.format(new Date(event.endsAtUtc))}
              />
              <Field
                label={t("events.fee")}
                value={formatMinor(
                  event.entryFeeMinor,
                  event.currency,
                  locale === "ar" ? "ar-SA" : "en-US",
                )}
              />
              <Field
                label="Registration opens"
                value={df.format(new Date(event.registrationOpensAtUtc))}
              />
              <Field
                label="Registration closes"
                value={df.format(new Date(event.registrationClosesAtUtc))}
              />
              <Field
                label={t("events.seats")}
                value={
                  event.capacity == null
                    ? t("events.unlimited")
                    : `${event.registrationCount} / ${event.capacity}` +
                      (seatsLeft != null ? ` (${seatsLeft} left)` : "")
                }
              />
              <Field label="Currency" value={event.currency} />
            </dl>

            {event.liveStreamUrl && event.liveStreamProvider && (
              <div className="mt-6 aspect-video w-full overflow-hidden rounded-xl bg-black">
                <iframe
                  className="h-full w-full"
                  src={buildEmbedUrl(event.liveStreamProvider, event.liveStreamUrl)}
                  title={`Live stream for ${event.title}`}
                  allow="autoplay; encrypted-media; picture-in-picture"
                  allowFullScreen
                />
              </div>
            )}
          </div>

          <RegistrationPanel
            event={event}
            disabled={!registrationOpen}
            locale={locale}
          />
        </div>
      </div>
    </section>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{label}</dt>
      <dd className="mt-0.5 text-slate-800">{value}</dd>
    </div>
  );
}

function RegistrationPanel({
  event,
  disabled,
  locale,
}: {
  event: EventSummary;
  disabled: boolean;
  locale: string;
}) {
  const [memberId, setMemberId] = useState("");
  const [provider, setProvider] = useState<PaymentProvider>(PaymentProvider.Mada);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<{ kind: "ok" | "err"; text: string } | null>(null);

  const fmt = (n: number) =>
    formatMinor(n, event.currency, locale === "ar" ? "ar-SA" : "en-US");
  const isPaid = event.entryFeeMinor > 0;

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!memberId.trim()) {
      setNotice({ kind: "err", text: "Please enter your member ID." });
      return;
    }
    setBusy(true);
    setNotice(null);
    try {
      const regId = await registerForEvent(event.id, memberId.trim());
      if (!isPaid) {
        setNotice({
          kind: "ok",
          text: `You're registered. Reference: ${regId}`,
        });
        setBusy(false);
        return;
      }
      const res = await initializePayment({
        memberId: memberId.trim(),
        provider,
        purpose: PaymentPurpose.EventFee,
        amountMinor: event.entryFeeMinor,
        currency: event.currency,
        callbackUrl: `${window.location.origin}/events/${event.id}?paid=1`,
        description: `Entry fee — ${event.title}`,
        eventRegistrationId: regId,
      });
      window.location.href = res.redirectUrl;
    } catch (err) {
      setBusy(false);
      setNotice({
        kind: "err",
        text: err instanceof Error ? err.message : "Registration failed.",
      });
    }
  };

  return (
    <aside className="rounded-xl border border-slate-200 bg-white/60 p-5">
      <h2 className="text-lg font-semibold text-slate-900">Register</h2>
      <p className="mt-1 text-xs text-slate-500">
        {isPaid
          ? `Secure your seat — entry fee ${fmt(event.entryFeeMinor)}.`
          : "This event is free. Submit your member ID to reserve a seat."}
      </p>

      {disabled && (
        <div className="mt-3 rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs text-amber-800">
          Registration is currently closed for this event.
        </div>
      )}

      <form onSubmit={submit} className="mt-4 space-y-3">
        <label className="block text-sm">
          <span className="font-medium text-slate-700">Member ID</span>
          <input
            type="text"
            value={memberId}
            onChange={(e) => setMemberId(e.target.value)}
            placeholder="00000000-0000-0000-0000-000000000000"
            disabled={disabled || busy}
            className="mt-1 block w-full rounded-lg border-slate-300 bg-white px-3 py-2 font-mono text-xs shadow-sm focus:border-smf-500 focus:ring-smf-500 disabled:bg-slate-100"
          />
        </label>

        {isPaid && (
          <fieldset className="text-sm">
            <legend className="font-medium text-slate-700">Payment method</legend>
            <div className="mt-1 flex flex-wrap gap-2">
              {(Object.values(PaymentProvider) as PaymentProvider[]).map((p) => (
                <label
                  key={p}
                  className={`cursor-pointer rounded-full border px-3 py-1 text-xs font-medium ${
                    provider === p
                      ? "border-smf-500 bg-smf-50 text-smf-800"
                      : "border-slate-300 bg-white text-slate-700 hover:border-slate-400"
                  }`}
                >
                  <input
                    type="radio"
                    name="provider"
                    value={p}
                    className="sr-only"
                    checked={provider === p}
                    onChange={() => setProvider(p)}
                  />
                  {p}
                </label>
              ))}
            </div>
          </fieldset>
        )}

        <button
          type="submit"
          disabled={disabled || busy}
          className="w-full rounded-lg bg-smf-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-smf-700 disabled:cursor-not-allowed disabled:bg-slate-300"
        >
          {busy ? "Processing…" : isPaid ? `Register & pay ${fmt(event.entryFeeMinor)}` : "Register"}
        </button>
      </form>

      {notice && (
        <div
          className={`mt-3 rounded-lg p-3 text-xs ${
            notice.kind === "ok"
              ? "border border-emerald-200 bg-emerald-50 text-emerald-800"
              : "border border-rose-200 bg-rose-50 text-rose-800"
          }`}
        >
          {notice.text}
        </div>
      )}
    </aside>
  );
}
