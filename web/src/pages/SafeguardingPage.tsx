import { useI18n } from "../i18n";
import { SafeguardingTab } from "../features/safeguarding/SafeguardingTab";

export function SafeguardingPage() {
  const { t } = useI18n();
  return (
    <section className="mx-auto max-w-3xl px-6 py-12">
      <h1 className="text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl">
        {t("safeguarding.title")}
      </h1>
      <p className="mt-2 max-w-2xl text-slate-600">{t("safeguarding.subtitle")}</p>
      <div className="mt-8">
        <SafeguardingTab />
      </div>
    </section>
  );
}
