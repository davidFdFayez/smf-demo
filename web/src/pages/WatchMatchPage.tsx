import { useEffect, useMemo, useState } from "react";

import { useSearchParams } from "react-router-dom";

import { useI18n } from "../i18n";

import { Scoreboard } from "../features/scoring/Scoreboard";

import { getTenantByCode, type ScoringTenantDto } from "../services/tenantsApi";

import {

  buildEmbedUrl,

  getMatchLiveStreamByCode,

  type MatchLiveStreamDto,

} from "../services/liveStreamsApi";

import { HlsVideoPlayer, isDirectVideoUrl } from "../features/watch/HlsVideoPlayer";

import { getMatchInsights, type MatchInsights } from "../services/analyticsApi";

import { listMatches, type MatchSummary } from "../services/matchesApi";

const DemoLiveMatchCode = "match-002";
const SampleMatchCode = "match-001";



/**

 * Public "watch" screen that ties three advanced features into one view:

 *   - White-label theming driven by ?tenant=CODE

 *   - Live stream embed for the selected match

 *   - AI insights sidebar (tempo, momentum, predicted winner)

 *

 * Everything below the scoreboard degrades gracefully — if there's no

 * tenant / stream / match data, the user still sees the plain Scoreboard.

 */

export function WatchMatchPage() {

  const { t } = useI18n();

  const [params, setParams] = useSearchParams();

  const tenantCode = params.get("tenant") ?? "";

  const matchCode = params.get("match") ?? DemoLiveMatchCode;
  const [pendingCode, setPendingCode] = useState(matchCode);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => setPendingCode(matchCode), [matchCode]);

  const [matches, setMatches] = useState<MatchSummary[]>([]);

  const [matchesLoading, setMatchesLoading] = useState(true);

  // When landing on legacy match-001, pre-select the live demo match-002.
  useEffect(() => {
    if (matchesLoading || matches.length === 0) return;
    const preferred =
      matches.find((m) => m.code === DemoLiveMatchCode)?.code ?? matches[0]?.code;
    if (!preferred) return;
    if (!matches.some((m) => m.code === pendingCode)) {
      setPendingCode(preferred);
      return;
    }
    if (matchCode === SampleMatchCode && preferred !== SampleMatchCode && pendingCode === matchCode) {
      setPendingCode(preferred);
    }
  }, [matches, matchesLoading, pendingCode, matchCode]);

  const [tenant, setTenant] = useState<ScoringTenantDto | null>(null);

  const [stream, setStream] = useState<MatchLiveStreamDto | null>(null);

  const [insights, setInsights] = useState<MatchInsights | null>(null);

  const [insightsError, setInsightsError] = useState(false);



  useEffect(() => {

    const ctrl = new AbortController();

    setMatchesLoading(true);

    listMatches(ctrl.signal)

      .then((data) => {

        if (!ctrl.signal.aborted) setMatches(data);

      })

      .catch(() => {

        if (!ctrl.signal.aborted) setMatches([]);

      })

      .finally(() => {

        if (!ctrl.signal.aborted) setMatchesLoading(false);

      });

    return () => ctrl.abort();

  }, []);



  // Tenant → theme lookup. Falls back to federation brand if unknown.

  useEffect(() => {

    if (!tenantCode) {

      setTenant(null);

      return;

    }

    const ctrl = new AbortController();

    getTenantByCode(tenantCode, ctrl.signal).then(setTenant);

    return () => ctrl.abort();

  }, [tenantCode]);



  // Stream URL for the selected match.

  useEffect(() => {

    const ctrl = new AbortController();

    setStream(null);

    getMatchLiveStreamByCode(matchCode, ctrl.signal)

      .then((data) => {

        if (!ctrl.signal.aborted) setStream(data);

      })

      .catch(() => {

        if (!ctrl.signal.aborted) setStream(null);

      });

    return () => ctrl.abort();

  }, [matchCode]);



  // Match insights, refreshed every 15s so the sidebar tracks a live bout.

  useEffect(() => {

    let alive = true;

    const poll = async () => {

      try {

        const data = await getMatchInsights(matchCode);

        if (alive) {

          setInsights(data);

          setInsightsError(false);

        }

      } catch {

        if (alive) {

          setInsights(null);

          setInsightsError(true);

        }

      }

    };

    setInsights(null);

    setInsightsError(false);

    poll();

    const id = window.setInterval(poll, 15_000);

    return () => {

      alive = false;

      window.clearInterval(id);

    };

  }, [matchCode]);



  const themeStyle = useMemo(() => {

    if (!tenant) return undefined;

    return {

      "--tenant-primary": tenant.primaryColor,

      "--tenant-accent": tenant.accentColor,

    } as React.CSSProperties;

  }, [tenant]);



  const commitMatch = (code: string) => {

    const trimmed = code.trim();

    if (!trimmed) return;

    if (trimmed === matchCode) {
      setRefreshKey((k) => k + 1);
      return;
    }

    const next = new URLSearchParams(params);

    next.set("match", trimmed);

    setParams(next, { replace: false });

  };



  return (

    <section

      className="mx-auto w-full max-w-6xl overflow-x-clip px-4 py-8 sm:px-6 sm:py-10"

      style={themeStyle}

    >

      {tenant && (

        <div

          className="mb-6 flex items-center gap-3 rounded-xl p-4 text-white shadow-card"

          style={{ background: `linear-gradient(135deg, ${tenant.primaryColor}, ${tenant.accentColor})` }}

        >

          {tenant.logoUrl && (

            <img src={tenant.logoUrl} alt="" className="h-9 w-9 rounded-md bg-white/10 object-cover" />

          )}

          <div className="min-w-0 flex-1">

            <div className="text-[11px] uppercase tracking-wide text-white/80">

              White-label broadcast

            </div>

            <div className="truncate text-base font-semibold">{tenant.displayName}</div>

          </div>

          <button

            type="button"

            className="shrink-0 rounded-full bg-white/15 px-3 py-1 text-xs font-semibold text-white/90 ring-1 ring-inset ring-white/25 transition hover:bg-white/25"

            onClick={() => {

              const next = new URLSearchParams(params);

              next.delete("tenant");

              setParams(next, { replace: true });

            }}

          >

            Clear branding

          </button>

        </div>

      )}



      {/* ── Hero ─────────────────────────────────────────────────────── */}

      <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">

        <div className="min-w-0">

          <h1 className="text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">

            {t("watch.title")}

          </h1>

          <p className="mt-2 max-w-3xl text-slate-600">{t("watch.subtitle")}</p>

        </div>

        <CurrentMatchBadge code={matchCode} />

      </header>



      <MatchPicker

        matches={matches}

        loading={matchesLoading}

        pendingValue={pendingCode}

        currentlyWatching={matchCode}

        onPendingChange={setPendingCode}

        onWatch={commitMatch}

      />



      {/* ── Main grid: video + scoreboard | AI insights ──────────────── */}

      <div className="mt-8 grid gap-6 lg:grid-cols-[minmax(0,1.35fr)_minmax(0,1fr)]">

        <div className="min-w-0 space-y-6">

          <StreamTile stream={stream} matchCode={matchCode} />

          <Scoreboard key={`${matchCode}-${refreshKey}`} matchCode={matchCode} autoStart hideControls />

        </div>



        <aside className="min-w-0 space-y-5">

          <InsightsPanel

            insights={insights}

            matchCode={matchCode}

            failed={insightsError}

          />

          <div className="public-card p-5 text-sm">

            <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">

              Tips

            </div>

            <ul className="mt-2 space-y-1.5 text-slate-600 [&_code]:inline-block [&_code]:max-w-full [&_code]:break-all [&_code]:rounded [&_code]:bg-slate-100 [&_code]:px-1 [&_code]:font-mono [&_code]:text-[12px] [&_code]:align-middle">

              <li>

                Pick a match from the dropdown, then click <strong>Watch match</strong>.

              </li>

              <li>

                Broadcasters can re-brand the header via <code>?tenant=CODE</code>.

              </li>

              <li>

                Need an OBS overlay? Open <code>/overlay?match={matchCode}</code>.

              </li>

            </ul>

          </div>

        </aside>

      </div>

    </section>

  );

}



