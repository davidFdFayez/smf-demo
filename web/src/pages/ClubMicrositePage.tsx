import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getClubMicrosite, type ClubMicrositeDetails } from "../services/micrositesApi";

export function ClubMicrositePage() {
  const { slug = "" } = useParams();
  const [club, setClub] = useState<ClubMicrositeDetails | null>(null);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    const ctrl = new AbortController();
    getClubMicrosite(slug, ctrl.signal)
      .then((c) => {
        setClub(c);
        setErr(null);
      })
      .catch(() => setErr("Club not found or not yet activated."));
    return () => ctrl.abort();
  }, [slug]);

  if (err) {
    return (
      <section className="mx-auto max-w-3xl px-6 py-16 text-center">
        <h1 className="text-2xl font-semibold text-slate-900">Club unavailable</h1>
        <p className="mt-2 text-slate-600">{err}</p>
        <Link to="/clubs" className="btn-primary mt-6 inline-flex">
          Back to clubs directory
        </Link>
      </section>
    );
  }
  if (!club) {
    return (
      <section className="mx-auto max-w-4xl px-6 py-12">
        <p className="text-sm text-slate-500">Loading microsite…</p>
      </section>
    );
  }

  const brand = club.primaryColor ?? "#0c6b3a";

  return (
    <section className="flex flex-col">
      <Hero club={club} brand={brand} />
      <div className="mx-auto w-full max-w-6xl px-6 py-10">
        <div className="grid gap-8 lg:grid-cols-3">
          <div className="lg:col-span-2">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">
              About the club
            </h2>
            <div className="mt-3 text-slate-700 whitespace-pre-wrap">
              {club.about ?? club.description ?? "No description provided yet."}
            </div>

            <h2 className="mt-10 text-sm font-semibold uppercase tracking-wide text-slate-500">
              Roster preview
            </h2>
            {club.roster.length === 0 ? (
              <p className="mt-3 text-sm text-slate-500">No approved members yet.</p>
            ) : (
              <ul className="mt-3 grid gap-3 sm:grid-cols-2">
                {club.roster.map((m) => (
                  <li key={m.id} className="public-card p-3">
                    <div className="text-sm font-medium text-slate-900">{m.fullName}</div>
                    <div className="text-xs text-slate-500">
                      {m.smfId} · {m.role}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <aside className="space-y-5">
            <div className="public-card p-5">
              <h3 className="text-sm font-semibold text-slate-900">Contact</h3>
              <dl className="mt-2 text-sm text-slate-600">
                <dt className="mt-2 text-xs uppercase tracking-wide text-slate-400">Email</dt>
                <dd>{club.contactEmail}</dd>
                <dt className="mt-2 text-xs uppercase tracking-wide text-slate-400">Phone</dt>
                <dd>{club.contactPhone}</dd>
                <dt className="mt-2 text-xs uppercase tracking-wide text-slate-400">City</dt>
                <dd>{club.city}</dd>
                {club.websiteUrl && (
                  <>
                    <dt className="mt-2 text-xs uppercase tracking-wide text-slate-400">Website</dt>
                    <dd>
                      <a className="underline" href={club.websiteUrl} target="_blank" rel="noreferrer">
                        {club.websiteUrl}
                      </a>
                    </dd>
                  </>
                )}
              </dl>
              <Socials club={club} />
            </div>

            <div className="public-card p-5">
              <h3 className="text-sm font-semibold text-slate-900">Upcoming federation events</h3>
              {club.upcomingEvents.length === 0 ? (
                <p className="mt-2 text-sm text-slate-500">No upcoming events on the calendar.</p>
              ) : (
                <ul className="mt-3 space-y-2 text-sm text-slate-600">
                  {club.upcomingEvents.map((e) => (
                    <li key={e.id} className="border-t border-slate-100 pt-2 first:border-t-0 first:pt-0">
                      <div className="font-medium text-slate-900">{e.title}</div>
                      <div className="text-xs text-slate-500">
                        {e.location} · {new Date(e.startsAtUtc).toLocaleString()}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
              <Link to="/events" className="btn-outline mt-4 inline-flex w-full justify-center">
                See all events
              </Link>
            </div>
          </aside>
        </div>
      </div>
    </section>
  );
}

function Hero({ club, brand }: { club: ClubMicrositeDetails; brand: string }) {
  const style = {
    background:
      club.heroImageUrl
        ? `linear-gradient(135deg, ${brand}cc, ${brand}77), url("${club.heroImageUrl}") center/cover no-repeat`
        : `linear-gradient(135deg, ${brand}, #0f172a)`,
  } as React.CSSProperties;

  return (
    <header className="text-white" style={style}>
      <div className="mx-auto flex max-w-6xl flex-col gap-4 px-6 py-14">
        <Link to="/clubs" className="text-xs text-white/80 underline-offset-2 hover:underline">
          ← All clubs
        </Link>
        <div className="flex items-start gap-4">
          {club.logoUrl && (
            <img
              src={club.logoUrl}
              alt=""
              className="h-20 w-20 rounded-xl border border-white/30 bg-white/10 object-cover"
            />
          )}
          <div>
            <div className="text-xs font-semibold uppercase tracking-wide text-white/80">
              {club.city} · {club.status}
            </div>
            <h1 className="mt-1 text-3xl font-bold tracking-tight sm:text-4xl">{club.name}</h1>
            {club.headline && <p className="mt-2 max-w-2xl text-white/90">{club.headline}</p>}
          </div>
        </div>
        <div className="mt-6 flex flex-wrap gap-4 text-sm text-white/90">
          <Stat label="Members" value={club.memberCount.toString()} />
          <Stat label="Roster preview" value={club.roster.length.toString()} />
        </div>
      </div>
    </header>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-white/30 bg-white/10 px-4 py-3 backdrop-blur">
      <div className="text-[11px] uppercase tracking-wide text-white/80">{label}</div>
      <div className="mt-0.5 text-lg font-semibold">{value}</div>
    </div>
  );
}

function Socials({ club }: { club: ClubMicrositeDetails }) {
  const links: { label: string; href: string }[] = [];
  if (club.instagramHandle)
    links.push({ label: `Instagram @${club.instagramHandle}`, href: `https://instagram.com/${club.instagramHandle}` });
  if (club.twitterHandle)
    links.push({ label: `Twitter / X @${club.twitterHandle}`, href: `https://x.com/${club.twitterHandle}` });
  if (club.youtubeChannel)
    links.push({ label: `YouTube ${club.youtubeChannel}`, href: club.youtubeChannel.startsWith("http") ? club.youtubeChannel : `https://youtube.com/@${club.youtubeChannel}` });
  if (links.length === 0) return null;
  return (
    <div className="mt-4 space-y-1 text-sm">
      {links.map((l) => (
        <a key={l.href} className="block text-smf-700 hover:underline" href={l.href} target="_blank" rel="noreferrer">
          {l.label}
        </a>
      ))}
    </div>
  );
}
