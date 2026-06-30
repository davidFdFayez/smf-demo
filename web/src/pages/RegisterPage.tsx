import { useI18n } from "../i18n";
import { RegisterMemberForm } from "../features/members/RegisterMemberForm";

export function RegisterPage() {
  const { t } = useI18n();
  return (
    <PageShell title={t("register.title")} subtitle={t("register.subtitle")}>
      <div className="public-card p-6 sm:p-8">
        <RegisterMemberForm />
      </div>
    </PageShell>
  );
}

function PageShell({ title, subtitle, children }: { title: string; subtitle: string; children: React.ReactNode }) {
  return (
    <section className="mx-auto max-w-4xl px-6 py-12">
      <h1 className="text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">{title}</h1>
      <p className="mt-2 max-w-3xl text-slate-600">{subtitle}</p>
      <div className="mt-8">{children}</div>
    </section>
  );
}
