import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { strings, type Locale, type MessageKey } from "./strings";

/**
 * Minimal i18n provider — picks a locale, applies <html lang> and <html dir>,
 * and exposes a `t()` function for lookup. Persists the user's choice in
 * localStorage so navigating between tabs / reloading the page preserves it.
 *
 * Intentionally light: we don't pull react-intl / i18next for this app size.
 * If the key count outgrows a handful of screens, swap in react-intl and
 * keep `strings.ts` as the message catalog.
 */
interface I18nContextValue {
  locale: Locale;
  dir: "ltr" | "rtl";
  t: (key: MessageKey) => string;
  setLocale: (locale: Locale) => void;
  toggleLocale: () => void;
}

const I18nContext = createContext<I18nContextValue | null>(null);

const STORAGE_KEY = "smf.locale";

function resolveInitialLocale(): Locale {
  if (typeof window === "undefined") return "en";
  const stored = window.localStorage.getItem(STORAGE_KEY);
  if (stored === "en" || stored === "ar") return stored;
  // Arabic is a first-class locale for this federation — pick it automatically
  // if the browser's preferred language starts with "ar".
  const browser = window.navigator.language.toLowerCase();
  return browser.startsWith("ar") ? "ar" : "en";
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState<Locale>(resolveInitialLocale);

  const dir: "ltr" | "rtl" = locale === "ar" ? "rtl" : "ltr";

  useEffect(() => {
    // Propagate to <html> so CSS can key off [dir="rtl"] and screen-readers
    // get the correct language announcement.
    document.documentElement.lang = locale;
    document.documentElement.dir = dir;
    window.localStorage.setItem(STORAGE_KEY, locale);
  }, [locale, dir]);

  const t = useCallback(
    (key: MessageKey): string => {
      const bundle = strings[locale];
      return bundle[key] ?? strings.en[key] ?? key;
    },
    [locale],
  );

  const setLocale = useCallback((l: Locale) => setLocaleState(l), []);
  const toggleLocale = useCallback(
    () => setLocaleState((l) => (l === "en" ? "ar" : "en")),
    [],
  );

  const value = useMemo<I18nContextValue>(
    () => ({ locale, dir, t, setLocale, toggleLocale }),
    [locale, dir, t, setLocale, toggleLocale],
  );

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18nContextValue {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error("useI18n must be used inside <I18nProvider>.");
  return ctx;
}

export function LanguageSwitcher() {
  const { locale, toggleLocale, t } = useI18n();
  return (
    <button
      type="button"
      onClick={toggleLocale}
      aria-label={t("language.switch")}
      className="rounded-full border border-slate-300 bg-white px-3 py-1 text-xs font-semibold uppercase tracking-wide text-slate-700 hover:bg-slate-50"
    >
      {locale === "en" ? "العربية" : "English"}
    </button>
  );
}

export type { Locale, MessageKey } from "./strings";
