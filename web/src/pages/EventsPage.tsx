import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import clsx from "clsx";
import { useI18n } from "../i18n";
import { listEvents, type EventSummary } from "../services/eventsApi";

export function EventsPage() {
  const { t, locale } = useI18n();
  const [events, setEvents] = useState<EventSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const ctrl = new AbortController();
    setLoading(true);
    listEvents(ctrl.signal)
      .then((all) => {
        // Hide Draft events from the public. Everything else is visible.
        setEvents(all.filter((e) => e.status !== "Draft"));
        setError(null);
      })
      .catch(() => setError(t("common.error_generic")))
      .finally(() => setLoading(false));
    return () => ctrl.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const df = new Intl.DateTimeFormat(locale === "ar" ? "ar-SA" : "en-US", {
    dateStyle: "medium", timeStyle: "short",
  });

  return (
    <section className="mx-auto max-w-6xl px-6 py-12">
      <h1 className="text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">{t("events.title")}</h1>
      <p className="mt-2 max-w-3xl text-slate-600">{t("events.subtitle")}</p>

      {error && (
        <div className="mt-6 rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
          {error}
        </div>
      )}

      {loading && events.length === 0 ? (
        <p className="mt-8 text-sm text-slate-500">{t("common.loading")}</p>
      ) : events.length === 0 ? (
        <p className="mt-8 text-sm text-slate-500">{t("events.empty")}</p>
      ) : (
        <ul className="mt-8 grid gap-5 md:grid-cols-2">
          {events.map((e) => (
            <li key={e.id} className="public-card overflow-hidden">
              <div className="flex items-center justify-between bg-gradient-to-r from-navy-800 to-smf-700 px-5 py-3 text-white">
                <StatusPill status={e.status} />
                <span className="text-xs font-medium text-white/80">
                  {df.format(new Date(e.startsAtUtc))}
                </span>
              </div>
              <div className="p-5">
                <h3 className="text-lg font-semibold text-slate-900">{e.title}</h3>
                {e.description && <p className="mt-2 text-sm text-slate-600">{e.description}</p>}
                <dl className="mt-4 grid grid-cols-3 gap-3 text-xs">
                  <Field label={t("events.location")} value={e.location} />
                  <Field label={t("events.fee")} value={`${(e.entryFeeMinor / 100).toFixed(2)} ${e.currency}`} />
                  <Field label={t("events.seats")} value={e.capacity == null ? t("events.unlimited") : `${e.registrationCount}/${e.capacity}`} />
                </dl>
                <div className="mt-4">
                  <Link
                    to={`/events/${e.id}`}
                    className="inline-flex items-center text-sm font-semibold text-smf-700 hover:text-smf-800"
                  >
                    {t("events.view_details")} →
                  </Link>
                </div>
              </div>
            </li>
          ))}
        </ul>
      )}
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

function StatusPill({ status }: { status: EventSummary["status"] }) {
  const label = status.replace(/([A-Z])/g, " $1").trim();
  const cls: Record<EventSummary["status"], string> = {
    Draft: "bg-white/20 text-white",
    Published: "bg-white/20 text-white",
    RegistrationOpen: "bg-emerald-500/30 text-white ring-1 ring-inset ring-emerald-300/40",
    RegistrationClosed: "bg-amber-400/30 text-white ring-1 ring-inset ring-amber-300/40",
    InProgress: "bg-indigo-500/30 text-white ring-1 ring-inset ring-indigo-300/40",
    Completed: "bg-white/20 text-white",
    Cancelled: "bg-rose-500/30 text-white ring-1 ring-inset ring-rose-300/40",
  };
  return <span className={clsx("inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium", cls[status])}>{label}</span>;
}