// ── Sub-components ───────────────────────────────────────────────────────



function CurrentMatchBadge({ code }: { code: string }) {

  return (

    <div className="inline-flex max-w-full shrink-0 items-center gap-2 self-start overflow-hidden rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 shadow-sm">

      <span className="relative flex h-2 w-2 shrink-0">

        <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-red-400 opacity-75" />

        <span className="relative inline-flex h-2 w-2 rounded-full bg-red-500" />

      </span>

      <span className="shrink-0 uppercase tracking-wide text-slate-500">Watching</span>

      <span className="truncate font-mono text-slate-900">{code}</span>

    </div>

  );

}



function MatchPicker({

  matches,

  loading,

  pendingValue,

  currentlyWatching,

  onPendingChange,

  onWatch,

}: {

  matches: MatchSummary[];

  loading: boolean;

  pendingValue: string;

  currentlyWatching: string;

  onPendingChange: (code: string) => void;

  onWatch: (code: string) => void;

}) {

  const hasPending = matches.some((m) => m.code === pendingValue);

  const trimmed = pendingValue.trim();

  const canSubmit = trimmed.length > 0;

  const isWatching = trimmed === currentlyWatching;



  return (

    <form

      onSubmit={(e) => {

        e.preventDefault();

        onWatch(pendingValue);

      }}

      className="mt-6 rounded-2xl border border-slate-200 bg-white p-3 shadow-card sm:p-4"

    >

      <label className="block">

        <span className="block text-[11px] font-semibold uppercase tracking-wide text-slate-500">

          Match

        </span>

        <div className="mt-1 flex flex-col gap-2 sm:flex-row sm:items-center">

          <select

            value={pendingValue}

            onChange={(e) => onPendingChange(e.target.value)}

            disabled={loading}

            className="w-full flex-1 rounded-lg border border-slate-300 bg-white px-3 py-2.5 font-mono text-sm text-slate-900 shadow-sm focus:border-smf-500 focus:outline-none focus:ring-2 focus:ring-smf-500/30 disabled:bg-slate-50 disabled:text-slate-400"

          >

            {loading && <option value={pendingValue}>Loading matches…</option>}

            {!loading && !hasPending && pendingValue && (

              <option value={pendingValue}>{pendingValue} (not in list)</option>

            )}

            {!loading &&

              matches.map((m) => (

                <option key={m.code} value={m.code}>

                  {formatMatchOption(m)}

                </option>

              ))}

            {!loading && matches.length === 0 && (

              <option value="match-001">match-001</option>

            )}

          </select>

          <button type="submit" disabled={!canSubmit} className="btn-primary sm:w-auto">

            {!canSubmit ? "Select a match" : isWatching ? "Reload match" : "Watch match"}

          </button>

        </div>

        <p className="mt-2 text-xs text-slate-500">

          {loading

            ? "Fetching matches…"

            : `${matches.length} match${matches.length === 1 ? "" : "es"} available`}

        </p>

      </label>

    </form>

  );

}



