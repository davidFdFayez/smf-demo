import { Link } from "react-router-dom";
import { useI18n } from "../i18n";

export function AboutPage() {
  const { t } = useI18n();
  return (
    <section className="mx-auto max-w-4xl px-6 py-16">
      <h1 className="text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">
        {t("about.title")}
      </h1>
      <p className="mt-3 text-lg text-slate-600">{t("about.subtitle")}</p>

      <div className="mt-10 grid gap-6 sm:grid-cols-2">
        <Block
          title="Mission"
          body="Govern MuayThai in the Kingdom with transparency, safety, and sporting excellence — from grassroots participation to elite competition."
        />
        <Block
          title="Values"
          body="Integrity, fair play, safeguarding of minors, anti-doping compliance, and pathways to international representation."
        />
        <Block
          title="Membership"
          body="Athletes, coaches, referees, and clubs register through our unified platform with digital ID issuance and compliance attestations."
        />
        <Block
          title="Events"
          body="National tournaments, rankings, and certifications. Live scoring via our real-time referee consoles."
        />
      </div>

      <div className="mt-12 public-card p-6">
        <h3 className="text-lg font-semibold text-slate-900">Questions, concerns, or press?</h3>
        <p className="mt-2 text-sm text-slate-600">
          For confidential reports, use the integrity channel. For press and general inquiries,
          please contact the federation office via the channels listed in the footer.
        </p>
        <div className="mt-5 flex flex-wrap gap-3">
          <Link to="/safeguarding" className="btn-primary">Report a concern</Link>
          <Link to="/register" className="btn-outline">Become a member</Link>
        </div>
      </div>
    </section>
  );
}

function Block({ title, body }: { title: string; body: string }) {
  return (
    <div className="public-card p-5">
      <h3 className="text-base font-semibold text-slate-900">{title}</h3>
      <p className="mt-2 text-sm text-slate-600">{body}</p>
    </div>
  );
}