function formatMatchOption(m: MatchSummary): string {

  const live = m.isTimerRunning ? " · clock running" : "";

  const when = new Date(m.scheduledAtUtc).toLocaleString([], {

    month: "short",

    day: "numeric",

    hour: "2-digit",

    minute: "2-digit",

  });

  return `${m.code} — ${m.status}${live} · ${when}`;

}



function StreamTile({

  stream,

  matchCode,

}: {

  stream: MatchLiveStreamDto | null;

  matchCode: string;

}) {

  if (stream?.url && stream.provider) {

    const useNative =

      stream.provider === "CustomEmbed" && isDirectVideoUrl(stream.url);



    return (

      <div className="aspect-video w-full overflow-hidden rounded-2xl bg-black shadow-card ring-1 ring-slate-200/70">

        {useNative ? (

          <HlsVideoPlayer

            src={stream.url}

            className="h-full w-full"

            controls

            autoPlay

          />

        ) : (

          <iframe

            className="h-full w-full"

            src={buildEmbedUrl(stream.provider, stream.url)}

            title={`Live stream for ${matchCode}`}

            allow="autoplay; encrypted-media; picture-in-picture"

            allowFullScreen

          />

        )}

      </div>

    );

  }

  return (

    <div className="relative aspect-video w-full overflow-hidden rounded-2xl bg-gradient-to-br from-slate-900 via-slate-800 to-slate-950 text-white shadow-card ring-1 ring-slate-900/10">

      <div

        aria-hidden

        className="absolute inset-0 opacity-20"

        style={{

          backgroundImage:

            "radial-gradient(circle at 20% 30%, rgba(255,255,255,0.25), transparent 40%), radial-gradient(circle at 80% 70%, rgba(255,255,255,0.15), transparent 45%)",

        }}

      />

      <div className="absolute inset-0 flex flex-col items-center justify-center gap-3 px-6 text-center">

        <div className="inline-flex items-center gap-2 rounded-full bg-white/10 px-3 py-1 text-[11px] font-semibold uppercase tracking-wider text-white/80 ring-1 ring-inset ring-white/20">

          <span className="h-1.5 w-1.5 rounded-full bg-amber-400" />

          Broadcast offline

        </div>

        <div className="text-xl font-semibold">No broadcast URL yet for this match</div>

        <p className="max-w-md text-sm text-white/70">

          You'll still see real-time scoring below. Federation staff can attach

          a YouTube, Twitch, Vimeo or custom embed from the admin panel to

          start streaming.

        </p>

      </div>

    </div>

  );

}



function InsightsPanel({

  insights,

  matchCode,

  failed,

}: {

  insights: MatchInsights | null;

  matchCode: string;

  failed: boolean;

}) {

  const timelineMax = useMemo(() => {

    if (!insights?.timeline.length) return 1;

    return Math.max(1, ...insights.timeline.map((x) => x.redStrikes + x.blueStrikes));

  }, [insights?.timeline]);



  if (!insights) {

    return (

      <div className="public-card p-5 text-sm text-slate-600">

        <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">

          AI match insights

        </div>

        {failed ? (

          <p className="mt-2 text-slate-500">

            Could not load insights for{" "}

            <code className="rounded bg-slate-100 px-1 font-mono">{matchCode}</code>.

            {" "}Try <strong>match-002</strong> (live demo) from the dropdown, or score

            this bout from the admin panel first.

          </p>

        ) : (

          <div className="mt-2 flex items-center gap-2 text-slate-500">

            <span className="inline-block h-2 w-2 animate-pulse rounded-full bg-smf-500" />

            <span>

              Loading insights for{" "}

              <code className="rounded bg-slate-100 px-1 font-mono">{matchCode}</code>…

            </span>

          </div>

        )}

      </div>

    );

  }

  const redPct = Math.round(insights.redWinProbability * 100);

  const bluePct = 100 - redPct;

  return (

    <div className="public-card p-5">

      <div className="flex items-center justify-between">

        <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">

          AI match insights

        </div>

        <span className="chip bg-smf-50 text-smf-700 ring-smf-500/30">{insights.status}</span>

      </div>



      <div className="mt-4 grid grid-cols-2 gap-3 text-sm">

        <InsightStat label="Total strikes" value={insights.totalStrikes.toString()} />

        <InsightStat

          label="Duration"

          value={`${insights.matchDurationMinutes.toFixed(1)} min`}

        />

        <InsightStat

          label="Red tempo"

          value={`${insights.red.strikesPerMinute.toFixed(1)}/min`}

        />

        <InsightStat

          label="Blue tempo"

          value={`${insights.blue.strikesPerMinute.toFixed(1)}/min`}

        />

      </div>



      <div className="mt-5">

        <div className="flex items-center justify-between text-xs font-medium text-slate-500">

          <span>Red win probability</span>

          <span>{redPct}%</span>

        </div>

        <div className="mt-1 flex h-2.5 overflow-hidden rounded-full bg-slate-100">

          <div className="h-full bg-red-500" style={{ width: `${redPct}%` }} />

          <div className="h-full bg-blue-500" style={{ width: `${bluePct}%` }} />

        </div>

      </div>



      <p className="mt-4 text-sm text-slate-700">{insights.predictionNarrative}</p>

      <p className="mt-2 text-xs italic text-slate-500">{insights.momentumLabel}</p>



      {insights.timeline.length > 0 && (

        <div className="mt-4">

          <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">

            Per-minute timeline

          </div>

          <div className="mt-2 flex items-end gap-1.5">

            {insights.timeline.map((b) => {

              const totalH = Math.round(((b.redStrikes + b.blueStrikes) / timelineMax) * 60);

              const redH = Math.round(

                (b.redStrikes / Math.max(1, b.redStrikes + b.blueStrikes)) * totalH,

              );

              return (

                <div

                  key={b.minuteOffset}

                  className="flex h-[64px] flex-col-reverse items-center gap-0.5"

                  title={`min ${b.minuteOffset}: R${b.redStrikes} · B${b.blueStrikes}`}

                >

                  <div className="w-3 rounded-t bg-red-500" style={{ height: `${redH}px` }} />

                  <div

                    className="w-3 rounded-t bg-blue-500"

                    style={{ height: `${totalH - redH}px` }}

                  />

                </div>

              );

            })}

          </div>

        </div>

      )}

    </div>

  );

}



function InsightStat({ label, value }: { label: string; value: string }) {

  return (

    <div className="rounded-xl border border-slate-100 bg-slate-50 p-3">

      <div className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{label}</div>

      <div className="mt-0.5 text-base font-semibold text-slate-900">{value}</div>

    </div>

  );

}


